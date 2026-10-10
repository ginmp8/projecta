# Migrating from 0.2.0-alpha.1 to 0.3.0-alpha.1

This remains a pre-1.0 alpha line. Existing high-level constructor/Compile/Project/ProjectToToken signatures, type-registry behavior and optional adapter boundaries are retained. The original 110-case executable suite still passes. This is bounded compatibility evidence, not a promise about every low-level undocumented behavior.

## Opt-in additions

Selective reading defaults off. Schemas require an explicit provider. Functions require a catalog plus per-call allowlist. Context-bearing and JsonTypeInfo APIs use distinct names to avoid ambiguity with existing cancellation-token overloads. Streaming, document operations, LINQ projection, code generation and tooling are separate modules.

No data migration, database change or network service is required. New output/input schemas can intentionally reject previously accepted data; deploy rule changes with application-owned versioning and rollback. Do not reuse a compiled plan as an authorization token.

## Observable fixes

Decimal-backed values retain their numeric precision through direct writing and supported bridges. Tiny finite doubles no longer become zero through an intermediate decimal cast. Numeric equality no longer treats arbitrary different tiny values as equal through a broad epsilon. Consumers that relied on rounded serialization or approximate equality must update their expectations deliberately.

Configuration rejects duplicate members and unsupported schema assertions rather than accepting ambiguous definitions. CLI/document profiles have explicit limits and supported subsets; do not treat a successful dynamic expression as proof it can be translated/generated.

## Adoption sequence

1. Run existing application tests plus `scripts/verify.ps1` (or `.sh`) with the exact packages and intended OS.
2. Test representative DTO/JSON/EF sources and converters, including missing/null/required fields and decimal values.
3. Enable a new module separately; enforce application authorization before passing rules, data and context.
4. For provider projection, run the relational suite and inspect SQL for your provider before rollout.
5. For code generation, build the consumer with a compatible .NET 10 compiler host; retain compile-time diagnostics as errors.

Rollback is replacing packages/rules with the previously qualified versions. The library owns no persistent schema or deployment state. Never mutate a published package version; generate a new prerelease for subsequent repairs.
