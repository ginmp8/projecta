using System.Diagnostics;
using System.Diagnostics.Metrics;

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
}

public sealed record ProjectionCacheStatistics(long Hits, long Misses, long Evictions, int Count, int Capacity);
