# Changelog

This changelog tracks Projecta releases. Changes developed during the preparation of a release are summarized under that release rather than recorded as separate published versions.

## 0.2.0 - 2026-10-12

### Standalone library and integration

- Expanded the independently maintained .NET 10 / C# 14 Projecta library with centrally managed NuGet versions, shared build properties and additional projection and document-transformation modules.
- Preserved the public Projecta package, assembly and namespace identities, typed projection APIs, CLR/JSON source adapters, immutable compiled plans, destination type allowlists and extension points.
- Provided 11 packable modules for the expression engine, runtime mapping, System.Text.Json integration, contracts, streaming, documents, restricted LINQ projection, source generation, local tooling and optional Newtonsoft.Json / EF Core adapters.
- Standardized package authorship as `Projecta contributors` and maintained MIT distribution metadata.
- Renamed the internal historical-language regression suite to `Projecta.Compatibility.Tests` without changing its test assertions.

### Rules, authoring and execution

- Added immutable per-call execution contexts, trusted host-function catalogs with explicit execution allowlists, structured diagnostics and optional input/output contract validation.
- Added conservative type-aware preflight, deterministic plan explanation, bounded staged rule registry with immutable versions and compare-and-swap activation/rollback, and bounded metadata-focused rule comparison.
- Added opt-in root-member exposure policies, static nested POCO path selection with conservative fallback, and configurable null-object-property omission.
- Added stage-level timing metrics and configuration-based replay identities with explicit provenance limitations.
- Improved source-reading and evaluation bounds, cancellation behavior, direct UTF-8 writing, numeric fidelity and cold compilation outside the LRU cache lock.

### Additional modules and tooling

- Added ordered batch and incremental JSONL processing; detached JSON Pointer, JSON Patch, merge-patch and pipeline operations.
- Added restricted scalar LINQ projections and compile-time static mapping generation.
- Added local CLI commands for validation, preview, explanation, comparison, JSONL, document operations, conformance tests and benchmarking.
- Added maintained semantic fixtures, adversarial/fuzz tests, provider integration tests, package-consumer examples and Linux/Windows/macOS verification workflows.
- Added a source inventory validator and reproducible package/verification scripts. Structural validation is separate from build, test, provider and runtime qualification.

### Compatibility and known boundaries

- New policies, contracts, functions and optimizer profiles are opt-in. Defaults retain the documented projection behavior.
- Source-member allowlists do not replace authorization; host callbacks execute trusted code; rule registries are in-memory unless the host persists their state.
- Full JSONata language equivalence, Native AOT/trimming, production performance and provider-specific SQL require independent qualification. See [validation](docs/VALIDATION.md), [feature boundaries](docs/FEATURES.md) and [adoption guidance](docs/MIGRATION.md).

## 0.1.0

First standalone release of Projecta for .NET 10 and C# 14, providing JSONata expression evaluation under Projecta namespaces alongside reusable object-projection APIs.

### Initial capabilities

- Added a bounded CLR source reader supporting dictionaries and enumerables, cached property access and explicit handling of cycles, runtime proxies and queryables.
- Added declarative projection definitions, immutable compiled plans and a bounded LRU plan cache.
- Added typed `System.Text.Json` destinations and an explicit runtime type registry.
- Added optional Newtonsoft.Json and Entity Framework Core adapters, distributed separately from the engine and `System.Text.Json` integration.
- Added cancellation, time, step, token, range, parser and regular-expression execution bounds.

### Historical validation limitations

- An earlier development snapshot recorded **110 core tests passed** with zero failures, including a differential corpus of 200 JSON objects. These results do **not** establish that the final 0.1.0 snapshot was tested.
- The Newtonsoft.Json adapter completed **12 smoke tests** using an SDK-bundled Newtonsoft.Json 13.0.3 DLL. Restore and testing with the declared 13.0.4 package were not verified.
- The final snapshot was not rebuilt or retested after a later test-source repair; that repair received static verification only.
- Entity Framework Core 10.0.12 restore/build/tests, integration and historical regression suites, and a vulnerability audit were not executed. The release was **not** qualified as production-ready.
