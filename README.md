# Projecta

**Standalone .NET 10 object projection with a source-owned JSONata engine.**

Projecta incorporates and evolves the MIT-licensed `jsonata.net.native` engine supplied for this fork. It does **not** reference the `Jsonata.Net.Native` NuGet packages. It has no application, API, database service or product-specific dependency.

Version: **0.1.0**. This is a new package identity, not an upstream JSONata release. Package names have not been published or reserved on nuget.org.

## Validation status

The engine, System.Text.Json bridge and main projection package were built with .NET SDK 10.0.302 / runtime 10.0.10 on Linux. **110 package-free verification cases passed**; one case also executes a 200-object JSON differential corpus. The runnable sample and microbenchmarks executed.

The Newtonsoft bridge passed **12 smoke cases against the SDK's Newtonsoft 13.0.3 DLL**, not a restored 13.0.4 NuGet graph. `Directory.Packages.props` declares Newtonsoft **13.0.4** and EF Core **10.0.12**. Those exact package restores and EF runtime tests were blocked by unavailable NuGet transport. EF source, tests and CI are included, but EF is **not runtime-validated in this delivery**. Retained upstream NUnit suites were not executed. See [validation details](docs/VALIDATION.md); do not interpret core success as full production readiness.

## Packages

| Project | Role | Dependencies |
|---|---|---|
| `Projecta.Core` | Full native JSONata parser, AST, evaluator, built-in functions, JSON DOM, bounded CLR reader | .NET 10 only |
| `Projecta.SystemTextJson` | Native/System.Text.Json DOM bridge | Core; System.Text.Json from .NET 10 |
| `Projecta` | JSON definitions, compiled plans, bounded LRU cache, typed targets and target registry | Core and SystemTextJson |
| `Projecta.NewtonsoftJson` | Optional JObject/JArray/JValue sources and targets | Projecta; Newtonsoft.Json |
| `Projecta.EntityFrameworkCore` | Optional tracked entity/proxy snapshots and explicit scalar updates | Projecta; EF Core 10 |

Every project inherits `net10.0` and C# 14 from `Directory.Build.props`. All NuGet versions are centralized in `Directory.Packages.props`. No ORM dependency enters the main package.

## Quick start

Reference `src/Projecta/Projecta.csproj` from a consuming application, or build local NuGet packages. This source ZIP does not bundle `.nupkg` files; generate local NuGet packages with `dotnet pack Projecta.slnx -c Release -o artifacts/packages` after a successful build.

```csharp
using Projecta.Mapping;

var rule = ProjectionDefinition.Parse("""
{
  "id": "customer-v1",
  "version": 1,
  "fields": {
    "Id": "$string(id)",
    "Name": "fullName",
    "IsActive": "status = 'ACTIVE'"
  }
}
""");

var projector = new ObjectProjector();
var plan = projector.Compile(rule); // Compile once and reuse.
var source = new { id = 123, fullName = "Ana", status = "ACTIVE" };
CustomerView? result = projector.Project<CustomerView>(source, plan);

public sealed record CustomerView(string Id, string Name, bool IsActive);
```

`ProjectToToken()` returns the native JSON DOM; `Project<T>()` materializes a compatible CLR/JSON destination. DTO constructors, arrays, nullable values, enums and required properties use the .NET 10 System.Text.Json contract. Extra destination JSON members are rejected. Missing non-required destination members retain normal serializer defaults; mark members `required` when absence must fail.

A CLR `string` is a scalar. To project a JSON document, parse it first with `JsonDocument.Parse`, `JsonNode.Parse`, a Newtonsoft parser, or `Projecta.Json.JToken.Parse`. The native parser retains its upstream permissive mode; pass `ParseSettings.GetStrict()` for strict input rules.

## JSON rule contract

Choose exactly one of `fields` or `expression`. Each field is a **JSONata expression**, not a JSONPath string. Field names are literal: `"Address.City"` does not create nested objects. Build nesting in the expression:

```json
{
  "id": "nested-v1",
  "expression": "{'Name':customer.name,'Address':{'City':customer.city},'Total':$sum(items.price)}"
}
```

Use JSONata conditions/default expressions explicitly, e.g. `$exists(city) ? city : 'unknown'`. A wholly undefined result is rejected rather than silently becoming null. For stable zero/one/many collection cardinality, use explicit array constructors such as `[items.name]`.

A `CompiledProjection` is a snapshot: editing the original dictionary cannot change it. Compilation is cached by expression in a bounded LRU; evaluation limits are applied per call, not stored in shared query state.

## Supported sources and targets

- DTOs/POCOs, records, inherited public readable properties, dictionaries with string keys, arrays and `IEnumerable` values.
- Native `Projecta.Json` nodes, System.Text.Json `JsonDocument`, `JsonElement` and `JsonNode`.
- Newtonsoft `JObject`/`JArray`/`JValue` through the optional adapter, including nesting inside DTOs and collections.
- Tracked EF Core entities/proxies through the optional EF adapter, including collections of entities.
- Registered CLR destination types or concrete JSON destination shapes. Domain-specific factories can implement `IProjectionTargetMaterializer`.

The generic reader excludes static properties, indexers and non-public getters; handles common scalars explicitly; supports `[JsonPropertyName]` and `[JsonIgnore]`; rejects cycles, queryables and unregistered runtime proxies. Reflection accessors are weakly cached per type and compiled where supported. The fallback is reflection, not a claim of general trimming/Native AOT compatibility.

## Newtonsoft integration

```csharp
using Newtonsoft.Json.Linq;
using Projecta.Mapping;
using Projecta.NewtonsoftJson;

var projector = new ObjectProjector(
    sourceAdapters: new[] { new NewtonsoftSourceAdapter() },
    targetMaterializers: new[] { new NewtonsoftTargetMaterializer() });

JObject input = JObject.Parse("{\"name\":\"Ana\"}");
var definition = new ProjectionDefinition { Expression = "{'Name':name}" };
JObject? json = projector.Project<JObject>(input, definition);
Person? dto = projector.Project<Person>(input, definition);
```

Token adapters do not serialize an entire CLR object or pass through JSON text. The main DTO materializer uses System.Text.Json attributes, not Newtonsoft-specific DTO attributes. Use an explicit custom materializer when a special construction/converter contract is required.

## EF Core integration

```csharp
using Projecta.EntityFrameworkCore;
using Projecta.Mapping;

var adapter = new EfCoreSourceAdapter(dbContext, new EfCoreReadOptions
{
    ScalarProperties = new HashSet<string> { "Id", "Name", "Status" }
});
var projector = new ObjectProjector(sourceAdapters: new[] { adapter });
var dto = projector.Project<CustomerView>(trackedEntity, definition);
```

The adapter is **scoped to its DbContext**. It does not attach entities, execute `Load`, call `SaveChanges`, or translate JSONata into SQL. Lazy loading and automatic change detection are disabled for each snapshot and restored in `finally`. Mapped scalars and complex properties are read through EF entries; shadow properties are excluded unless explicitly enabled.

`LoadedNavigations` is an explicit allowlist. A requested but unloaded navigation throws by default; set `OmitUnloadedNavigations` deliberately to omit it. Cyclic graphs still fail rather than silently dropping references. Ordinary non-proxy entities using `ILazyLoader` must also use this adapter: the core cannot infer all hidden getter behavior.

```csharp
var options = new EfCoreReadOptions
{
    LoadedNavigations = new HashSet<string> { "Orders" }
};
// Load the relationship explicitly before projecting; the adapter never loads it.
```

An opt-in `TrackedEntityUpdater.Apply(entity, nativeJObject, permittedProperties)` applies a validated scalar patch to an existing tracked entity. It rejects keys, foreign keys, shadow/concurrency/generated members and relationships. It does not enforce domain methods, emit domain events, persist changes or roll back a setter that throws midway. Use domain factories/methods instead for aggregates with behavioral invariants.

## Runtime target allowlist

```csharp
var types = new ProjectionTypeRegistry().Register<CustomerView>("customer-view");
var rule = new ProjectionDefinition
{
    TargetType = "customer-view",
    Expression = "{'Id':'123','Name':'Ana','IsActive':true}"
};
object? result = projector.Project(source, rule, types);
```

No assembly scan or `Type.GetType` is performed from JSON. The registry is not caller authorization: authorize rules and data before invoking the library.

## Limits

```csharp
var projector = new ObjectProjector(new ProjectionOptions
{
    CacheCapacity = 128,
    Source = new() { MaxDepth = 32, MaxNodes = 50_000, MaxCharacters = 1_000_000 },
    Output = new() { MaxDepth = 32, MaxNodes = 50_000, MaxCharacters = 1_000_000 },
    Evaluation = new()
    {
        MaxSteps = 250_000,
        MaxAllocatedTokens = 100_000,
        MaxRangeLength = 20_000,
        MaxStringLength = 250_000,
        Timeout = TimeSpan.FromSeconds(2),
        AllowDynamicEvaluation = false
    }
});
```

Cancellation is accepted by reader/projector/evaluator APIs. Parser/optimizer nesting and expression length are independently bounded. Regex literals have a 250 ms match timeout. Nested `$eval`, when explicitly enabled, shares the original budget and variable environment.

**These are cooperative safety controls, not a malicious-code sandbox or a hard process-memory cap.** Existing CLR getters, custom converters/functions, BCL operations and malicious regex patterns may do work outside checkpoints. Use approved rules and model types. Isolate untrusted evaluation in an OS/container process with hard CPU/memory/deadline limits. See [security boundaries](docs/SECURITY.md).

## Build, verify, package

Requires a stable .NET 10 SDK and normal NuGet connectivity for integrations and retained NUnit projects.

```bash
dotnet build Projecta.slnx -c Release
dotnet run --project tests/Projecta.Tests -c Release
dotnet run --project tests/Projecta.NewtonsoftIntegration.Tests -c Release
dotnet run --project tests/Projecta.EntityFrameworkCore.Tests -c Release
dotnet run --project samples/Projecta.Sample -c Release
dotnet pack Projecta.slnx -c Release --no-build -o artifacts/packages
```

`./scripts/verify.sh` and `./scripts/verify.ps1` run the supported verification flow. `--core-only` / `-CoreOnly` selects only the package-free core subset and must not be reported as full integration validation.

The project-specific verification executables are intentionally package-free where possible; their exit code is nonzero on assertion failure or an unexpected empty run. They are invoked with **`dotnet run`**, not discovered by `dotnet test`. The separate upstream NUnit projects retain original compatibility assertions, including old defaults; they are reference suites, not a claim of full upstream compatibility or executed conformance.

CI definitions target Linux, Windows and macOS, but only Linux was executed for this delivery. GitHub Actions were not run here.

## Performance

The included warm microbenchmark is runnable with `dotnet run --project benchmarks/Projecta.Benchmarks -c Release`. A separate comparison with the unchanged original engine source (both built on .NET 10) measured roughly **2.3x faster CLR normalization** for one nested synthetic DTO and eliminated quadratic string-padding allocations. These narrow results are not a general throughput claim. Raw observations, workload details and limitations are in [performance notes](docs/PERFORMANCE.md).

## License and scope

MIT. Original copyright and permission text are retained in [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). See [upstream scope](docs/UPSTREAM.md) for included engine code, preserved tests and missing upstream git-submodule corpora. No original signing key, upstream publishing identity or application coupling is retained.
