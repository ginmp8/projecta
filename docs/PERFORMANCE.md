# Performance measurements

The included protocol is `projecta-batch-means-v1`: 200 warmup operations, 9 rounds of 2,000 operations per scenario, with raw samples, allocated bytes per operation and GC counts. Run:

```sh
dotnet run --project src/Projecta.Tooling -c Release -- benchmark --iterations 2000 --warmup 200 --rounds 9
```

Observed on .NET runtime 10.0.10, Debian GNU/Linux 13 (trixie), X64, 4 reported processors. The sandbox is not dedicated benchmark hardware. Runtime JIT, scheduling, GC and contention affect observations. The report describes a synthetic source with a few scalar/array properties and an unused 4 KiB string.

| Scenario | Median batch mean, microseconds/op | Median allocated bytes/op |
|---|---:|---:|
| normalize-full | 3.355 | 1840.0 |
| normalize-selected | 1.421 | 872.0 |
| evaluate-to-native | 14.821 | 6618.6 |
| evaluate-to-dto | 22.165 | 7242.6 |
| cold-projector-and-compile | 11.418 | 6793.0 |

The two normalization scenarios intentionally read different member sets; they illustrate the cost of the configured selective path, not semantic interchangeability for arbitrary rules. The evaluation scenarios also include source normalization; the native scenario returns detached tokens, and the DTO scenario materializes a typed result. The cold scenario includes constructing a projector and compiling one expression.

`P95BatchMeanNanoseconds` is the 95th percentile of **round-average costs**, not the p95 latency of individual requests. The protocol does not perform statistical significance analysis, production load/soak tests, arbitrary hardware normalization or cross-engine comparisons. GC counts cover measured rounds; allocation measurement is for the executing thread. Source getters/callbacks that perform asynchronous work are not represented.

These numbers are observations, not performance promises. Raw results are included in `artifacts/validation/benchmark.json`. Benchmark the actual source adapters, expression mix, cold/warm ratios and concurrency before making capacity decisions. The legacy warm sample remains runnable under `benchmarks/Projecta.Benchmarks`; it is not the qualification protocol.
