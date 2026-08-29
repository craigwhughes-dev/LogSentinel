using LogSentinel.ClaudeIntegration;
using LogSentinel.Config;
using LogSentinel.Handoff;
using LogSentinel.Logging;
using LogSentinel.Reporting;
using LogSentinel.Scanning;

namespace LogSentinel;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var configPath = ParseConfigArg(args) ?? Path.Combine(repoRoot, "config", "log_sentinel.config.json");

        AppConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Config error: {ex.Message}");
            return 2;
        }

        var scriptPath = ResolveScriptPath(repoRoot);
        var reportDir = ResolveRelative(repoRoot, config.ReportDir);
        var runLogDir = ResolveRelative(repoRoot, config.RunLogDir);

        IPowerShellRunner scanner = new PowerShellRunner(scriptPath);
        IClaudeInvoker claudeInvoker = new ClaudeInvoker();

        var timestamp = DateTimeOffset.Now;
        var entries = new List<DirReportEntry>();

        foreach (var dirConfig in config.LogDirs)
        {
            ScanResult scanResult;
            try
            {
                scanResult = await scanner.RunAsync(dirConfig, config.DaysToCheck, config.ContextLines, config.Patterns);
                scanResult = scanResult with { Issues = IssueDeduplicator.Dedup(scanResult.Issues) };
            }
            catch (PowerShellInvocationException ex)
            {
                Console.Error.WriteLine($"[{dirConfig.Name}] scan failed: {ex.Message}");
                scanResult = new ScanResult { ScanErrors = new[] { ex.Message } };
            }

            ClaudeInvocationResult? claudeResult = null;
            if (scanResult.Issues.Count > 0 && config.Claude.Enabled)
            {
                Console.WriteLine($"[{dirConfig.Name}] {scanResult.Issues.Count} issue(s) found — invoking Claude for investigation...");
                claudeResult = await claudeInvoker.InvestigateAsync(scanResult.Issues, dirConfig, config.Claude);
                if (!claudeResult.Success)
                {
                    Console.Error.WriteLine($"[{dirConfig.Name}] Claude investigation failed: {claudeResult.FailureReason}");
                }
            }

            var reportPath = ReportWriter.WriteDirReport(reportDir, dirConfig, scanResult, claudeResult, timestamp);
            entries.Add(new DirReportEntry
            {
                DirConfig = dirConfig,
                ScanResult = scanResult,
                ClaudeResult = claudeResult,
                ReportFilePath = reportPath,
            });
        }

        var summaryPath = ReportWriter.WriteSummary(reportDir, entries, timestamp);
        var runLogEntry = RunLogger.BuildEntry(entries, summaryPath, timestamp);
        RunLogger.Append(runLogDir, runLogEntry);

        if (!string.IsNullOrWhiteSpace(config.HandoffDocPath))
        {
            var handoffPath = ResolveRelative(repoRoot, config.HandoffDocPath);
            try
            {
                HandoffWriter.UpdateDoc(handoffPath, entries, timestamp);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to update handoff doc '{handoffPath}': {ex.Message}");
            }
        }

        var totalIssues = entries.Sum(e => e.ScanResult.Issues.Count);
        Console.WriteLine($"Done. {totalIssues} issue(s) across {entries.Count} log dir(s). Summary: {summaryPath}");

        return RunLogger.HasScanErrors(runLogEntry) ? 1 : 0;
    }

    private static string? ParseConfigArg(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--config" or "-c")
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string ResolveRelative(string repoRoot, string maybeRelativePath) =>
        Path.IsPathRooted(maybeRelativePath) ? maybeRelativePath : Path.Combine(repoRoot, maybeRelativePath);

    private static string ResolveScriptPath(string repoRoot)
    {
        var alongside = Path.Combine(AppContext.BaseDirectory, "scripts", "Search-Logs.ps1");
        if (File.Exists(alongside))
        {
            return alongside;
        }

        return Path.Combine(repoRoot, "scripts", "Search-Logs.ps1");
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LogSentinel.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "LogSentinel.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate LogSentinel.sln by walking up from '{startDir}'. Run from within the LogSentinel repo.");
    }
}
