using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LogSentinel.Config;

namespace LogSentinel.Scanning;

public sealed class PowerShellInvocationException : Exception
{
    public PowerShellInvocationException(string message) : base(message) { }
}

public sealed class PowerShellRunner : IPowerShellRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _scriptPath;

    public PowerShellRunner(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Search-Logs.ps1 not found at: {scriptPath}", scriptPath);
        }

        _scriptPath = scriptPath;
    }

    public async Task<ScanResult> RunAsync(LogDirConfig dirConfig, int daysBack, int contextLines, IReadOnlyList<PatternConfig> patterns, CancellationToken cancellationToken = default)
    {
        var patternsJson = JsonSerializer.Serialize(patterns.Select(p => new { name = p.Name, regex = p.Regex, severity = p.Severity }));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(_scriptPath);
        startInfo.ArgumentList.Add("-LogDir");
        startInfo.ArgumentList.Add(dirConfig.Path);
        startInfo.ArgumentList.Add("-DaysBack");
        startInfo.ArgumentList.Add(daysBack.ToString());
        startInfo.ArgumentList.Add("-FileFilter");
        startInfo.ArgumentList.Add(dirConfig.FileFilter);
        startInfo.ArgumentList.Add("-ContextLines");
        startInfo.ArgumentList.Add(contextLines.ToString());
        startInfo.ArgumentList.Add("-PatternsJson");
        startInfo.ArgumentList.Add(patternsJson);
        if (dirConfig.Recurse)
        {
            startInfo.ArgumentList.Add("-Recurse");
        }

        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        var stdoutText = stdout.ToString();
        if (string.IsNullOrWhiteSpace(stdoutText))
        {
            throw new PowerShellInvocationException(
                $"Search-Logs.ps1 for '{dirConfig.Name}' produced no stdout (exit code {process.ExitCode}). stderr: {stderr}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<ScanResult>(stdoutText, JsonOptions);
            return result ?? throw new PowerShellInvocationException($"Search-Logs.ps1 for '{dirConfig.Name}' returned null result.");
        }
        catch (JsonException ex)
        {
            throw new PowerShellInvocationException(
                $"Search-Logs.ps1 for '{dirConfig.Name}' did not return parseable JSON: {ex.Message}. stdout: {stdoutText}");
        }
    }
}
