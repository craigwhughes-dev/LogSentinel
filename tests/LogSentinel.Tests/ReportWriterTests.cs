using LogSentinel.ClaudeIntegration;
using LogSentinel.Config;
using LogSentinel.Reporting;
using LogSentinel.Scanning;

namespace LogSentinel.Tests;

public class ReportWriterTests
{
    private static LogDirConfig MakeDirConfig() => new()
    {
        Name = "myapp",
        Path = "C:\\logs\\myapp",
        CodebasePath = "C:\\code\\myapp",
    };

    private static LogIssue MakeIssue() => new()
    {
        File = "C:\\logs\\myapp\\run.log",
        LineNumber = 10,
        PatternName = "error",
        Severity = "error",
        Line = "ERROR something broke",
    };

    [Fact]
    public void BuildDirReport_NoIssues_OmitsIssuesTableAndClaudeSection()
    {
        var scanResult = new ScanResult();
        var report = ReportWriter.BuildDirReport(MakeDirConfig(), scanResult, claudeResult: null, DateTimeOffset.Now);

        Assert.Contains("myapp", report);
        Assert.Contains("Issues found: 0", report);
        Assert.DoesNotContain("## Issues", report);
        Assert.DoesNotContain("## Claude Investigation", report);
    }

    [Fact]
    public void BuildDirReport_WithIssues_RendersIssueTableRow()
    {
        var scanResult = new ScanResult { Issues = new[] { MakeIssue() } };
        var report = ReportWriter.BuildDirReport(MakeDirConfig(), scanResult, claudeResult: null, DateTimeOffset.Now);

        Assert.Contains("## Issues", report);
        Assert.Contains("run.log", report);
        Assert.Contains("| 10 |", report);
        Assert.Contains("ERROR something broke", report);
    }

    [Fact]
    public void BuildDirReport_WithScanErrors_RendersScanErrorsSection()
    {
        var scanResult = new ScanResult { ScanErrors = new[] { "Log directory not found: C:\\missing" } };
        var report = ReportWriter.BuildDirReport(MakeDirConfig(), scanResult, claudeResult: null, DateTimeOffset.Now);

        Assert.Contains("## Scan Errors", report);
        Assert.Contains("Log directory not found", report);
    }

    [Fact]
    public void BuildDirReport_SuccessfulClaudeResult_IncludesResponseText()
    {
        var scanResult = new ScanResult { Issues = new[] { MakeIssue() } };
        var claudeResult = ClaudeInvocationResult.Ok("## Root Cause\nOrder API returned 503.");
        var report = ReportWriter.BuildDirReport(MakeDirConfig(), scanResult, claudeResult, DateTimeOffset.Now);

        Assert.Contains("## Claude Investigation & Fix Plan", report);
        Assert.Contains("Order API returned 503", report);
    }

    [Fact]
    public void BuildDirReport_FailedClaudeResult_IncludesFailureReason()
    {
        var scanResult = new ScanResult { Issues = new[] { MakeIssue() } };
        var claudeResult = ClaudeInvocationResult.Failed("timed out after 300s", timedOut: true);
        var report = ReportWriter.BuildDirReport(MakeDirConfig(), scanResult, claudeResult, DateTimeOffset.Now);

        Assert.Contains("Claude investigation unavailable", report);
        Assert.Contains("timed out after 300s", report);
    }

    [Fact]
    public void BuildSummary_ListsEachDirWithIssueCountAndClaudeStatus()
    {
        var entries = new[]
        {
            new DirReportEntry
            {
                DirConfig = MakeDirConfig(),
                ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
                ClaudeResult = ClaudeInvocationResult.Ok("plan"),
                ReportFilePath = "reports\\myapp_20260817_030000.md",
            },
        };

        var summary = ReportWriter.BuildSummary(entries, DateTimeOffset.Now);

        Assert.Contains("myapp", summary);
        Assert.Contains("| 1 |", summary);
        Assert.Contains("ok", summary);
        Assert.Contains("myapp_20260817_030000.md", summary);
    }

    [Fact]
    public void BuildSummary_ClaudeNotRun_ShowsNotRunStatus()
    {
        var entries = new[]
        {
            new DirReportEntry
            {
                DirConfig = MakeDirConfig(),
                ScanResult = new ScanResult(),
                ClaudeResult = null,
                ReportFilePath = "reports\\myapp.md",
            },
        };

        var summary = ReportWriter.BuildSummary(entries, DateTimeOffset.Now);

        Assert.Contains("not run", summary);
    }
}
