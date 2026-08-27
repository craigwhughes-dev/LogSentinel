using LogSentinel.Scanning;

namespace LogSentinel.Tests;

public class IssueDeduplicatorTests
{
    private static LogIssue MakeIssue(string line, string file = "C:\\logs\\run.log", int lineNumber = 1, string pattern = "error") => new()
    {
        File = file,
        LineNumber = lineNumber,
        PatternName = pattern,
        Severity = "error",
        Line = line,
    };

    [Fact]
    public void Dedup_ExactRepeats_CollapsesToOneWithCount()
    {
        var issues = new[]
        {
            MakeIssue("API connection failed: ConnectionRefusedError(22, 'refused', None, 1225, None)"),
            MakeIssue("API connection failed: ConnectionRefusedError(22, 'refused', None, 1225, None)"),
            MakeIssue("API connection failed: ConnectionRefusedError(22, 'refused', None, 1225, None)"),
        };

        var result = IssueDeduplicator.Dedup(issues);

        var single = Assert.Single(result);
        Assert.Equal(3, single.OccurrenceCount);
    }

    [Fact]
    public void Dedup_SameMessageDifferentTimestampsAndFilenames_StillCollapses()
    {
        var issues = new[]
        {
            MakeIssue("2026-08-26 01:00:09 [ERROR] Error in IBKR data reconciliation: ibkr_reconcile exited 1: Logging to ibkr_reconcile_20260826T010007.log"),
            MakeIssue("2026-08-26 01:01:12 [ERROR] Error in IBKR data reconciliation: ibkr_reconcile exited 1: Logging to ibkr_reconcile_20260826T010110.log"),
        };

        var result = IssueDeduplicator.Dedup(issues);

        var single = Assert.Single(result);
        Assert.Equal(2, single.OccurrenceCount);
    }

    [Fact]
    public void Dedup_KeepsFirstOccurrenceLocationAndLine()
    {
        var issues = new[]
        {
            MakeIssue("ERROR broke", lineNumber: 10),
            MakeIssue("ERROR broke", lineNumber: 99),
        };

        var result = IssueDeduplicator.Dedup(issues);

        var single = Assert.Single(result);
        Assert.Equal(10, single.LineNumber);
    }

    [Fact]
    public void Dedup_DistinctMessages_KeepsSeparateWithCountOne()
    {
        var issues = new[]
        {
            MakeIssue("ERROR broke A"),
            MakeIssue("ERROR broke B"),
        };

        var result = IssueDeduplicator.Dedup(issues);

        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal(1, i.OccurrenceCount));
    }

    [Fact]
    public void Dedup_SamePatternDifferentPatternName_KeptSeparate()
    {
        var issues = new[]
        {
            MakeIssue("something happened", pattern: "error"),
            MakeIssue("something happened", pattern: "warning"),
        };

        var result = IssueDeduplicator.Dedup(issues);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Dedup_PreservesFirstSeenOrder()
    {
        var issues = new[]
        {
            MakeIssue("B first"),
            MakeIssue("A first"),
            MakeIssue("B first"),
        };

        var result = IssueDeduplicator.Dedup(issues);

        Assert.Equal("B first", result[0].Line);
        Assert.Equal("A first", result[1].Line);
    }

    [Fact]
    public void Dedup_EmptyList_ReturnsEmpty()
    {
        var result = IssueDeduplicator.Dedup(Array.Empty<LogIssue>());

        Assert.Empty(result);
    }
}
