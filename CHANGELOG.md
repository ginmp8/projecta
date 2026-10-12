# Changelog

## 0.1.0

- New standalone Projecta identity and namespaces; native JSONata engine incorporated as source.
- .NET 10/C# 14 throughout; central build/package policy and new NuGet package boundaries.
- Bounded CLR reader, weak cached/compiled property accessors, indexer/static exclusion, dictionary/enumerable support, cycle/proxy/queryable controls and scalar fixes.
- Declarative fields/full-expression mappings, immutable compiled plans, bounded LRU cache, typed System.Text.Json destinations, explicit runtime type registry and extension points.
- Optional recursive Newtonsoft token adapters; round-trip offset/date handling.
- Optional tracked EF Core source snapshots, explicit loaded-navigation policy and guarded tracked scalar updater.
- Cooperative evaluation/cancellation/time/step/token/range/string controls, parser bounds, regex timeout, isolated default environments and opt-in budget-inheriting dynamic evaluation.
- Count-based range traversal at Int64 boundaries; single-allocation string padding instead of repeated concatenation.
- Package-free verification executable, optional integration tests, retained upstream test sources, runnable example, benchmarks, CI and evidence reports.

Validation caveats are part of this release: full external dependency restore, EF runtime tests, upstream NUnit conformance and vulnerability audits were not executed. This is not a production-readiness approval.
