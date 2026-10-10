using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using Projecta.Mapping;
using Projecta.Objects;

namespace Projecta.Tooling;

public sealed record Measurement(string Name, int Iterations, int Rounds, double MedianBatchMeanNanoseconds,
    double P95BatchMeanNanoseconds, double MedianAllocatedBytesPerOperation, IReadOnlyList<double> SamplesNanoseconds,
    IReadOnlyList<double> SamplesBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections);
public sealed record BenchmarkReport(string Protocol, string Runtime, string OperatingSystem, string Architecture, int ProcessorCount,
    bool ServerGc, long StopwatchFrequency, string EngineVersion, int Warmup, IReadOnlyList<Measurement> Scenarios);

/// <summary>Dependency-free synthetic measurement protocol. Percentiles are of batch means, not request tail latency.</summary>
public static class ProjectionBenchmarks
{
    private sealed record Target(int Id, string Name, int Total);
    public static BenchmarkReport Run(int iterations = 2000, int warmup = 200, int rounds = 9, CancellationToken cancellationToken = default)
    {
        if (iterations is < 1 or > 100_000 || warmup is < 0 or > 10_000 || rounds is < 3 or > 50) throw new ArgumentOutOfRangeException(nameof(iterations));
        var source = new { Id = 42, Name = "sample", Values = new[] { 1, 2, 3, 4, 5 }, Unused = new string('x', 4096) };
        var reader = new ObjectReader(); var selected = new HashSet<string>(new[] { "Id", "Name" }, StringComparer.Ordinal);
        var projector = new ObjectProjector(); var rule = new ProjectionDefinition { Expression = "{'Id':Id,'Name':Name,'Total':$sum(Values)}" };
        var plan = projector.Compile(rule);
        var scenarios = new (string Name, Func<object?> Action)[]
        {
            ("normalize-full", () => reader.Read(source)), ("normalize-selected", () => reader.ReadSelected(source, selected)),
            ("evaluate-to-native", () => projector.ProjectToToken(source, plan)),
            ("evaluate-to-dto", () => projector.Project<Target>(source, plan)),
            ("cold-projector-and-compile", () => new ObjectProjector().Compile(rule))
        };
        var measurements = new List<Measurement>();
        foreach (var scenario in scenarios)
        {
            object? last = null;
            for (int i = 0; i < warmup; i++) { cancellationToken.ThrowIfCancellationRequested(); last = scenario.Action(); }
            var samples = new List<double>(); var bytes = new List<double>();
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            for (int round = 0; round < rounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++)
                { if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested(); last = scenario.Action(); }
                long elapsed = Stopwatch.GetTimestamp() - start;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
                samples.Add(elapsed * 1_000_000_000d / Stopwatch.Frequency / iterations);
                bytes.Add(allocated / (double)iterations);
            }
            GC.KeepAlive(last);
            measurements.Add(new(scenario.Name, iterations, rounds, Percentile(samples, .5), Percentile(samples, .95), Percentile(bytes, .5),
                samples.AsReadOnly(), bytes.AsReadOnly(), GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2));
        }
        return new("projecta-batch-means-v1", Environment.Version.ToString(), RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount, GCSettings.IsServerGC, Stopwatch.Frequency, typeof(ObjectProjector).Assembly.GetName().Version?.ToString() ?? "unknown", warmup, measurements.AsReadOnly());
    }
    private static double Percentile(List<double> values, double percentile)
    { var sorted = values.OrderBy(x => x).ToArray(); return sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)]; }
}
