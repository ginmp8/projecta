using System;
using System.Diagnostics;
using System.Text.Json;
using Projecta.Mapping;
using Projecta.Objects;

var source = new { Id = 42, Name = "sample", Values = new[] { 1, 2, 3, 4, 5 } };
var reader = new ObjectReader();
var projector = new ObjectProjector();
var plan = projector.Compile(new() { Expression = "{'Id':Id,'Name':Name,'Total':$sum(Values)}" });
void Run(string scenario, Func<object?> action)
{
    for (int i = 0; i < 1000; ++i) _ = action();
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long bytes = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew(); object? last = null;
    const int count = 10000;
    for (int i = 0; i < count; ++i) last = action();
    timer.Stop();
    Console.WriteLine(JsonSerializer.Serialize(new { scenario, iterations = count,
        microsecondsPerOperation = timer.Elapsed.TotalMicroseconds / count,
        allocatedBytesPerOperation = (GC.GetAllocatedBytesForCurrentThread() - bytes) / (double)count }));
    GC.KeepAlive(last);
}
Console.WriteLine("Synthetic warm microbenchmark; not an end-to-end performance guarantee. Runtime " + Environment.Version);
Run("bounded-clr-reader", () => reader.Read(source));
Run("compiled-projection-to-token", () => projector.ProjectToToken(source, plan));
Run("compiled-projection-to-dto", () => projector.Project<Summary>(source, plan));
public sealed record Summary(int Id, string Name, int Total);
