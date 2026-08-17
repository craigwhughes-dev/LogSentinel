using LogSentinel.Config;

namespace LogSentinel.Scanning;

public interface IPowerShellRunner
{
    Task<ScanResult> RunAsync(LogDirConfig dirConfig, int daysBack, int contextLines, IReadOnlyList<PatternConfig> patterns, CancellationToken cancellationToken = default);
}
