# Changelog

## 0.4.0-alpha.1

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


## 0.3.0-alpha.1

- Added immutable execution contexts, allowlisted host functions and structured diagnostics.
- Added optional structural input/output contracts, ordered batch/JSONL APIs and detached document transformations.
- Added restricted scalar LINQ projection and compile-time mapper generation.
- Added local validation/preview/document/suite/benchmark tooling and reproducible package-consumer verification.
- Added conservative root-member selection, direct UTF-8 destination materialization and numeric fidelity fixes.
- Moved parsing outside the LRU lock while retaining an independent cold-compilation bound.
- Added evaluation telemetry without payloads or an exporter.
- Expanded regression, semantic, fuzz, generator and provider-test sources; separated full and package-free validation.
- Retained .NET 10/C# 14, central dependency management, existing projection APIs and optional adapters.
- Qualification remains alpha: exact optional dependencies, provider SQL, other OS execution, full-language conformance and Native AOT publishing need additional evidence.

## 0.2.0-alpha.1

- Established standalone object projection, compiled rules, bounded caching, typed targets and destination registry.
- Added bounded CLR reading, System.Text.Json and optional Newtonsoft/EF adapters.
- Added parser/evaluation safeguards, cancellation, source tests, sample and build scripts.
- Repaired test-source namespace and overload ambiguities without changing public projection contracts.
