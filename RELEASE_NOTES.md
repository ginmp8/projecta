# Projecta 0.2.0

Projecta 0.2.0 is the standalone .NET 10 / C# 14 object-projection and document-transformation release. The repository provides the expression engine, mapping and document modules, tests, examples, local tooling, package scripts and configuration for 11 packable projects.

## Capabilities

- **Expression and mapping engine:** reusable compiled JSONata expression plans, heterogeneous CLR and JSON inputs, typed DTO destinations, explicit type registry, evaluation budgets and cancellation.
- **Authoring:** conservative `ProjectionInspector` checks and explainable plans report known source/target requirements without evaluating source getters. Dynamic cases remain indeterminate rather than being silently accepted.
- **Rule lifecycle:** `ProjectionRuleRegistry` stages immutable versions with bounded retention and compare-and-swap activation/rollback; `ProjectionComparer` reports bounded metadata-oriented differences between rules.
- **Execution controls:** immutable per-call contexts, explicit host-function allowlists, opt-in source exposure rules, selective POCO reading and `ProjectionNullProperties.Omit` for object members. Source allowlists are not an authorization mechanism.
- **Diagnostics and operations:** structured failures, per-stage timing, execution-profile fingerprints and CLI `explain`/`compare` with pointer redaction enabled by default.
- **Integrations:** System.Text.Json, optional Newtonsoft.Json and EF Core adapters, input/output contract validation, ordered batch/JSONL processing, detached document patches and pipelines, restricted LINQ scalar projection, compile-time generated mappers and local development tooling.

## Public identities and license

- Release version: `0.2.0`, defined in `Directory.Build.props`.
- NuGet package authorship: `Projecta contributors`.
- Existing `Projecta` assembly, namespaces, module and package identities are retained.
- The internal compatibility test project is `Projecta.Compatibility.Tests`.
- Projecta is MIT-licensed under `LICENSE`; notices for incorporated third-party source are distributed separately in `THIRD-PARTY-NOTICES.md`.

## Verification and limitations

Build and test from a checkout with a supported .NET 10 SDK and the dependencies declared in `Directory.Packages.props`:

```sh
./scripts/verify.sh
./scripts/pack.sh
python3 scripts/release_manifest.py --check
```

PowerShell equivalents are provided under `scripts/`. See [validation instructions](docs/VALIDATION.md) for package-consumer checks and CI coverage. A valid source manifest does not attest that the runtime tests or dependency-security checks passed.

Full-language conformance, specific database-provider SQL translation, Windows/macOS runtime behavior, Native AOT/trimming, performance under production workloads, host callback safety and live vulnerability status must be qualified for each intended deployment. See [feature boundaries](docs/FEATURES.md) and [adoption guidance](docs/MIGRATION.md).
