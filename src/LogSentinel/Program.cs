using LogSentinel.ClaudeIntegration;
using LogSentinel.Config;
using LogSentinel.Handoff;
using LogSentinel.Logging;
using LogSentinel.Reporting;
using LogSentinel.Scanning;
using log4net;
using log4net.Config;
using System.Reflection;

namespace LogSentinel;

public static class Program
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

    public static int Main(string[] args)
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

        var timestamp = DateTimeOffset.Now;
        Directory.CreateDirectory(runLogDir);
        GlobalContext.Properties["LogDir"] = runLogDir;
        XmlConfigurator.Configure(LogManager.GetRepository(Assembly.GetExecutingAssembly()),
            new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));

        IPowerShellRunner scanner = new PowerShellRunner(scriptPath);
        IClaudeInvoker claudeInvoker = new ClaudeInvoker();

        var entries = new List<DirReportEntry>();

        foreach (var dirConfig in config.LogDirs)
        {
            ScanResult scanResult;
            try
            {
                scanResult = scanner.Run(dirConfig, config.DaysToCheck, config.ContextLines, config.Patterns);
                scanResult = scanResult with { Issues = IssueDeduplicator.Dedup(scanResult.Issues) };
            }
            catch (PowerShellInvocationException ex)
            {
                Log.Error($"[{dirConfig.Name}] scan failed: {ex.Message}");
                scanResult = new ScanResult { ScanErrors = new[] { ex.Message } };
            }

            foreach (var scanError in scanResult.ScanErrors)
            {
                Log.Error($"[{dirConfig.Name}] scan error: {scanError}");
            }

            var totalOccurrences = scanResult.Issues.Sum(i => i.OccurrenceCount);
            Log.Info(scanResult.Issues.Count > 0
                ? $"[{dirConfig.Name}] scan complete: {scanResult.Issues.Count} distinct issue(s), {totalOccurrences} occurrence(s)"
                : $"[{dirConfig.Name}] scan complete: no issues found");

            foreach (var issue in scanResult.Issues)
            {
                var excerpt = issue.Line.Trim();
                if (excerpt.Length > 200)
                {
                    excerpt = excerpt[..200] + "…";
                }
                Log.Info($"[{dirConfig.Name}] issue: {issue.Severity} | {issue.PatternName} | {issue.File}:{issue.LineNumber} | x{issue.OccurrenceCount} | {excerpt}");
            }

            ClaudeInvocationResult? claudeResult = null;
            if (scanResult.Issues.Count > 0 && config.Claude.Enabled)
            {
                Log.Info($"[{dirConfig.Name}] invoking Claude for investigation...");
                claudeResult = claudeInvoker.Investigate(scanResult.Issues, dirConfig, config.Claude);
                if (claudeResult.Success)
                {
                    Log.Info($"[{dirConfig.Name}] Claude investigation complete ({claudeResult.ResponseText?.Length ?? 0} char response).");
                }
                else
                {
                    Log.Error($"[{dirConfig.Name}] Claude investigation failed: {claudeResult.FailureReason}");
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
                Log.Error($"Failed to update handoff doc '{handoffPath}': {ex.Message}");
            }
        }

        var totalIssues = entries.Sum(e => e.ScanResult.Issues.Count);
        Log.Info($"Done. {totalIssues} issue(s) across {entries.Count} log dir(s). Summary: {summaryPath}");

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
