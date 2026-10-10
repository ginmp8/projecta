# Migration and compatibility

This is a source fork with a new identity. Do not treat it as a binary-compatible replacement package.

| Previous surface | Projecta surface |
|---|---|
| `Jsonata.Net.Native` namespace | `Projecta` |
| `Jsonata.Net.Native.Json` | `Projecta.Json` |
| `JsonataQuery` | `ProjectaQuery` |
| `Jsonata.Net.Native.JsonNet` | `Projecta.NewtonsoftJson` |
| `Jsonata.Net.Native.SystemTextJson` | `Projecta.SystemTextJson` |
| `UniversalProjection.ObjectProjector` alpha wrapper | `Projecta.Mapping.ObjectProjector` |
| Previous wrapper `ProjectToJson` | `ProjectToToken` |
| netstandard/net8/net9 targets | net10.0 only |
| NuGet engine dependency | ProjectReference to source-owned Projecta.Core |

`JsonataException` and `JsonataErrorCode` names remain language-specific diagnostics, now inside the `Projecta` namespace. Private `JsonataQ`/`JsonataArray` identifiers describe JSONata semantics rather than an external package dependency.

Intentional behavior changes: cycles/proxies/queryables are rejected by the generic reader; indexers/static getters are not invoked; GUIDs/enums/common scalar types are explicit; sequence enumeration is bounded; JSON and expression parsing have limits; `$eval` is disabled unless explicitly enabled; global default environments no longer share mutable user bindings; native ranges are capped at 100,000; regex literals time out; default Newtonsoft date conversion is invariant round-trip rather than current-culture general format.

Use the mapping layer for typed DTO construction (including records, arrays, nullable/required contracts). Low-level upstream `JToken.ToObject` is retained for compatibility but does not gain all System.Text.Json constructor/converter semantics.

No external configuration repository, DI package, hosted worker, HTTP endpoint or database access service is included. Host applications own configuration storage/version publication, data loading and authorization.
