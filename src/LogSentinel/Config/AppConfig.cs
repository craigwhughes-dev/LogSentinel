namespace LogSentinel.Config;

public sealed record PatternConfig
{
    public required string Name { get; init; }
    public required string Regex { get; init; }
    public string Severity { get; init; } = "error";
}

public sealed record ClaudeConfig
{
    public bool Enabled { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 300;
    public string AllowedTools { get; init; } = "Read,Grep,Glob";
    public int MaxIssuesPerPrompt { get; init; } = 25;
}

public sealed record LogDirConfig
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string CodebasePath { get; init; }
    public string FileFilter { get; init; } = "*.log";
    public bool Recurse { get; init; } = true;
}

public sealed record AppConfig
{
    public int DaysToCheck { get; init; } = 1;
    public string ReportDir { get; init; } = "reports";
    public string RunLogDir { get; init; } = "logs";
    public int ContextLines { get; init; } = 2;

    /// <summary>
    /// Optional path to a markdown doc (e.g. a project's HANDOFF.md) whose managed
    /// section gets rewritten each run with the current findings. Null/absent disables it.
    /// </summary>
    public string? HandoffDocPath { get; init; }

    public ClaudeConfig Claude { get; init; } = new();
    public required IReadOnlyList<PatternConfig> Patterns { get; init; }
    public required IReadOnlyList<LogDirConfig> LogDirs { get; init; }
}
