# Security and trust boundaries

## Intended use

Projecta processes approved projection rules and permitted data models in a host application. The host owns user/tenant authorization, field visibility, model admission, secret management, process isolation and persistence. Registering a type or function is not per-user authorization. Never interpret a caller-supplied assembly-qualified type name or arbitrary C# snippet.

## Cooperative limits

Reader/output limits bound depth, nodes and characters. Engine limits bound steps, allocated tokens, recursion/ranges/string growth and elapsed time at checkpoints. Parser expression length/nesting are independently bounded. Cancellation is propagated. `$eval` is disabled by default and, when enabled, shares the original budget.

Existing CLR getters, custom converters, domain factories, callbacks, regex engine work and other runtime operations can execute between checkpoints. These controls are **not** a hard timeout, memory quota or malicious-code sandbox. The library does not spawn an isolated worker. For untrusted computation use an explicitly restricted OS/container process with resource/deadline controls and a minimal serializable data boundary.

## Data minimization and diagnostics

Generic sources exclude static/indexer/nonpublic getters, reject cycles/queryables/unknown runtime proxies and respect supported System.Text.Json metadata. Selective reading is an optimization for proven literal paths, not a security boundary or universal getter policy. `AllowedRootMembers` is an explicit source-exposure filter (and works on array items); a custom adapter can still execute its own getters before its resulting root object is filtered. Host-level authorization remains mandatory. Explicit source adapters are needed for models with hidden lazy loading or side effects.

Structured diagnostics omit raw inputs, expressions, exception messages and stacks. The programmatic comparison API reports JSON Pointers which can contain dynamically computed sensitive property names; do not log these pointers in multi-tenant environments. The CLI comparison always redacts these pointers. Rule IDs, field names, pointers and fingerprints are metadata, not guaranteed non-sensitive information. Direct throwing APIs may retain inner exceptions; do not log them indiscriminately. Telemetry installs no exporter and sends no network data. CLI stdout intentionally returns user-requested transformed data.

## Functions and contracts

Functions are registered by trusted application code and individually allowed per call. Variables cannot replace built-ins. Callback arguments/results are detached and bounded. Purity is a host assertion, and callback cancellation is cooperative. Do not give callback implementations unnecessary filesystem/network/credential access.

Schemas are compiled under explicit limits and accept only a declared profile. Unknown assertions, remote references and unsupported formats fail closed. Regex patterns have length/time bounds. There is no remote schema loader, code compilation or schema-based type activation.

## Documents, database and generated code

Patches/pipelines work on detached trees and do not persist or mutate EF entities. The original JSON remains unchanged after a failed operation. External callback side effects are not transactional. Authorize patch paths before application. Root removal is rejected.

The EF adapter must remain scoped to its DbContext, cannot be used concurrently, and never loads/attaches/saves. The existing scalar updater does not enforce aggregate invariants or roll back arbitrary setter effects. Use domain methods/factories for behavior-rich models.

Queryable translation rejects unsupported constructs and never silently switches to client enumeration. SQL translation still depends on the provider. Generated mappers run ordinary trusted CLR property access; no compiler plugin is a runtime sandbox. The analyzer requires a .NET 10 compiler host.

## Supply chain and release gates

Central dependency versions, warnings-as-errors and inherited notices are preserved. Exact declared optional packages and full configured Linux suites have been executed using a supplied offline dependency closure. Online NuGet vulnerability advisories were not checked; this is not a supply-chain clearance. Windows/macOS CI, application-specific hard isolation and operational review remain release gates. Retain an immutable known-good package and application tests before replacing a deployed version.
