using LogSentinel.Scanning;
using System.Text.Json;

namespace LogSentinel.Tests;

public class ScanResultParsingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void Deserialize_TypicalPowerShellOutput_ParsesIssuesAndScanErrors()
    {
        // Shape mirrors what scripts/Search-Logs.ps1 emits via ConvertTo-Json.
        const string json = """
        {
          "issues": [
            {
              "file": "C:\\logs\\run_20260817.log",
              "line_number": 42,
              "pattern_name": "error",
              "severity": "error",
              "line": "2026-08-17 08:00:12 ERROR Failed to place order",
              "context_before": ["line before"],
              "context_after": ["line after"]
            }
          ],
          "scan_errors": ["Log directory not found: C:\\missing"]
        }
        """;

        var result = JsonSerializer.Deserialize<ScanResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Single(result!.Issues);
        Assert.Single(result.ScanErrors);

        var issue = result.Issues[0];
        Assert.Equal("C:\\logs\\run_20260817.log", issue.File);
        Assert.Equal(42, issue.LineNumber);
        Assert.Equal("error", issue.PatternName);
        Assert.Equal("error", issue.Severity);
        Assert.Contains("Failed to place order", issue.Line);
        Assert.Equal("line before", Assert.Single(issue.ContextBefore));
        Assert.Equal("line after", Assert.Single(issue.ContextAfter));
        Assert.Equal("Log directory not found: C:\\missing", result.ScanErrors[0]);
    }

    [Fact]
    public void Deserialize_EmptyIssuesAndErrors_ProducesEmptyCollections()
    {
        const string json = """{ "issues": [], "scan_errors": [] }""";

        var result = JsonSerializer.Deserialize<ScanResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result!.Issues);
        Assert.Empty(result.ScanErrors);
    }

    [Fact]
    public void Deserialize_MissingOptionalContextArrays_DefaultsToEmpty()
    {
        const string json = """
        {
          "issues": [
            { "file": "a.log", "line_number": 1, "pattern_name": "error", "severity": "error", "line": "ERROR boom" }
          ],
          "scan_errors": []
        }
        """;

        var result = JsonSerializer.Deserialize<ScanResult>(json, JsonOptions);

        Assert.NotNull(result);
        var issue = Assert.Single(result!.Issues);
        Assert.Empty(issue.ContextBefore);
        Assert.Empty(issue.ContextAfter);
    }
}
