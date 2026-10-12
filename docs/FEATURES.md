# Feature and qualification matrix

Version 0.4.0-alpha.1. "Implemented" refers to the declared bounded contract, not every possible extension of that capability. "Blocked" means code/configuration exists but the required validation did not execute.

| ID | Capability delivered | Qualification / boundary |
|---|---|---|
| F01 | Semantic suite runner, fixture identity, explicit outcomes and 490 included cases | Finite suite passed; full-language conformance and independent differential corpus remain open |
| F02 | Full/core solution files, executable suites, NUnit invocation, three-OS CI and package scripts | Full Linux build and configured verification executed; Windows/macOS jobs remain unrun |
| F03 | Deterministic parser/evaluator fuzzing, depth/range/cancel budgets and adversarial regressions | Bounded tests passed; not a malicious-code sandbox or proof of bug absence |
| F04 | Structured compile/execute/contract diagnostics with rule, phase, field and position | Executed; unknown positions/types omitted, no fabricated attribution |
| F05 | Detached immutable per-call variable contexts | Executed, including parallel calls and failure isolation |
| F06 | Immutable host function catalog, explicit per-call allowlist, arity and purity opt-in | Executed; trusted host callbacks are not isolated or proven pure |
| F07 | Optional input/output validation with projecta-contract-v1 | Executed; unsupported/remote assertions rejected, not full JSON Schema |
| F08 | Sync/async batches, bounded per-record JSONL, ordering and error policies | Executed; no full-document streaming or implicit parallelism |
| F09 | Opt-in static path analysis and selective nested POCO normalization | Executed; nested POCO paths and list elements supported; opaque adapters/JSON DOM remain full and dynamic analysis falls back |
| F10 | Direct bounded UTF-8 materialization, JsonTypeInfo destination path and numeric fidelity fixes | Executed; no global AOT or arbitrary-precision arithmetic claim |
| F11 | Repeatable synthetic benchmark protocol, raw samples, allocation/GC metadata | Executed; no cross-engine ranking or production latency inference |
| F12 | scalar-select-v1 LINQ projection and SQLite verification source | In-memory construction/execution passed; SQLite provider SQL test executed |
| F13 | static-scalar-v1 incremental source generator and compiler/execution tests | Generated mapper executed; runtime rules remain interpreted, Native AOT publish unrun |
| F14 | Versioned copy/rename/default/omit/ensureArray/project pipelines | Executed; atomic JSON results, not callback side-effect transactions |
| F15 | Local CLI with validation, preview, batch, document, suite and benchmark commands | Executed; a graphical editor/playground is not included |
| F16 | JSON Pointer, six patch operations and merge patches | Executed within a document-preserving profile; root removal is explicitly unsupported |

The original source/target adapters, typed target registry, expression engine, limits and sample remain in the solution. An EF identity-index optimization was not added without provider evidence; existing tracked-entry lookup behavior is retained. Unrestricted SQL translation, runtime C# compilation, arbitrary schema resolvers, domain-aggregate mutation and automatic persistence are not product features.

## Authoring, lifecycle, data-minimization and operational refinements (0.4.0-alpha.1)

| ID | Capability delivered | Qualification / boundary |
|---|---|---|
| F17 | Type-aware conservative preflight with source/target metadata and required-field checks | Explicit `fields` output only; dynamic expressions emit indeterminate warnings, never unproven acceptance |
| F18 | Plan explanation with literal JSON Pointer paths and explicit full-read fallback reason | No evaluation or arbitrary getter invocation; not a complete static type checker |
| F19 | Thread-safe bounded staged rule registry with version immutability, compare-and-swap activation and rollback | In-memory only; caller manages persistence, authorization and deployment |
| F20 | Bounded output comparison: changes, omissions, types, both rule IDs/versions and failure metadata | Same in-process source is evaluated twice; side-effecting getters/callbacks are caller concerns |
| F21 | Optional null-object-property omission and root-member allowlist | Defaults preserve behavior; root allowlist is a data-exposure filter, not a complete field-authorization service |
| F22 | Static nested POCO path pruning plus enumerable-element propagation | Dynamic constructs, adapters and JSON containers retain complete normalization |
| F23 | Low-cardinality per-stage duration metrics for compile/context/source/validation/evaluate/materialize | No exporter; excludes unrelated host side effects and is not a production benchmark |
| F24 | Configuration fingerprint including read/binding/output policies, execution budgets and context binding names | Source/binding values are not embedded; replay requires caller-provided provenance IDs and external state stability |
| F25 | `explain` and `compare` CLI commands | CLI comparison redacts output-property paths by default to avoid leaking data-dependent keys |

No new runtime NuGet dependency was required for these refinements. See [authoring](AUTHORING.md) for exact contracts and examples.
