# Performance evidence

Executed on Linux x64 with SDK 10.0.302 / runtime 10.0.10. The comparison builds the unchanged supplied engine source on the same .NET 10 runtime; this isolates source changes rather than mixing runtime versions.

Three warm rounds, medians below. DTO workload: one object containing an ID, a string and 20 nested objects (each with ID/name/decimal). 200 warmups and 10,000 measured calls per reader round. Padding workload: `pad("x", 20000, "ab")`, 200 warmups and 200 measured calls per round. Source normalization outputs were checked for equality before timing.

| Scenario | Microseconds / operation | Allocated bytes / operation |
|---|---:|---:|
| projecta-object-reader | 4.847 | 10,619 |
| projecta-pad-20000 | 23.992 | 40,024 |
| upstream-object-reader | 11.219 | 10,768 |
| upstream-pad-20000 | 8602.449 | 200,320,024 |

These are synthetic microbenchmarks measured with Stopwatch and per-thread allocation counters, not BenchmarkDotNet confidence intervals, load testing or service-level latency. Order, JIT tiering and container scheduling may affect results. No claim is made for all DTO sizes, all expressions, EF workloads or untrusted input.

The direct reader's main change is caching eligible property accessors instead of discovering/reflection-invoking them on every object; budget/cycle validation adds work. String padding now allocates once and fills the result, avoiding the original quadratic concatenation pattern.

The included `benchmarks/Projecta.Benchmarks` measures the current reader, compiled projection to native JSON and compiled projection to DTO. It does not require or download the original package. The original-source comparison harness is recorded in `docs/validation/comparison-harness.cs`; its project paths were sandbox-local and were not injected into product code.

Raw data: `validation/performance-comparison.json` and `validation/projecta-benchmark.log`.
