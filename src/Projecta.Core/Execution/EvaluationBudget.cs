using System;
using System.Diagnostics;
using System.Threading;

namespace Projecta.Execution;

// Evaluation is synchronous. Thread-local scope avoids per-node AsyncLocal writes and
// prevents concurrent calls from sharing counters. Nested evaluation inherits this budget.
internal sealed class EvaluationBudget : IDisposable
{
    [ThreadStatic] private static EvaluationBudget? _current;
    internal static EvaluationBudget? Current => _current;
    internal EvaluationLimits Limits { get; }
    private readonly CancellationToken _cancellation;
    private readonly long _started = Stopwatch.GetTimestamp();
    private long _steps, _tokens;
    private bool _disposed;

    private EvaluationBudget(EvaluationLimits limits, CancellationToken cancellation)
    { Limits = limits; _cancellation = cancellation; _current = this; }

    internal static IDisposable Enter(EvaluationLimits limits, CancellationToken cancellation)
    {
        limits.Validate(); cancellation.ThrowIfCancellationRequested();
        if (_current is not null) { _current.Checkpoint(); return Nested.Instance; }
        return new EvaluationBudget(limits, cancellation);
    }

    internal static void CheckpointCurrent() => _current?.Checkpoint();
    internal void Checkpoint()
    {
        _cancellation.ThrowIfCancellationRequested();
        if (++_steps > Limits.MaxSteps) throw new ExecutionLimitException("steps");
        if (Stopwatch.GetElapsedTime(_started) >= Limits.Timeout) throw new ExecutionLimitException("timeout");
    }
    internal void Allocate()
    {
        if (++_tokens > Limits.MaxAllocatedTokens) throw new ExecutionLimitException("allocated tokens");
        Checkpoint();
    }
    internal void CheckString(long length)
    { Checkpoint(); if (length > Limits.MaxStringLength) throw new ExecutionLimitException("string length"); }
    internal void CheckRange(decimal length)
    { Checkpoint(); if (length > Limits.MaxRangeLength) throw new ExecutionLimitException("range length"); }
    public void Dispose() { if (!_disposed) { _disposed = true; _current = null; } }
    private sealed class Nested : IDisposable
    { internal static readonly Nested Instance = new(); public void Dispose() { } }
}
