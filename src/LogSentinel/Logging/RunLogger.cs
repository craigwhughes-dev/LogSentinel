using System.Text.Json;
using LogSentinel.Reporting;

namespace LogSentinel.Logging;

public sealed record RunLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required IReadOnlyList<RunLogDirEntry> Dirs { get; init; }
    public required string SummaryReportPath { get; init; }
}

public sealed record RunLogDirEntry
{
    public required string Name { get; init; }
    public required int IssueCount { get; init; }
    public required IReadOnlyList<string> ScanErrors { get; init; }
    public required string ClaudeStatus { get; init; }
}

public static class RunLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static RunLogEntry BuildEntry(IReadOnlyList<DirReportEntry> entries, string summaryReportPath, DateTimeOffset timestamp)
    {
        var dirs = entries.Select(e => new RunLogDirEntry
        {
            Name = e.DirConfig.Name,
            IssueCount = e.ScanResult.Issues.Count,
            ScanErrors = e.ScanResult.ScanErrors,
            ClaudeStatus = e.ClaudeResult switch
            {
                null => "not_run",
                { Success: true } => "ok",
                { TimedOut: true } => "timed_out",
                _ => "failed",
            },
        }).ToList();

        return new RunLogEntry
        {
            Timestamp = timestamp,
            Dirs = dirs,
            SummaryReportPath = summaryReportPath,
        };
    }

    public static void Append(string runLogDir, RunLogEntry entry)
    {
        Directory.CreateDirectory(runLogDir);
        var path = Path.Combine(runLogDir, "run_log.jsonl");
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        File.AppendAllText(path, line + Environment.NewLine);
    }

    public static bool HasScanErrors(RunLogEntry entry) => entry.Dirs.Any(d => d.ScanErrors.Count > 0);
}
