# Projecta

**Standalone .NET 10 projection and document transformation.**

Version **0.3.0-alpha.1**. Dynamic JSONata rules, heterogeneous object sources, typed destinations, bounded execution, optional data contracts, streaming records, document operations and restricted compiled projections.

## Release qualification

The package-free solution has been built and exercised on Linux with .NET SDK **10.0.302** / runtime **10.0.10**. The exact execution counts, package-consumer checks and archive identity are recorded in [validation](docs/VALIDATION.md).

This is an **alpha source and package distribution, not a production approval**. Exact EF Core/Newtonsoft/NUnit restores and relational-provider execution remain unverified in this environment. No Windows/macOS execution, complete JSONata conformance or Native AOT publish is claimed. Every optional integration is included as source; the package inventory identifies which binaries were actually produced.

## Packages

| Package | Responsibility | Extra NuGet dependencies |
|---|---|---|
| `Projecta.Core` | JSONata parser, AST, evaluator, native JSON DOM, CLR reader and budgets | None |
| `Projecta.SystemTextJson` | JSON DOM adapters, bounded UTF-8 writer | None |
| `Projecta` | Immutable plans, LRU cache, contexts, diagnostics, contracts, typed materialization | None |
| `Projecta.Contracts` | Explicit structural JSON Schema profile | None |
| `Projecta.Streaming` | Ordered batches and incremental JSONL | None |
| `Projecta.Documents` | JSON Pointer, document patches, merge patches and pipelines | None |
| `Projecta.Queryable` | Restricted scalar LINQ `Select` expression builder | None |
| `Projecta.SourceGeneration` | Static mapper generator for .NET 10 compiler hosts | SDK compiler assemblies at build time |
| `Projecta.Tooling` | Local CLI: validate, preview, JSONL, patch, pipeline, conformance and benchmark | None |
| `Projecta.NewtonsoftJson` | Optional `JObject`/`JArray`/`JValue` adapters | Newtonsoft.Json |
| `Projecta.EntityFrameworkCore` | Optional tracked-entity snapshots and explicitly permitted scalar updates | EF Core 10 |

"None" excludes other Projecta packages. All runtime projects target `net10.0` / C# 14. Versions are centralized in `Directory.Packages.props`; build policy is in `Directory.Build.props`. The core has no database, application, API, service or product-specific dependency.

## Quick start

Reference `src/Projecta/Projecta.csproj`, or restore the corresponding package from your local package feed.

```csharp
using Projecta.Mapping;

var rule = ProjectionDefinition.Parse("""
{
  "id": "customer-view",
  "version": 1,
  "fields": {
    "Id": "$string(id)",
    "Name": "fullName",
    "IsActive": "status = 'ACTIVE'"
  }
}
""");
var projector = new ObjectProjector();
var plan = projector.Compile(rule); // Compile once; reuse the immutable plan.
var source = new { id = 123, fullName = "Ana", status = "ACTIVE" };
CustomerView? value = projector.Project<CustomerView>(source, plan);

public sealed record CustomerView(string Id, string Name, bool IsActive);
```

Choose exactly one of `fields` or `expression`. Values are **JSONata expressions**, not JSONPath. Field keys are literal: `"Address.City"` does not create a nested object. Use `{'Address':{'City':address.city}}` for nesting. Use explicit arrays, such as `[items.name]`, to stabilize zero/one/many cardinality. An undefined top-level result is rejected; JSON null is distinct from undefined.

A CLR `string` is a scalar. Parse JSON text with `JsonDocument.Parse`, `JsonNode.Parse` or a native parser before projecting. The native parser retains a permissive mode; select `ParseSettings.GetStrict()` where strict JSON is required.

## Context and diagnostics

```csharp
var plan = projector.Compile(new ProjectionDefinition
{
    Id = "invoice",
    Expression = "{'Total':amount * $rate}"
});
var context = new ProjectionExecutionContext(
    new Dictionary<string, object?> { ["rate"] = 1.05m });
var result = projector.TryProject<InvoiceView>(new { amount = 100m }, plan, context);
if (!result.Success)
    foreach (var error in result.Diagnostics)
        Console.WriteLine($"{error.Code}: {error.Phase}/{error.Field}");

public sealed record InvoiceView(decimal Total);
```

Contexts detach variable data at construction and isolate evaluations. Variable names cannot replace built-ins. Cancellation propagates rather than being disguised as a failed record. Diagnostics carry rule/version, phase, field and engine position where available; they do not include source values, full expressions or raw exception messages. See [execution](docs/EXECUTION.md).

## Optional features

**Contracts.** Configure `ContractProvider = new JsonSchemaContractProvider()` and supply `inputSchema` / `outputSchema` in a definition. Unsupported assertions fail during compilation, and remote references are not resolved. The [contract profile](docs/CONTRACTS.md) is deliberately smaller than complete JSON Schema.

**Selective reading.** Set `EnableSelectiveReading = true` to prune statically proven root POCO members. Dynamic/context-dependent expressions fall back to full reading. Input contracts disable pruning so validation sees the full input. Nested selected subtrees are still normalized completely. Existing adapters keep their source-specific policies.

**Functions.** Register trusted callbacks in an immutable `ProjectionFunctionCatalog`, then authorize function names explicitly in each `ProjectionExecutionContext`. An impure callback needs additional execution opt-in. This is not arbitrary C# execution from JSON or a sandbox.

**Typed metadata.** `ProjectWithTypeInfo<T>()` accepts `JsonTypeInfo<T>`. It honors the supplied serializer contract. This removes reflection from that destination path, not necessarily from source reading or the whole application.

**Streaming.** `ProjectMany`, `ProjectManyAsync` and `ProjectJsonLinesAsync` reuse plans, preserve order and report per-record outcomes. JSONL is incremental by record, not a streaming evaluator for a single arbitrarily large document. See [streaming](docs/STREAMING.md).

**Document operations.** Detached patches and pipelines preserve the original document on failure. See [document operations](docs/DOCUMENTS.md).

**Query and generated mapping.** Scalar property paths and constants can use the restricted `Projecta.Queryable` or compile-time generator. These are explicit profiles, not alternative ways to execute every JSONata expression. See [static projections](docs/STATIC-PROJECTIONS.md).

## Existing integration contracts

DTOs, records, public inherited properties, dictionaries with string keys, arrays, collections, native JSON, `JsonElement`, `JsonDocument` and `JsonNode` remain supported. Custom `IObjectSourceAdapter` and `IProjectionTargetMaterializer` implementations remain available. The generic reader rejects cycles, queryables and unregistered runtime proxies; it skips static properties, indexers and non-public getters.

`Project<T>` uses the System.Text.Json destination contract, rejects unmapped output members and supports constructor binding, required properties and string enums. Missing non-required members keep normal serializer defaults. Register special construction logic in a target materializer; the library does not bypass domain invariants.

The optional Newtonsoft adapter supports JSON DOMs, not Newtonsoft-specific DTO attributes. EF adapters remain **scoped to a DbContext**: tracked entities only, no attach/load/save, explicit navigation allowlists, lazy loading and automatic change detection temporarily disabled and restored. Unloaded requested navigations fail by default. `TrackedEntityUpdater` is opt-in and rejects key/foreign-key/concurrency/generated/relationship updates; it does not enforce domain methods, persist changes or roll back arbitrary setter effects.

The destination type registry is an allowlist of registered CLR aliases. It does not scan assemblies or resolve type names from JSON and is not a substitute for caller authorization.

## Build, verify and package

Use a stable .NET 10 SDK. Full verification requires access to the exact packages declared in central package management.

```powershell
./scripts/verify.ps1
./scripts/pack.ps1
```

```sh
./scripts/verify.sh
./scripts/pack.sh
```

The **explicit package-free subset** is:

```sh
./scripts/verify.sh --core-only
./scripts/pack.sh --core-only
# PowerShell: -CoreOnly
```

`--no-restore` / `-NoRestore` reuses an already successful restore. It does not turn a missing restore into a pass. Verification runs executable suites with `dotnet run`; full verification additionally runs the three retained NUnit suites with `dotnet test` and a relational SQLite test executable. CI is configured for Linux, Windows and macOS; configuration is not evidence that those runners passed.

A local `projecta` tool can be installed from `artifacts/packages` after packaging:

```sh
dotnet tool install --tool-path .tools --add-source ./artifacts/packages Projecta.Tooling --version 0.3.0-alpha.1
.tools/projecta help
```

See [tooling and examples](docs/TOOLING.md), [feature status](docs/FEATURES.md), [migration](docs/MIGRATION.md), [performance](docs/PERFORMANCE.md) and [security boundaries](docs/SECURITY.md).

## License

MIT. Retained copyright and permission notices are in [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Package names are local distribution identities; this delivery does not publish or reserve names on a package registry.
