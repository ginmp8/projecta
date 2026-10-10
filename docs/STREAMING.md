# Batches and JSONL

`ProjectMany<T>`, `ProjectManyAsync<T>` and `ProjectJsonLinesAsync<T>` are sequential pull-based APIs over a reused compiled plan. They preserve input order and do not share a DbContext across parallel tasks. Each yielded `ProjectionItemResult<T>` has a zero-based processed-record index and a `ProjectionResult<T>`. JSONL also exposes the one-based physical line number.

```csharp
using Projecta.Mapping;
using Projecta.Streaming;

var projector = new ObjectProjector();
var plan = projector.Compile(new ProjectionDefinition { Expression = "{'Name':name}" });
await using var stream = File.OpenRead("input.jsonl");
await foreach (var row in projector.ProjectJsonLinesAsync<Person>(stream, plan,
    options: new ProjectionBatchOptions { ErrorMode = BatchErrorMode.Continue },
    cancellationToken: cancellationToken))
{
    if (row.Result.Success) Consume(row.Result.Value);
    else RecordFailure(row.Index, row.LineNumber, row.Result.Diagnostics);
}
```

The example assumes the application supplies `Person`, `Consume`, `RecordFailure` and its cancellation token.

The caller owns the input stream/enumerator lifetime. There is no global list of input/output records. A consumer can still accumulate results, so application memory behavior remains its responsibility. JSONL records are individually buffered up to `MaxLineBytes` before parsing; an entire JSON array or an unbounded single document is not streamed through the evaluator.

Defaults: continue after record-level projection/parse failures, at most 1,000,000 processed records, 1,000,000 bytes per line, JSON depth 64, blank lines skipped. CRLF and a final line without a newline are accepted. Malformed JSON yields a record failure in Continue mode. Stop mode throws `ProjectionBatchException` with the failing index and sanitized diagnostics.

Cancellation, record-count exhaustion, oversize lines and I/O failures terminate the enumeration: they are not converted into ordinary bad-record results. Skipped blank lines do not count as processed records. Enumeration fetches the next source item before detecting a record limit; avoid sources whose `MoveNext` has irreversible side effects. No retries or implicit transactions occur.
