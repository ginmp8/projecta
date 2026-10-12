# Migrating from 0.1.0 to 0.2.0

This remains a pre-1.0 release line. Existing high-level constructor/Compile/Project/ProjectToToken signatures, type-registry behavior and optional adapter boundaries are retained. The original 110-case executable suite still passes. This is bounded compatibility evidence, not a promise about every low-level undocumented behavior.

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

Rollback is replacing packages/rules with the previously qualified versions. The library owns no persistent schema or deployment state. Never mutate a published package version; generate a new version for subsequent repairs.

## Additional changes in 0.2.0

No existing projection call needs to adopt authoring or replay APIs. Existing default null-property behavior, complete-reading default and object materialization contracts are preserved. The optional `AllowedRootMembers` property is new and must be configured deliberately. Existing `IncludeProperty` callbacks still run for CLR source properties; their closure state is not frozen by the library.

The new `ProjectionInspector` has a bounded static scope. Treat preflight warnings as unresolved runtime questions, not validation success. `ProjectionRuleRegistry` does not persist activation state across processes; distribute source and active version through the host's established store when needed. `ProjectionReplayIdentity` is a configuration identity, not a record of input bytes or a signature. Programmatic comparisons can expose dynamic output field names through JSON Pointers; the CLI omits pointers by default.

The legacy bundled test data now includes a deterministic local library dataset instead of an unresolvable relative external path. `$eval` remains disabled by default, and the corresponding test asserts this security behavior. Historical date-format expectations were corrected to the existing ISO default while preserving an explicit custom-format test. These corrections do not change public date conversion defaults.
