namespace LogSentinel.Scanning;

public sealed record LogIssue
{
    public required string File { get; init; }
    public required int LineNumber { get; init; }
    public required string PatternName { get; init; }
    public required string Severity { get; init; }
    public required string Line { get; init; }
    public IReadOnlyList<string> ContextBefore { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ContextAfter { get; init; } = Array.Empty<string>();

    /// <summary>How many raw matches this representative issue was collapsed from by <see cref="IssueDeduplicator"/>.</summary>
    public int OccurrenceCount { get; init; } = 1;
}

public sealed record ScanResult
{
    public IReadOnlyList<LogIssue> Issues { get; init; } = Array.Empty<LogIssue>();
    public IReadOnlyList<string> ScanErrors { get; init; } = Array.Empty<string>();
}
