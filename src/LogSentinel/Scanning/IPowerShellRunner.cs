using LogSentinel.Config;

namespace LogSentinel.Scanning;

public interface IPowerShellRunner
{
    ScanResult Run(LogDirConfig dirConfig, int daysBack, int contextLines, IReadOnlyList<PatternConfig> patterns);
}
