using LogSentinel.Config;
using LogSentinel.Scanning;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LogSentinel.ClaudeIntegration;

/// <summary>
/// Launches the Claude Code CLI headlessly (`claude -p`) via cmd.exe, since on Windows
/// the CLI resolves to a .cmd/.ps1 shim rather than a directly-executable .exe.
/// </summary>
public sealed class ClaudeInvoker : IClaudeInvoker
{
    public async Task<ClaudeInvocationResult> InvestigateAsync(IReadOnlyList<LogIssue> issues, LogDirConfig dirConfig, ClaudeConfig claudeConfig, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(dirConfig.CodebasePath))
        {
            return ClaudeInvocationResult.Failed($"codebase_path does not exist: {dirConfig.CodebasePath}");
        }

        var prompt = PromptBuilder.Build(issues, dirConfig.Name, dirConfig.CodebasePath, claudeConfig.MaxIssuesPerPrompt);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = dirConfig.CodebasePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // claude resolves to a .cmd/.ps1 shim on Windows, so route through cmd.exe /c
        // rather than invoking it directly as ProcessStartInfo.FileName.
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("claude");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--allowedTools");
        startInfo.ArgumentList.Add(claudeConfig.AllowedTools);

        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return ClaudeInvocationResult.Failed($"Failed to launch claude CLI: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(claudeConfig.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillTree(process);
            return ClaudeInvocationResult.Failed(
                $"claude CLI timed out after {claudeConfig.TimeoutSeconds}s for '{dirConfig.Name}'.", timedOut: true);
        }

        if (process.ExitCode != 0)
        {
            return ClaudeInvocationResult.Failed(
                $"claude CLI exited with code {process.ExitCode} for '{dirConfig.Name}'. stderr: {stderr}");
        }

        var responseText = ExtractResponseText(stdout.ToString());
        return ClaudeInvocationResult.Ok(responseText);
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have already exited between the timeout firing and the kill call.
        }
    }

    private static string ExtractResponseText(string stdout)
    {
        // `--output-format json` wraps the answer in an envelope (typically a "result" field).
        // Fall back to raw stdout if the shape doesn't match what we expect, so a CLI
        // output-format change degrades gracefully instead of losing the response.
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.String)
            {
                return resultProp.GetString() ?? stdout;
            }
        }
        catch (JsonException)
        {
            // Not JSON (or not the expected envelope) — return raw stdout below.
        }

        return stdout;
    }
}
