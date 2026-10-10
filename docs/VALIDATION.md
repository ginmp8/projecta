# Validation: 0.2.0

## Qualification

**Executed package-free Linux candidate, with bounded feature profiles. Not a full integration or production approval.** The entire optional source tree is included, but binary/runtime qualification is limited to the exact paths below. The distribution manifest identifies delivered bytes; `artifacts/validation/release-validation.json` records final gates and source identity.

## Executed environment

.NET SDK 10.0.302; .NET runtime 10.0.10; Linux x64 / Debian 13. A verified SDK archive was used in an isolated build environment. No SDK, package-cache path, network workaround or Library dependency is part of the product. Commands used the normal source project files and retained warnings-as-errors. Restore for package-free projects used a local empty-feed configuration; full package restore was separately attempted and failed for unavailable exact dependencies.

## Executed checks

| Check | Result | Evidence |
|---|---|---|
| Package-free solution, Release | 0 warnings, 0 errors | `verify-core.log` |
| Original executable verification | 110 passed, 0 failed | `core-tests.json` |
| Evolution verification | 238 passed, 0 failed | `evolution-tests.json` |
| Finite semantic corpus | 490 passed, 0 failed, 0 skipped | `semantics.json` |
| Real compiler-generated mapper | 102 checks passed | `verify-core.log` |
| Separate package consumer | 12 passed; restores actual `.nupkg` files | `package-consumer.log` |
| Supplementary Newtonsoft smoke | 12 passed against SDK DLL **13.0.3.27908**, not declared NuGet 13.0.4 | `newtonsoft-13.0.3-smoke.log` |
| CLI tool installation and local examples | Executed help, validate, preview, JSONL, pipeline and patch | `cli-smoke.json`, `tool-install.log` |
| Synthetic benchmark | Five scenarios, nine measured rounds each | `benchmark.json` |
| Nine package-free packages | Produced and inspected | `packages.json` |

Evolution cases include deterministic parser/evaluator fuzzing of 600 inputs, selective-reading equivalence and concurrent context isolation. Those nested workloads are **not added again** to the 238 test count. The semantic fixture suite is project-authored and deliberately finite; its passing denominator does not establish complete language compatibility or independent differential equivalence.

The old 0.2 validation observations were not reused as evidence for current code. Focused red/green regressions exposed and fixed numeric conversion, configuration, generation and analyzer packaging issues; `review.json` records their scope. Log copies normalize absolute host paths and remove terminal escape sequences without changing outcomes. This is not an independent third-party audit.

## Blocked or not executed

- Full `Projecta.slnx` restore: unavailable exact NUnit/Test SDK, Newtonsoft.Json 13.0.4 and EF Core 10.0.12 package closures. `full-restore-blocked.log` records NU1100 failures. Versions were not downgraded to make the build appear successful.
- Exact optional integration builds, the three retained NUnit suites and EF tracked-entity/provider runtime tests. The supplementary Newtonsoft DLL smoke is not a replacement for these gates.
- `Projecta.Relational.Tests` SQLite SQL assertions: source exists; not executed. In-memory LINQ tests do not prove SQL translation.
- Windows/macOS execution and remote GitHub Actions. Workflows and PowerShell scripts are supplied, not claimed as executed.
- Full official/independent JSONata conformance and complete behavior parity with any external engine.
- Native AOT or trimming publish/run, production workload/soak tests and process-level isolation.
- Current dependency-vulnerability clearance, signing, registry publication and reservation of package names.

## Reproduction

```powershell
./scripts/verify.ps1
./scripts/pack.ps1
```

```sh
./scripts/verify.sh
./scripts/pack.sh
```

For the deliberately limited package-free gate, use `--core-only` / `-CoreOnly`. Only use `--no-restore` / `-NoRestore` after a successful restore. The scripts build the relevant solution, execute ordinary console test suites and samples, and the full path separately invokes all three retained NUnit suites plus optional integrations. A build or a zero-test invocation is not presented as executed coverage.

For the packaged graph, follow `tests/PackageConsumer/README.md`. The consumer is intentionally excluded from both source solutions to avoid a package-production dependency cycle. Use fresh local package caches when validating a newly produced unpublished candidate. Package identity is determined by hashes, not by a mutable local filename.

## Release boundary

Run full integration/provider/platform checks on the intended environment before production adoption. No claim is made that every error path, getter, converter, callback or hostile expression is safe. Feature-profile limitations are listed in `FEATURES.md`, and trust boundaries in `SECURITY.md`. The archive contains tested package-free binaries plus all optional source; it does not invent unbuilt optional packages.
