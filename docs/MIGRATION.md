# Adopting Projecta 0.2.0

Projecta 0.2.0 is the version to reference when adopting the standalone library. Use the package identities shown in the [README](../README.md); public Projecta assembly names, namespaces and existing high-level `Compile`, `Project` and `ProjectToToken` APIs are retained.

This release requires .NET 10. Centrally managed dependency versions are defined in `Directory.Packages.props`; the optional Newtonsoft.Json, EF Core and generator integrations require their respective build/runtime dependencies. Projecta does not own a database schema and does not require a database migration.

## Opt-in features

- **Selective reading:** disabled by default. Enable only for rules whose source dependencies can be conservatively determined. Input-schema validation requires the full source and disables selective input normalization.
- **Contracts:** configure an explicit schema provider to validate inputs and outputs. Unsupported schema assertions and remote references are rejected.
- **Functions:** register trusted callbacks in a function catalog, allowlist their names per execution context and explicitly opt in to impure functions.
- **Context-aware execution:** contexts detach provided variable data. Context and `JsonTypeInfo<T>` overloads are explicitly named to avoid ambiguity with cancellation-token overloads.
- **Output policy:** omit null object properties only with `ProjectionNullProperties.Omit`; null array positions are unchanged.
- **Streaming, document transforms, LINQ and code generation:** each is a separate module/profile with distinct limitations; a dynamic JSONata rule is not automatically SQL-translatable or statically generatable.

## Behavior to validate when integrating

- Decimal-backed values preserve numeric precision in supported serialization paths. Very small finite doubles are not coerced to zero through an intermediate decimal conversion, and numeric equality does not treat arbitrary small values as equal using a broad epsilon.
- Rule and schema validation rejects duplicate or unsupported definitions rather than accepting ambiguous inputs. Distinguish undefined values from explicit JSON null and test missing/required destination properties.
- `AllowedRootMembers` can filter root members from supported source shapes. `IncludeProperty` still runs for CLR source properties; the library does not freeze the closure state of the callback. Data-exposure filtering does **not** replace application authorization.
- Authoring preflight and plan explanations are conservative and do not execute arbitrary source getters. Warnings describe unresolved cases, not proven acceptance.
- `ProjectionRuleRegistry` is in-memory. Persist and coordinate active rule versions in the host when running across processes. `ProjectionReplayIdentity` fingerprints configuration, not input bytes or cryptographic provenance.
- `ProjectionComparer` can expose data-dependent output property names through JSON Pointer paths when called programmatically. CLI comparison omits pointers by default.
- JSONata `$eval` remains disabled by default. Default date serialization remains ISO-based unless an explicit custom format is configured.
- Optional EF Core integration is scoped to tracked entities and a DbContext. Validate navigation loading, relationship restrictions and permitted scalar updates for the actual provider; no implicit `SaveChanges` or domain-invariant enforcement is provided.

## Adoption and rollback checklist

1. Reference version `0.2.0` of the required modules and restore the dependency graph with the target .NET 10 SDK.
2. Run `scripts/verify.ps1` or `scripts/verify.sh`, the package-consumer example and your application's regression tests.
3. Exercise representative DTO, dictionary, JSON DOM and EF sources, including null/required members, converters and decimal values.
4. Enable additional modules individually, validate contracts and enforce host authorization before supplying rules, source objects or execution context.
5. For provider projections, inspect generated SQL and execute the relational tests with the intended provider.
6. For static generation, verify the consumer compiler host and source-generator diagnostics.

Application rollback consists of restoring the previously qualified application/package state and rule definitions. The library does not provide a persistence migration or deployment rollback system. Published NuGet versions must remain immutable.
