using LogSentinel.ClaudeIntegration;
using LogSentinel.Scanning;

namespace LogSentinel.Tests;

public class PromptBuilderTests
{
    private static LogIssue MakeIssue(int n) => new()
    {
        File = $"C:\\logs\\file{n}.log",
        LineNumber = n,
        PatternName = "error",
        Severity = "error",
        Line = $"ERROR something broke #{n}",
    };

    [Fact]
    public void Build_IncludesGuidanceForUnclearRootCauseAndTransientRetry()
    {
        var issues = new[] { MakeIssue(1) };

        var prompt = PromptBuilder.Build(issues, "myapp", "C:\\code\\myapp", maxIssuesPerPrompt: 25);

        Assert.Contains("not obvious", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("additional logging", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("back-off", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not edit any files", prompt);
    }

    [Fact]
    public void Build_IncludesCodebasePathSoClaudeCanReadRealCode()
    {
        var issues = new[] { MakeIssue(1) };

        var prompt = PromptBuilder.Build(issues, "myapp", "C:\\code\\myapp", maxIssuesPerPrompt: 25);

        Assert.Contains("C:\\code\\myapp", prompt);
    }

    [Fact]
    public void Build_IncludesEachIssueLineAndContext()
    {
        var issues = new[] { MakeIssue(1), MakeIssue(2) };

        var prompt = PromptBuilder.Build(issues, "myapp", "C:\\code", maxIssuesPerPrompt: 25);

        Assert.Contains("ERROR something broke #1", prompt);
        Assert.Contains("ERROR something broke #2", prompt);
        Assert.Contains("file1.log:1", prompt);
        Assert.Contains("file2.log:2", prompt);
    }

    [Fact]
    public void Build_MoreIssuesThanMax_TruncatesAndNotesRemainder()
    {
        var issues = Enumerable.Range(1, 5).Select(MakeIssue).ToList();

        var prompt = PromptBuilder.Build(issues, "myapp", "C:\\code", maxIssuesPerPrompt: 3);

        Assert.Contains("ERROR something broke #1", prompt);
        Assert.Contains("ERROR something broke #3", prompt);
        Assert.DoesNotContain("ERROR something broke #4", prompt);
        Assert.DoesNotContain("ERROR something broke #5", prompt);
        Assert.Contains("+2 more issue(s) truncated", prompt);
    }

    [Fact]
    public void Build_FewerIssuesThanMax_NoTruncationNote()
    {
        var issues = new[] { MakeIssue(1) };

        var prompt = PromptBuilder.Build(issues, "myapp", "C:\\code", maxIssuesPerPrompt: 25);

        Assert.DoesNotContain("truncated", prompt);
    }
}
