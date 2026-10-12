# Execution contracts

## Plans and contexts

`ObjectProjector.Compile` snapshots definitions, schemas and fields. The bounded expression LRU caches parsed queries; contracts and rule metadata belong to the returned plan. Parsing does not hold the LRU lock. `MaxConcurrentCompilations` independently bounds cold parsing; the default is 4. A failed compilation is not cached as a valid expression.

`CompiledProjection.Fingerprint` hashes expression, target alias and raw schema text. It is a plan-content identifier, not a signature, authorization credential or canonical semantic equivalence test. Whitespace changes in schema text can change the fingerprint. `CacheStatistics` reports lookups, misses, evictions and current size; concurrent misses can share a subsequent cache fill.

A `ProjectionExecutionContext` snapshots variables and the allowed-function set. Each execution receives detached JSON values and a fresh environment. Names are unprefixed ASCII identifiers, capped at 128 characters. At most 128 variables/functions are accepted. Built-in names cannot be shadowed. Source adapters and callbacks may maintain host state; their own thread safety remains a caller responsibility.

```csharp
using Projecta.Json;
using Projecta.Mapping;

var functions = new ProjectionFunctionCatalog(new[]
{
    new ProjectionFunction("cleanName", 1, 1,
        (arguments, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new JValue(((string?)arguments[0] ?? "").Trim());
        }, IsPure: true)
});
var projector = new ObjectProjector(new ProjectionOptions { Functions = functions });
var plan = projector.Compile(new ProjectionDefinition { Expression = "{'Name':$cleanName(name)}" });
var context = new ProjectionExecutionContext(allowedFunctions: new[] { "cleanName" });
var output = projector.ProjectToTokenWithContext(new { name = " Ana " }, plan, context);
```

Arity and optional language signatures are checked. Arguments and return values cross bounded detached-token boundaries. `IsPure` is a declaration by trusted host code, not a proof. Impure functions require `allowImpureFunctions: true`. A callback cannot be forcibly interrupted inside arbitrary blocking host code; do not call external services from untrusted evaluations.

## Diagnostic behavior

`TryCompile` and `TryProject<T>` return `ProjectionResult<T>` with either a value or nonempty diagnostics. Phases are Compile, Context, Source, InputContract, Evaluate, OutputContract, Materialize and Stream. Field/position are present only when an engine position can be mapped to a `fields` expression; a complete free-form expression has no invented field attribution.

Contract errors include a JSON Pointer and expected/actual type where applicable. Other errors omit unknown type information rather than infer it. Cancellation and out-of-memory exceptions propagate. Do not log raw thrown exceptions from untrusted inputs: direct throwing APIs preserve inner exceptions for debugging; only structured diagnostics redact raw values/messages by design. IDs, field names and pointers are caller-owned metadata and can themselves be sensitive.

## Source selection and destination construction

Selective reading is off by default. Analysis can prove root dependencies for literal paths, arithmetic, simple conditions, arrays and literal-key objects. Calls, wildcards, root/context navigation and complex context changes require full reading. There is no lazy on-demand property proxy. Input-schema validation disables pruning. Selected nested POCO paths can be pruned recursively, including POCO items in root arrays or nested arrays. Requesting a parent without a child segment retains its complete object. JSON DOMs, dictionaries and custom adapters are always normalized completely for path selection; input-contract validation disables pruning. Adapters, dictionaries and JSON DOMs retain their established traversal behavior.

Destination DTOs are written to bounded UTF-8 and deserialized without constructing an intermediate JsonNode. Native/JsonNode destinations return detached values. Custom materializers receive detached tokens. `ProjectWithTypeInfo<T>` uses the caller's metadata/options, including enum/required/unmapped policies; these may differ from default `Project<T>` policies.

Numbers backed by decimal/integer storage retain invariant numeric text. Very small finite double values are not rounded to zero through a decimal cast. Floating-point arithmetic remains floating-point arithmetic; a JSON numeric value is not a promise of arbitrary precision throughout every engine function.

## Telemetry

`ProjectionTelemetry.ActivitySource` and `Meter` expose evaluation count, failure count, evaluation duration and a `projecta.stage.duration` histogram tagged only by fixed stage labels (compile, context, source, inputcontract, evaluate, outputcontract, materialize). Activities use plan fingerprints, not source data. The current evaluation span includes context, normalization, validation and evaluation; it excludes subsequent destination materialization and compilation. Do not interpret it as complete end-to-end request latency. Subscribe through normal .NET diagnostics integrations; the library installs no exporter, performs no network call and logs no payload.
