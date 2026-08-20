using LogSentinel.ClaudeIntegration;
using LogSentinel.Config;
using LogSentinel.Handoff;
using LogSentinel.Reporting;
using LogSentinel.Scanning;

namespace LogSentinel.Tests;

public class HandoffWriterTests
{
    private static LogDirConfig MakeDirConfig(string name = "myapp") => new()
    {
        Name = name,
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"logsentinel_handoff_{Guid.NewGuid():N}.md");

    [Fact]
    public void BuildSection_NoIssuesAcrossAllDirs_SaysNoIssuesFound()
    {
        var entries = new[]
        {
            new DirReportEntry { DirConfig = MakeDirConfig(), ScanResult = new ScanResult() },
        };

        var section = HandoffWriter.BuildSection(entries, DateTimeOffset.Now);

        Assert.Contains("No issues found", section);
        Assert.DoesNotContain("myapp", section); // dirs with 0 issues are omitted entirely
    }

    [Fact]
    public void BuildSection_DirWithZeroIssues_OmittedFromOutput()
    {
        var entries = new[]
        {
            new DirReportEntry { DirConfig = MakeDirConfig("clean_app"), ScanResult = new ScanResult() },
            new DirReportEntry
            {
                DirConfig = MakeDirConfig("noisy_app"),
                ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
            },
        };

        var section = HandoffWriter.BuildSection(entries, DateTimeOffset.Now);

        Assert.DoesNotContain("clean_app", section);
        Assert.Contains("noisy_app", section);
    }

    [Fact]
    public void BuildSection_ClaudeDisabled_ListsRawIssues()
    {
        var entries = new[]
        {
            new DirReportEntry
            {
                DirConfig = MakeDirConfig(),
                ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
                ClaudeResult = null,
            },
        };

        var section = HandoffWriter.BuildSection(entries, DateTimeOffset.Now);

        Assert.Contains("Claude investigation disabled", section);
        Assert.Contains("ERROR something broke", section);
    }

    [Fact]
    public void BuildSection_ClaudeSucceeded_DemotesResponseHeadingsAndIncludesText()
    {
        var entries = new[]
        {
            new DirReportEntry
            {
                DirConfig = MakeDirConfig(),
                ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
                ClaudeResult = ClaudeInvocationResult.Ok("## Root Cause\nOrder API returned 503."),
            },
        };

        var section = HandoffWriter.BuildSection(entries, DateTimeOffset.Now);

        Assert.Contains("### Root Cause", section); // demoted from ## to ###
        Assert.Contains("Order API returned 503", section);
    }

    [Fact]
    public void BuildSection_ClaudeFailed_ShowsFailureReasonAndFallsBackToRawIssues()
    {
        var entries = new[]
        {
            new DirReportEntry
            {
                DirConfig = MakeDirConfig(),
                ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
                ClaudeResult = ClaudeInvocationResult.Failed("timed out after 300s", timedOut: true),
            },
        };

        var section = HandoffWriter.BuildSection(entries, DateTimeOffset.Now);

        Assert.Contains("timed out after 300s", section);
        Assert.Contains("ERROR something broke", section);
    }

    [Fact]
    public void UpdateDoc_FileDoesNotExist_CreatesItWithJustTheSection()
    {
        var path = TempPath();
        try
        {
            var entries = new[] { new DirReportEntry { DirConfig = MakeDirConfig(), ScanResult = new ScanResult() } };
            HandoffWriter.UpdateDoc(path, entries, DateTimeOffset.Now);

            var content = File.ReadAllText(path);
            Assert.Contains("<!-- LOGSENTINEL:START -->", content);
            Assert.Contains("<!-- LOGSENTINEL:END -->", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateDoc_ExistingFileNoMarkers_AppendsSectionAndPreservesOriginalContent()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "# Handoff\n\nSome existing notes.\n");
            var entries = new[] { new DirReportEntry { DirConfig = MakeDirConfig(), ScanResult = new ScanResult() } };
            HandoffWriter.UpdateDoc(path, entries, DateTimeOffset.Now);

            var content = File.ReadAllText(path);
            Assert.Contains("Some existing notes.", content);
            Assert.Contains("<!-- LOGSENTINEL:START -->", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateDoc_ExistingMarkers_ReplacesOnlyTheManagedSection()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path,
                "# Handoff\n\n" +
                "Notes above.\n\n" +
                "<!-- LOGSENTINEL:START -->\n" +
                "## LogSentinel Findings (auto-updated 2026-01-01 00:00 +00:00)\n\n" +
                "### old_app — 3 issue(s)\n" +
                "<!-- LOGSENTINEL:END -->\n\n" +
                "Notes below.\n");

            var entries = new[]
            {
                new DirReportEntry
                {
                    DirConfig = MakeDirConfig("new_app"),
                    ScanResult = new ScanResult { Issues = new[] { MakeIssue() } },
                },
            };
            HandoffWriter.UpdateDoc(path, entries, DateTimeOffset.Now);

            var content = File.ReadAllText(path);
            Assert.Contains("Notes above.", content);
            Assert.Contains("Notes below.", content);
            Assert.Contains("new_app", content);
            Assert.DoesNotContain("old_app", content); // stale entry removed, not accumulated
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateDoc_CalledTwice_DoesNotDuplicateMarkers()
    {
        var path = TempPath();
        try
        {
            var entries = new[] { new DirReportEntry { DirConfig = MakeDirConfig(), ScanResult = new ScanResult() } };
            HandoffWriter.UpdateDoc(path, entries, DateTimeOffset.Now);
            HandoffWriter.UpdateDoc(path, entries, DateTimeOffset.Now);

            var content = File.ReadAllText(path);
            var startCount = content.Split("<!-- LOGSENTINEL:START -->").Length - 1;
            var endCount = content.Split("<!-- LOGSENTINEL:END -->").Length - 1;
            Assert.Equal(1, startCount);
            Assert.Equal(1, endCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
