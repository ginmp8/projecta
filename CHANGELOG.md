# Changelog

## 0.2.0

- Added conservative type-aware preflight and deterministic, redacted plan explanations.
- Added bounded rule registry with immutable staged versions, atomic compare-and-swap promotion and rollback.
- Added finite, bounded rule comparison with both rule identities, versions and metadata-only differences.
- Added opt-in null-object-property omission and unified root-member exposure filtering for POCO, JSON DOM, dictionary and array-element inputs.
- Extended conservative static POCO reading from root members to nested member paths and enumerable items.
- Added stage-level metrics and execution-profile replay identifiers covering binding and output policies.
- Added CLI `explain`/`compare` with automatic pointer redaction, and extra regression tests.
- Froze caller-owned exposure collections at projector construction to prevent configuration drift.
- Restored reproducible in-repository test datasets and aligned Newtonsoft date-oracle expectations to the documented ISO default while retaining explicit custom-format coverage.
- Verified the full offline dependency closure and Linux optional suites; production, Native AOT and non-Linux gates remain separate.

- Added immutable execution contexts, allowlisted host functions and structured diagnostics.
- Added optional structural input/output contracts, ordered batch/JSONL APIs and detached document transformations.
- Added restricted scalar LINQ projection and compile-time mapper generation.
- Added local validation/preview/document/suite/benchmark tooling and reproducible package-consumer verification.
- Added conservative root-member selection, direct UTF-8 destination materialization and numeric fidelity fixes.
- Moved parsing outside the LRU lock while retaining an independent cold-compilation bound.
- Added evaluation telemetry without payloads or an exporter.
- Expanded regression, semantic, fuzz, generator and provider-test sources; separated full and package-free validation.
- Retained .NET 10/C# 14, central dependency management, existing projection APIs and optional adapters.
- Validation is bounded to the recorded Linux environment; Windows/macOS, production workloads, full-language conformance and Native AOT remain unverified.

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
- Validation caveats: full external dependency restore, EF runtime tests, upstream NUnit conformance and vulnerability audits were not executed.
- Post-distribution repair: test-source namespace and overload ambiguities were fixed without changing public projection contracts; a fresh build/test after that repair was not executed.
