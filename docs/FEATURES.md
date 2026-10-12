# Feature and qualification matrix

Version 0.2.0. The matrix documents implemented source-level capabilities and known limitations. The presence of test sources is not a passing test result; use [verification](VALIDATION.md) to obtain execution evidence for a specific revision and environment. Listed features are not a claim of complete language conformance or production qualification.

| ID | Capability delivered | Qualification / boundary |
|---|---|---|
| F01 | Semantic suite runner, fixture identity, explicit outcomes and 490 included cases | Maintained semantic fixtures are included; full-language conformance and an independently validated differential corpus are not established |
| F02 | Full/core solution files, executable suites, NUnit invocation, three-OS CI and package scripts | CI targets Linux, Windows and macOS; check the workflow result for the intended revision |
| F03 | Deterministic parser/evaluator fuzzing, depth/range/cancel budgets and adversarial regressions | Adversarial tests and bounded evaluation are present; they do not create a malicious-code sandbox or prove bug absence |
| F04 | Structured compile/execute/contract diagnostics with rule, phase, field and position | Unknown positions/types are omitted rather than inferred |
| F05 | Detached immutable per-call variable contexts | Context detachment and isolation are implemented; validate parallel-call behavior in the target runtime |
| F06 | Immutable host function catalog, explicit per-call allowlist, arity and purity opt-in | Trusted host callbacks are not isolated or proven pure |
| F07 | Optional input/output validation with projecta-contract-v1 | Unsupported and remote assertions are rejected; this is not complete JSON Schema support |
| F08 | Sync/async batches, bounded per-record JSONL, ordering and error policies | Does not provide full-document streaming or implicit parallelism |
| F09 | Opt-in static path analysis and selective nested POCO normalization | Nested POCO paths and list elements are supported; opaque adapters/JSON DOM remain full and dynamic analysis falls back |
| F10 | Direct bounded UTF-8 materialization, JsonTypeInfo destination path and numeric fidelity fixes | No global AOT or arbitrary-precision arithmetic guarantee |
| F11 | Repeatable synthetic benchmark protocol, raw samples, allocation/GC metadata | No cross-engine ranking or production latency inference without measured evidence |
| F12 | scalar-select-v1 LINQ projection and SQLite verification source | In-memory and SQLite test sources are provided; validate generated SQL for the target provider |
| F13 | static-scalar-v1 incremental source generator and compiler/execution tests | Runtime rules remain interpreted; Native AOT publishing requires separate validation |
| F14 | Versioned copy/rename/default/omit/ensureArray/project pipelines | Atomic detached JSON results; external callback side effects are not transactional |
| F15 | Local CLI with validation, preview, batch, document, suite and benchmark commands | A graphical editor/playground is not included |
| F16 | JSON Pointer, six patch operations and merge patches | Root removal is explicitly unsupported in the document-preserving profile |

The original source/target adapters, typed target registry, expression engine, limits and sample remain in the solution. An EF identity-index optimization was not added without provider evidence; existing tracked-entry lookup behavior is retained. Unrestricted SQL translation, runtime C# compilation, arbitrary schema resolvers, domain-aggregate mutation and automatic persistence are not product features.

## Authoring, lifecycle, data-minimization and operational refinements

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
