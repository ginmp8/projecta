# Projecta 0.2.0

This release consolidates the standalone .NET 10 projection and document toolkit, including the capabilities developed across earlier branch candidates. The core remains independent of ORMs and host frameworks.

**Authoring:** `ProjectionInspector` reports statically known source/target members, required destination fields and indeterminate checks; `Explain` exposes a frozen plan fingerprint, literal dependency paths and conservative fallback reasons without executing source getters.

**Rules:** `ProjectionRuleRegistry` compiles versions before publishing, bounds retained rules, disallows replacing a version with different content and supports compare-and-swap activation/rollback. `ProjectionComparer` evaluates two plans and reports metadata-only differences with both IDs and versions, including failed evaluations.

**Execution:** explicit `ProjectionNullProperties.Omit` removes null object fields before output-contract validation, while arrays retain null items. Optional `AllowedRootMembers` is applied to root data in POCOs, dictionaries, native/standard JSON containers and array elements. Conservative selection supports nested POCO property paths. Mutated caller-owned allowlists cannot change a running projector policy snapshot.

**Operations:** low-cardinality stage timing metrics, non-plaintext replay-profile fingerprints with explicit missing-provenance qualifications, CLI `explain`/`compare` and redaction of data-dependent difference keys in CLI output.

**Also included:** declarative projections, typed destinations, input/output contracts, streaming batches and JSONL, document operations, restricted LINQ projection, source-generated mappers, local tooling, evaluation budgets and numeric fidelity improvements.

**Validation:** the Linux .NET 10 build, configured executable/NUnit/provider tests and a separate package-consumer test are documented in `docs/VALIDATION.md`. Non-Linux CI, full language conformance, production hardening, live NuGet audit and Native AOT publishing are not claimed.

See `docs/AUTHORING.md`, `docs/VALIDATION.md`, `docs/MIGRATION.md`, `docs/SECURITY.md` and `THIRD-PARTY-NOTICES.md`. Original license notices remain intact.
