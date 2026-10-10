# Security boundaries

## Implemented controls

- Bounded source graphs: maximum recursion depth, total nodes and characters; cycle detection using reference identity; checked enumeration and cancellation.
- Public instance readable properties only; indexers/static/non-public getters excluded. Weak type caches avoid permanently rooting dynamically unloadable model assemblies.
- Unregistered dynamic proxies and queryables rejected. EF adapter handles mapped/tracked sources explicitly and temporarily disables lazy loading/change detection.
- Native JSON parser has character, string, depth and token caps in sync/async paths. Strict mode rejects `undefined`.
- Expression length and parser/optimizer recursion are bounded before evaluation.
- Evaluation budgets cover evaluator checkpoints, many builtin loops, native-token creation and range/string growth. Range arithmetic uses decimal/count-based traversal to avoid long overflow.
- Regex literals have finite match timeouts. `$eval` is denied by default; opt-in calls inherit environment/budget.
- Default environments cannot be globally mutated by one call to contaminate another.
- Target aliases require code-side registration. Unknown destination properties fail under the default .NET serializer. No JSON-driven reflection type loading or entity attachment occurs.

## Explicit non-guarantees

1. CLR objects and extension implementations are **trusted executable code**. Getters, constructors, factories, converters, enumerators and host-bound functions can perform arbitrary side effects. This library cannot preempt them. Configure `IncludeProperty` or source adapters before reading sensitive types.
2. `MaxAllocatedTokens` counts native DOM allocations, not every byte allocated by .NET. Some BCL methods can allocate before a checkpoint. Timeouts and cancellation are cooperative, not hard process deadlines.
3. Regex timeouts do not make hostile regex patterns safe; parsing/compilation and runtime work remain potential resource risks. Approve expressions or isolate them externally.
4. Per-DbContext adapters are not thread-safe. Do not register an EF-backed projector as a singleton or use it concurrently. Core-only plans may be reused concurrently; do not mutate caller-provided EvaluationEnvironment bindings during evaluation.
5. Projection is not authorization, tenant filtering, redaction or a SQL query planner. Select/load only authorized data and approve rule changes before execution. Errors may contain inner exceptions from trusted third-party code; applications must apply their own sensitive-log policy.
6. Generic domain target construction is not invariant enforcement. A tracked scalar update can be partly applied in memory if a setter throws. Persist only after domain validation, and use domain methods when required.
7. Retained native low-level APIs and explicitly supplied ASTs/functions are developer APIs, not safe untrusted object channels. Use `ObjectProjector` with bounded source normalization for configurable data transformations.

## Verification status

See VALIDATION.md. No current NuGet vulnerability audit, dependency integrity review for all external packages, pentest, untrusted-process isolation test or complete EF provider test suite was executed. Do not label this alpha production-approved.

## Primary reference basis

- https://learn.microsoft.com/en-us/ef/core/querying/related-data/lazy
- https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-regex
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.metadata.imodel.findruntimeentitytype?view=efcore-10.0

The code and executed tests, rather than these general references alone, define the delivered behavior.
