using LogSentinel.Reporting;
using System.Text;
using System.Text.RegularExpressions;

namespace LogSentinel.Handoff;

/// <summary>
/// Rewrites a marker-delimited section of an external markdown doc (e.g. a project's
/// HANDOFF.md) with the current run's findings. The section is replaced in place each
/// run, not appended — resolved issues disappear on the next run instead of the doc
/// growing forever, matching HANDOFF.md's "current state" convention.
/// </summary>
public static class HandoffWriter
{
    private const string StartMarker = "<!-- LOGSENTINEL:START -->";
    private const string EndMarker = "<!-- LOGSENTINEL:END -->";

    public static string BuildSection(IReadOnlyList<DirReportEntry> entries, DateTimeOffset timestamp)
    {
        var sb = new StringBuilder();
        sb.AppendLine(StartMarker);
        sb.AppendLine($"## LogSentinel Findings (auto-updated {timestamp:yyyy-MM-dd HH:mm} {timestamp:zzz})");
        sb.AppendLine();

        var withIssues = entries.Where(e => e.ScanResult.Issues.Count > 0).ToList();

        if (withIssues.Count == 0)
        {
            sb.AppendLine("No issues found in the last scan.");
        }
        else
        {
            foreach (var entry in withIssues)
            {
                sb.AppendLine($"### {entry.DirConfig.Name} — {entry.ScanResult.Issues.Count} issue(s)");
                sb.AppendLine();

                if (entry.ClaudeResult is { Success: true } claude)
                {
                    sb.AppendLine(DemoteHeadings(claude.ResponseText ?? string.Empty));
                }
                else if (entry.ClaudeResult is { Success: false } failed)
                {
                    sb.AppendLine($"_Claude investigation unavailable: {failed.FailureReason}_");
                    sb.AppendLine();
                    AppendRawIssueList(sb, entry);
                }
                else
                {
                    sb.AppendLine("_Claude investigation disabled — raw matches below._");
                    sb.AppendLine();
                    AppendRawIssueList(sb, entry);
                }

                if (entry.ReportFilePath is not null)
                {
                    sb.AppendLine($"Full report: `{entry.ReportFilePath}`");
                    sb.AppendLine();
                }
            }
        }

        sb.Append(EndMarker);
        return sb.ToString();
    }

    // Used whenever Claude's own investigation isn't available (disabled, failed, timed
    // out) so the exception detail (stack trace / surrounding lines) still reaches the
    // handoff doc instead of being reduced to a single grep-matched line.
    private static void AppendRawIssueList(StringBuilder sb, DirReportEntry entry)
    {
        foreach (var issue in entry.ScanResult.Issues)
        {
            var suffix = issue.OccurrenceCount > 1 ? $" (×{issue.OccurrenceCount})" : string.Empty;
            sb.AppendLine($"- [{issue.Severity}/{issue.PatternName}] `{issue.File}:{issue.LineNumber}`{suffix}");

            var hasContext = issue.ContextBefore.Count > 0 || issue.ContextAfter.Count > 0;
            if (hasContext)
            {
                sb.AppendLine("  ```");
                foreach (var line in issue.ContextBefore)
                {
                    sb.AppendLine($"  {line}");
                }
                sb.AppendLine($"  >>> {issue.Line.Trim()}");
                foreach (var line in issue.ContextAfter)
                {
                    sb.AppendLine($"  {line}");
                }
                sb.AppendLine("  ```");
            }
            else
            {
                sb.AppendLine($"  {issue.Line.Trim()}");
            }
        }
        sb.AppendLine();
    }

    // Claude's response uses "## Root Cause" etc. Bump every heading down one level so it
    // nests under this section's own "##"/"###" headings instead of colliding with them.
    private static string DemoteHeadings(string markdown) =>
        Regex.Replace(markdown, @"^(#{1,5})\s", "#$1 ", RegexOptions.Multiline);

    public static void UpdateDoc(string path, IReadOnlyList<DirReportEntry> entries, DateTimeOffset timestamp)
    {
        var section = BuildSection(entries, timestamp);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, section + Environment.NewLine);
            return;
        }

        var existing = File.ReadAllText(path);
        var startIdx = existing.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIdx = existing.IndexOf(EndMarker, StringComparison.Ordinal);

        string updated;
        if (startIdx >= 0 && endIdx > startIdx)
        {
            var before = existing[..startIdx];
            var after = existing[(endIdx + EndMarker.Length)..];
            updated = before + section + after;
        }
        else
        {
            var separator = existing.EndsWith(Environment.NewLine) ? Environment.NewLine : Environment.NewLine + Environment.NewLine;
            updated = existing + separator + section + Environment.NewLine;
        }

        File.WriteAllText(path, updated);
    }
}
