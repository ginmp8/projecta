using System;

namespace Projecta.Execution;

/// <summary>Cooperative limits, not an isolation boundary for malicious code or regex patterns.</summary>
public sealed record EvaluationLimits
{
    public long MaxSteps { get; init; } = 1_000_000;
    public long MaxAllocatedTokens { get; init; } = 250_000;
    public int MaxRangeLength { get; init; } = 100_000;
    public int MaxStringLength { get; init; } = 1_000_000;
    public int MaxDepth { get; init; } = 128;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool AllowDynamicEvaluation { get; init; }

    internal void Validate()
    {
        if (MaxSteps < 1 || MaxAllocatedTokens < 1 || MaxRangeLength < 1 || MaxStringLength < 1 ||
            MaxDepth is < 1 or > 256 || Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(EvaluationLimits));
    }
}

public sealed class ExecutionLimitException : Exception
{
    public string Limit { get; }
    public ExecutionLimitException(string limit) : base($"Projection execution limit reached: {limit}.") => Limit = limit;
}
