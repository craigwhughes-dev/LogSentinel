using LogSentinel.Config;
using LogSentinel.Scanning;

namespace LogSentinel.ClaudeIntegration;

public interface IClaudeInvoker
{
    Task<ClaudeInvocationResult> InvestigateAsync(IReadOnlyList<LogIssue> issues, LogDirConfig dirConfig, ClaudeConfig claudeConfig, CancellationToken cancellationToken = default);
}
