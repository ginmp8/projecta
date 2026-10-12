# Performance measurements

Projecta includes a repeatable synthetic benchmark protocol, `projecta-batch-means-v1`: 200 warmup operations followed by 9 rounds of 2,000 operations per scenario. It records round samples, allocated bytes per operation and garbage-collection counts.

Run the benchmark against the current checkout and intended runtime:

```sh
dotnet run --project src/Projecta.Tooling -c Release -- benchmark --iterations 2000 --warmup 200 --rounds 9
```

The included scenarios cover full source normalization, statically selected-property normalization, evaluation to native JSON tokens, evaluation to typed DTOs, and cold projector construction plus rule compilation. These scenarios do not all perform equivalent work. In particular, selected normalization reads a different member set from full normalization, and evaluation scenarios include normalization and destination materialization.

`P95BatchMeanNanoseconds` is the 95th percentile of **round-average costs**, not the p95 latency of individual requests. This protocol does not establish statistical significance, production load or soak reliability, cross-engine ranking or equivalent performance across hardware. GC counts apply to measured rounds; allocation measurement is for the executing thread. Source getters and host callbacks that do asynchronous work are not represented.

When reporting results, retain raw samples and record the exact revision, runtime/SDK, OS, architecture, hardware, data shape, expression mix, cold/warm ratios and concurrency. Repeat on the deployment environment before making capacity decisions. The standalone sample in `benchmarks/Projecta.Benchmarks` can assist exploration but is not production qualification.
