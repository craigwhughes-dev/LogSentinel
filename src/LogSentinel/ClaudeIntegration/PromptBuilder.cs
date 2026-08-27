using LogSentinel.Scanning;
using System.Text;

namespace LogSentinel.ClaudeIntegration;

public static class PromptBuilder
{
    public static string Build(IReadOnlyList<LogIssue> issues, string logDirName, string codebasePath, int maxIssuesPerPrompt)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are investigating {issues.Count} log issue(s) found overnight in the '{logDirName}' log directory.");
        sb.AppendLine($"The codebase that produced these logs is checked out at: {codebasePath}");
        sb.AppendLine("Your working directory is set to that codebase, so use Read/Grep/Glob to find the code responsible for each log line.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: This is an unattended nightly run. Investigate and produce a plan only. Do not edit any files.");
        sb.AppendLine();
        sb.AppendLine("For each distinct issue (group duplicates/repeats of the same underlying problem together):");
        sb.AppendLine("1. State the likely root cause, citing the specific file and line in the codebase — not just the log line.");
        sb.AppendLine("2. If the root cause is NOT obvious from the log excerpt and codebase alone, say so explicitly rather than guessing,");
        sb.AppendLine("   and propose a concrete plan for what additional logging or unit/integration tests would narrow it down next time.");
        sb.AppendLine("3. If the issue looks transient/environmental (network blip, rate limit, timeout, momentary broker/API disconnect, etc.),");
        sb.AppendLine("   recommend a retry/back-off strategy (e.g. exponential backoff, max-retry count) instead of a code fix for a one-off.");
        sb.AppendLine();
        sb.AppendLine("Structure your response per issue (or per issue group) as:");
        sb.AppendLine("## Root Cause");
        sb.AppendLine("## Confidence (obvious / needs-more-diagnostics)");
        sb.AppendLine("## Recommended Fix Plan  (or Recommended Diagnostics Plan, if not obvious)");
        sb.AppendLine("## Retry/Standoff Recommendation  (only if transient)");
        sb.AppendLine();
        sb.AppendLine("Issues:");
        sb.AppendLine();

        var toInclude = issues.Take(maxIssuesPerPrompt).ToList();
        for (var i = 0; i < toInclude.Count; i++)
        {
            var issue = toInclude[i];
            var suffix = issue.OccurrenceCount > 1 ? $" (×{issue.OccurrenceCount} occurrences)" : string.Empty;
            sb.AppendLine($"### Issue {i + 1}: [{issue.Severity}] {issue.PatternName} in {issue.File}:{issue.LineNumber}{suffix}");
            foreach (var line in issue.ContextBefore)
            {
                sb.AppendLine($"    {line}");
            }
            sb.AppendLine($">>> {issue.Line}");
            foreach (var line in issue.ContextAfter)
            {
                sb.AppendLine($"    {line}");
            }
            sb.AppendLine();
        }

        if (issues.Count > maxIssuesPerPrompt)
        {
            sb.AppendLine($"(+{issues.Count - maxIssuesPerPrompt} more issue(s) truncated — same log directory, see the full report for the complete list.)");
        }

        return sb.ToString();
    }
}
