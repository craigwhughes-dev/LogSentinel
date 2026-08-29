using LogSentinel.Config;
using LogSentinel.Scanning;

namespace LogSentinel.ClaudeIntegration;

public interface IClaudeInvoker
{
    ClaudeInvocationResult Investigate(IReadOnlyList<LogIssue> issues, LogDirConfig dirConfig, ClaudeConfig claudeConfig);
}
