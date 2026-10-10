using System.Text;
using Projecta.Mapping;
using Projecta.Streaming;

internal static class StreamingTests
{
    internal static void Register(Action<string, Action> test)
    {
        var projector = new ObjectProjector(); var plan = projector.Compile(new() { Expression = "a + 1" });
        test("batch: ordered results", () =>
        {
            var rows = projector.ProjectMany<int>(new object[] { new { a = 1 }, new { a = 4 } }, plan).ToArray();
            Check.Equal(0L, rows[0].Index); Check.Equal(5, rows[1].Result.Value);
        });
        test("batch: input is lazy", () =>
        {
            int count = 0;
            IEnumerable<object> Input() { for (int i = 0; i < 4; i++) { count++; yield return new { a = i }; } }
            var rows = projector.ProjectMany<int>(Input(), plan); Check.Equal(0, count);
            using var iterator = rows.GetEnumerator(); Check.True(iterator.MoveNext()); Check.Equal(1, count);
        });
        test("batch: continue with per-record diagnostics", () =>
        {
            var rows = projector.ProjectMany<int>(new object[] { new { a = 1 }, new { a = "bad" }, new { a = 3 } }, plan).ToArray();
            Check.Equal(3, rows.Length); Check.True(!rows[1].Result.Success); Check.Equal(4, rows[2].Result.Value);
        });
        test("batch: stop mode", () => Check.Throws<ProjectionBatchException>(() => projector.ProjectMany<int>(
            new object[] { new { a = "bad" } }, plan, options: new() { ErrorMode = BatchErrorMode.Stop }).ToArray()));
        test("batch: record limit", () => Check.Throws<Projecta.Execution.ExecutionLimitException>(() => projector.ProjectMany<int>(
            new object[] { new { a = 1 }, new { a = 2 } }, plan, options: new() { MaxRecords = 1 }).ToArray()));
        test("batch: cancellation", () =>
        {
            using var c = new CancellationTokenSource(); c.Cancel();
            Check.Throws<OperationCanceledException>(() => projector.ProjectMany<int>(new object[] { new { a = 1 } }, plan, cancellationToken: c.Token).ToArray());
        });
        async Task<List<ProjectionItemResult<T>>> Collect<T>(IAsyncEnumerable<ProjectionItemResult<T>> values)
        { var result = new List<ProjectionItemResult<T>>(); await foreach (var value in values) result.Add(value); return result; }
        test("batch: async input", () =>
        {
            async IAsyncEnumerable<object?> Source() { yield return new { a = 5 }; await Task.Yield(); yield return new { a = 7 }; }
            var values = Collect(projector.ProjectManyAsync<int>(Source(), plan)).GetAwaiter().GetResult(); Check.Equal(8, values[1].Result.Value);
        });
        test("jsonl: CRLF, final line, blanks and errors", () =>
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":1}\r\n\ninvalid\n{\"a\":3}"));
            var rows = Collect(projector.ProjectJsonLinesAsync<int>(source, plan)).GetAwaiter().GetResult();
            Check.Equal(3, rows.Count); Check.Equal(2, rows[0].Result.Value); Check.Equal(3L, rows[1].LineNumber);
            Check.Equal(ProjectionPhase.Stream, rows[1].Result.Diagnostics[0].Phase); Check.Equal(4, rows[2].Result.Value); Check.True(source.CanRead);
        });
        test("jsonl: line bound before parsing", () =>
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 100)));
            Check.Throws<Projecta.Execution.ExecutionLimitException>(() => Collect(projector.ProjectJsonLinesAsync<int>(source, plan,
                options: new() { MaxLineBytes = 32 })).GetAwaiter().GetResult());
        });
        test("jsonl: empty input", () =>
        {
            using var source = new MemoryStream(); Check.Equal(0, Collect(projector.ProjectJsonLinesAsync<int>(source, plan)).GetAwaiter().GetResult().Count);
        });
        test("jsonl: cancellation", () =>
        {
            using var source = new MemoryStream(new byte[10]); using var c = new CancellationTokenSource(); c.Cancel();
            Check.Throws<OperationCanceledException>(() => Collect(projector.ProjectJsonLinesAsync<int>(source, plan, cancellationToken: c.Token)).GetAwaiter().GetResult());
        });
        test("jsonl: stop mode", () =>
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes("invalid\n"));
            Check.Throws<ProjectionBatchException>(() => Collect(projector.ProjectJsonLinesAsync<int>(source, plan,
                options: new() { ErrorMode = BatchErrorMode.Stop })).GetAwaiter().GetResult());
        });
    }
}
