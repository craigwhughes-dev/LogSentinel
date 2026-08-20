using LogSentinel.ClaudeIntegration;
using LogSentinel.Config;
using LogSentinel.Scanning;
using System.Text;

namespace LogSentinel.Reporting;

public sealed record DirReportEntry
{
    public required LogDirConfig DirConfig { get; init; }
    public required ScanResult ScanResult { get; init; }
    public ClaudeInvocationResult? ClaudeResult { get; init; }
    public string? ReportFilePath { get; init; }
}

public static class ReportWriter
{
    public static string BuildDirReport(LogDirConfig dirConfig, ScanResult scanResult, ClaudeInvocationResult? claudeResult, DateTimeOffset timestamp)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# LogSentinel report — {dirConfig.Name}");
        sb.AppendLine();
        sb.AppendLine($"Scanned at: {timestamp:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Log directory: {dirConfig.Path}");
        sb.AppendLine($"Codebase: {dirConfig.CodebasePath}");
        sb.AppendLine($"Issues found: {scanResult.Issues.Count}");
        sb.AppendLine();

        if (scanResult.ScanErrors.Count > 0)
        {
            sb.AppendLine("## Scan Errors");
            foreach (var err in scanResult.ScanErrors)
            {
                sb.AppendLine($"- {err}");
            }
            sb.AppendLine();
        }

        if (scanResult.Issues.Count > 0)
        {
            sb.AppendLine("## Issues");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Severity | Pattern | Excerpt |");
            sb.AppendLine("|------|------|----------|---------|---------|");
            foreach (var issue in scanResult.Issues)
            {
                var excerpt = issue.Line.Replace("|", "\\|").Trim();
                if (excerpt.Length > 120)
                {
                    excerpt = excerpt[..120] + "…";
                }
                sb.AppendLine($"| {issue.File} | {issue.LineNumber} | {issue.Severity} | {issue.PatternName} | {excerpt} |");
            }
            sb.AppendLine();
        }

        if (claudeResult is not null)
        {
            sb.AppendLine("## Claude Investigation & Fix Plan");
            sb.AppendLine();
            if (claudeResult.Success)
            {
                sb.AppendLine(claudeResult.ResponseText);
            }
            else
            {
                sb.AppendLine($"Claude investigation unavailable: {claudeResult.FailureReason}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string BuildSummary(IReadOnlyList<DirReportEntry> entries, DateTimeOffset timestamp)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# LogSentinel run summary");
        sb.AppendLine();
        sb.AppendLine($"Run at: {timestamp:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("| Log Dir | Issues | Claude Investigation | Report |");
        sb.AppendLine("|---------|--------|-----------------------|--------|");
        foreach (var entry in entries)
        {
            var claudeStatus = entry.ClaudeResult switch
            {
                null => "not run",
                { Success: true } => "ok",
                { TimedOut: true } => "timed out",
                _ => "failed",
            };
            var reportLink = entry.ReportFilePath is not null ? Path.GetFileName(entry.ReportFilePath) : "-";
            sb.AppendLine($"| {entry.DirConfig.Name} | {entry.ScanResult.Issues.Count} | {claudeStatus} | {reportLink} |");
        }

        return sb.ToString();
    }

    public static string WriteDirReport(string reportDir, LogDirConfig dirConfig, ScanResult scanResult, ClaudeInvocationResult? claudeResult, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(reportDir);
        var fileName = $"{dirConfig.Name}_{timestamp:yyyyMMdd_HHmmss}.md";
        var path = Path.Combine(reportDir, fileName);
        File.WriteAllText(path, BuildDirReport(dirConfig, scanResult, claudeResult, timestamp));
        return path;
    }

    public static string WriteSummary(string reportDir, IReadOnlyList<DirReportEntry> entries, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(reportDir);
        var fileName = $"summary_{timestamp:yyyyMMdd_HHmmss}.md";
        var path = Path.Combine(reportDir, fileName);
        File.WriteAllText(path, BuildSummary(entries, timestamp));
        return path;
    }
}
