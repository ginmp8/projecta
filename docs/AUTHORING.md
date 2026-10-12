# Authoring, lifecycle and explainable projections

This document describes the opt-in APIs available in `Projecta` 0.4.0-alpha.1. None of them creates a database, network connection, background worker or execution service.

## Static preflight and plan explanation

```csharp
var projector = new ObjectProjector(new ProjectionOptions { EnableSelectiveReading = true });
var definition = new ProjectionDefinition
{
    Id = "customer-view", Version = 2,
    Fields = new() { ["Name"] = "Customer.Name", ["Age"] = "Customer.Age" }
};
var inspector = new ProjectionInspector(projector);
ProjectionPreflightReport report = inspector.Inspect(definition, typeof(CustomerSource), typeof(CustomerView));
if (!report.Success) throw new InvalidOperationException("Rule failed static preflight.");
CompiledProjection compiled = projector.Compile(definition);
ProjectionPlanExplanation explanation = inspector.Explain(compiled);
```

The inspector uses the compiled expression AST and CLR type metadata without invoking source getters. It checks known root members and, for explicit `fields`, known target names and `[JsonRequired]`/C# `required` members. Missing CLR source members are **warnings**, because expressions can handle absent values. Unavailable target members and omitted required fields are errors in the supported strict case. Arbitrary expressions receive an **indeterminate** warning about output shape. A passing static preflight does not prove the expression will succeed or that its runtime output satisfies destination contracts. Path selection may still fall back to full reading for dynamic language constructs, input contracts or disabled optimization.

## Stage and atomically activate rule versions

```csharp
var rules = new ProjectionRuleRegistry(projector, maxRules: 100, maxVersionsPerRule: 16);
var first = rules.Stage(new ProjectionDefinition { Id = "customer", Version = 1, Expression = "Customer" });
var second = rules.Stage(new ProjectionDefinition { Id = "customer", Version = 2, Expression = "{'Name':Customer.Name}" });
rules.TryActivate("customer", version: 1, expectedActiveVersion: null);
// This only succeeds if the active version has not changed since the caller read it.
bool promoted = rules.TryActivate("customer", version: 2, expectedActiveVersion: 1);
// Roll back to an already staged version; another writer can still win first.
bool rolledBack = rules.TryActivate("customer", version: 1, expectedActiveVersion: 2);
CompiledProjection active = rules.GetActive("customer") ?? throw new InvalidOperationException("No active version.");
```

The registry is thread-safe and holds immutable compiled plan references. Compilation finishes before it acquires the publication lock. A different expression/schema cannot silently overwrite an existing `(id, version)`. Calling `Stage` does not activate a rule. Authorization of rule authors, durable history, storage, cross-process coordination and deployment rollback belong to the host.

## Compare rule versions

```csharp
var before = rules.GetVersion("customer", 1)!;
var after = rules.GetVersion("customer", 2)!;
ProjectionComparisonReport diff = new ProjectionComparer(projector)
    .Compare(source, before, after, maxDifferences: 64);
Console.WriteLine($"{diff.BeforeRuleId}:{diff.BeforeVersion} -> {diff.AfterRuleId}:{diff.AfterVersion}");
```

Both sides use the same source *reference* and call context, but the source is normalized separately. Mutable/side-effecting source getters, custom adapters, host callbacks and external state may produce different reads. A failed side still retains its own rule ID/version, plan fingerprint and diagnostic set. Difference entries distinguish added/removed keys, type changes and value changes. Comparison output excludes leaf values but **programmatic JSON Pointers may contain source-derived object keys**. Do not log them without host authorization and redaction. The local CLI returns difference kinds and IDs while redacting all pointer paths.

## Output and input exposure policies

```csharp
var projector = new ObjectProjector(new ProjectionOptions
{
    EnableSelectiveReading = true,
    NullProperties = ProjectionNullProperties.Omit,
    Source = new Projecta.Objects.ObjectReadOptions
    {
        AllowedRootMembers = new[] { "Customer", "RequestId" }
    }
});
```

Null-object-property omission runs after evaluation and before output-contract validation. Null array elements are never dropped. Without this opt-in, the original output shape is preserved. Source `AllowedRootMembers` is a root-member allowlist applied by the reader to JSON documents, native DOMs, dictionaries, POCOs and root array items; `IncludeProperty` remains a CLR property predicate. A custom source adapter may have already accessed its source before its root output is filtered. These are data-minimization tools, not tenant authorization. The caller must authorize source objects, fields and output consumption independently.

## Replay identity and lifecycle limitations

```csharp
var identity = ProjectionReplayIdentity.Create(compiled, projector.Options,
    context: ProjectionExecutionContext.Empty, sourceIdentity: "snapshot-0001");
if (!identity.CanReproduce) Console.WriteLine(string.Join(",", identity.Limitations));
```

`ConfigurationFingerprint` includes bounded reader/output policies, selective-reading and null policies, evaluation limits, context binding **names** and allowable functions. It deliberately excludes the raw source, binding values and callback bodies. Providing `sourceIdentity`/`bindingIdentity` means the caller assumes responsibility for the stability and meaning of those opaque identities; it is not cryptographic proof of the supplied input. Missing provenance, custom callbacks, mutable predicates and host catalogs appear as explicit limitations. Fingerprints are not authorization credentials or semantic equivalence proofs.

## CLI

`projecta explain --rule FILE` reports read mode, paths and plan identity without loading an input. `projecta compare --before OLD --after NEW --input DATA` compares outputs with bounded differences but redacted pointer paths. The CLI never executes remote scripts or downloads configuration.
