# Validation: 0.2.0

## Scope and verdict

**Executed and passed: complete Linux .NET 10 source build, configured executable tests, exact declared optional NuGet integrations, relational SQLite checks, built packages and independent package consumer.** These results do not constitute production approval. This page describes the included immutable candidate and its measured limitations; previous-candidate results are not substituted for current verification.

## Environment and provenance

- Linux x64; .NET SDK **10.0.302** / installed .NET 10 runtime; project targets `net10.0` and C# 14.
- Release build: **0 errors and 0 warnings**, `TreatWarningsAsErrors` preserved.
- `Projecta.slnx` restored using the exact direct and transitively resolved NuGet closure supplied as a verified **35-package offline bundle**, without changing `Directory.Packages.props` or project dependencies. Local SDK/feed/cache paths were validation-only and are not project dependencies.
- Source and package file hashes are in `DISTRIBUTION-MANIFEST.json` and `artifacts/validation/packages.json`; do not rely on mutable local NuGet caches.

## Executed checks

| Gate | Passed | Failed | Evidence |
|---|---:|---:|---|
| Original package-free core regression suite | 110 | 0 | `core-tests.json` |
| Evolution / robustness / authoring / CLI regression suite | 273 | 0 | `evolution-tests.json` |
| Finite JSONata semantic corpus | 490 | 0 | `semantics.json` |
| Source-generated mapper consumer checks | 102 | 0 | `build-and-tests.log` |
| Newtonsoft optional bridge smoke | 12 | 0 | `build-and-tests.log` |
| EF Core tracked entity/proxy smoke | 9 | 0 | `build-and-tests.log` |
| SQLite relational SQL tests | 5 | 0 | `build-and-tests.log` |
| Existing upstream NUnit language tests | 83 | 0 | `Projecta.Upstream.Tests.trx` |
| NUnit System.Text.Json suite | 26 | 0 | `Projecta.SystemTextJson.Tests.trx` |
| NUnit Newtonsoft.Json suite | 29 | 0 | `Projecta.NewtonsoftJson.Tests.trx` |
| Standalone packaged consumer from fresh local NuGet feed | 22 | 0 | `package-consumer.log` |

The `Projecta.Tooling` package was also **installed** from its `.nupkg` and its help command executed (`tool-install.log`). All **11 Projecta package archives** were generated during development of this 0.2.0 release (`packages.json`). Tests nested in individual suites (including 600 fuzz inputs) are not double counted. Full release commands:

```sh
./scripts/verify.sh
./scripts/pack.sh
# After packaging, follow tests/PackageConsumer/README.md
```

The test project assembly versions, provider identity, artifact set and baseline scenarios are fixed in checked-in configs; zero-test execution is not reported as coverage. Any change to candidate files after validating them requires another full gate.

## Important limitations

1. **Other OSes:** Windows and macOS are included in CI definitions but were **not executed** in this sandbox.
2. **Language equivalence:** 490 maintained corpus cases passed; this does **not** establish complete JSONata conformance or arbitrary expression equivalence.
3. **Provider scope:** real SQLite SQL tests passed, but no live PostgreSQL or SQL Server provider execution is claimed; LINQ translates only an explicit scalar subset.
4. **Host safety:** third-party getters/adapters and explicitly authorized callbacks may have side effects; root allowlists are not tenant authorization.
5. **Runtime delivery:** Native AOT/trimming publish, production workloads, exhaustive fuzzing, vulnerability-advisory freshness audit and signing are not qualified.

See `docs/FEATURES.md` for precise supported subsets and `docs/AUTHORING.md` for new API constraints.
