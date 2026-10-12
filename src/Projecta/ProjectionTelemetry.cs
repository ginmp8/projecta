using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Generic;

namespace Projecta.Mapping;

/// <summary>Low-cardinality telemetry. Sources, variable values, rule text and user IDs are never recorded.</summary>
public static class ProjectionTelemetry
{
    public const string Name = "Projecta";
    public static ActivitySource ActivitySource { get; } = new(Name);
    internal static readonly Meter Meter = new(Name);
    internal static readonly Counter<long> Executions = Meter.CreateCounter<long>("projecta.executions");
    internal static readonly Counter<long> Failures = Meter.CreateCounter<long>("projecta.failures");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("projecta.duration", "ms");
    internal static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>("projecta.stage.duration", "ms");

    internal static void RecordStage(ProjectionPhase phase, long started)
        => RecordStage(phase.ToString().ToLowerInvariant(), started);

    internal static void RecordStage(string stage, long started)
        => StageDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            new KeyValuePair<string, object?>("stage", stage));
}

public sealed record ProjectionCacheStatistics(long Hits, long Misses, long Evictions, int Count, int Capacity);
