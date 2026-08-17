namespace LogSentinel.ClaudeIntegration;

public sealed record ClaudeInvocationResult
{
    public required bool Success { get; init; }
    public string? ResponseText { get; init; }
    public string? FailureReason { get; init; }
    public bool TimedOut { get; init; }

    public static ClaudeInvocationResult Ok(string responseText) => new() { Success = true, ResponseText = responseText };

    public static ClaudeInvocationResult Failed(string reason, bool timedOut = false) =>
        new() { Success = false, FailureReason = reason, TimedOut = timedOut };
}
