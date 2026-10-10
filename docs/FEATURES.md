# Feature and qualification matrix

Version 0.3.0-alpha.1. "Implemented" refers to the declared bounded contract, not every possible extension of that capability. "Blocked" means code/configuration exists but the required validation did not execute.

| ID | Capability delivered | Qualification / boundary |
|---|---|---|
| F01 | Semantic suite runner, fixture identity, explicit outcomes and 490 included cases | Finite suite passed; full-language conformance and independent differential corpus remain open |
| F02 | Full/core solution files, executable suites, NUnit invocation, three-OS CI and package scripts | Package-free Linux path passed; exact optional packages and other OS jobs blocked/unrun |
| F03 | Deterministic parser/evaluator fuzzing, depth/range/cancel budgets and adversarial regressions | Bounded tests passed; not a malicious-code sandbox or proof of bug absence |
| F04 | Structured compile/execute/contract diagnostics with rule, phase, field and position | Executed; unknown positions/types omitted, no fabricated attribution |
| F05 | Detached immutable per-call variable contexts | Executed, including parallel calls and failure isolation |
| F06 | Immutable host function catalog, explicit per-call allowlist, arity and purity opt-in | Executed; trusted host callbacks are not isolated or proven pure |
| F07 | Optional input/output validation with projecta-contract-v1 | Executed; unsupported/remote assertions rejected, not full JSON Schema |
| F08 | Sync/async batches, bounded per-record JSONL, ordering and error policies | Executed; no full-document streaming or implicit parallelism |
| F09 | Opt-in static root-member analysis and selective POCO normalization | Executed; nested pruning/adapters are not generalized, unsafe analysis falls back |
| F10 | Direct bounded UTF-8 materialization, JsonTypeInfo destination path and numeric fidelity fixes | Executed; no global AOT or arbitrary-precision arithmetic claim |
| F11 | Repeatable synthetic benchmark protocol, raw samples, allocation/GC metadata | Executed; no cross-engine ranking or production latency inference |
| F12 | scalar-select-v1 LINQ projection and SQLite verification source | In-memory construction/execution passed; real provider SQL test blocked |
| F13 | static-scalar-v1 incremental source generator and compiler/execution tests | Generated mapper executed; runtime rules remain interpreted, Native AOT publish unrun |
| F14 | Versioned copy/rename/default/omit/ensureArray/project pipelines | Executed; atomic JSON results, not callback side-effect transactions |
| F15 | Local CLI with validation, preview, batch, document, suite and benchmark commands | Executed; a graphical editor/playground is not included |
| F16 | JSON Pointer, six patch operations and merge patches | Executed within a document-preserving profile; root removal is explicitly unsupported |

The original source/target adapters, typed target registry, expression engine, limits and sample remain in the solution. An EF identity-index optimization was not added without provider evidence; existing tracked-entry lookup behavior is retained. Unrestricted SQL translation, runtime C# compilation, arbitrary schema resolvers, domain-aggregate mutation and automatic persistence are not product features.
