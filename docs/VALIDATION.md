# Verification and validation

Projecta 0.2.0 targets `net10.0` / C# 14. Use a .NET 10 SDK and the dependency versions declared in `Directory.Packages.props`. This guide describes repeatable checks; the presence of a script or CI workflow is not evidence of a passing run.

## Source integrity

```sh
python3 scripts/release_manifest.py --check
```

This verifies the checked-in source inventory, SHA-256 file hashes, version and key metadata, 11 packable modules, project and solution references, license files and internal compatibility-test naming. Run `python3 scripts/release_manifest.py --write` after intentional source changes that will be included in a new distribution snapshot. This check is structural; it does **not** restore dependencies, compile C# or execute tests.

## Build and test

```sh
./scripts/verify.sh
./scripts/pack.sh
```

On Windows or any PowerShell host:

```powershell
./scripts/verify.ps1
./scripts/pack.ps1
```

The verification scripts compile the solution, run executable regression/semantic/generator/provider suites and invoke the NUnit test projects. Full verification includes the optional Newtonsoft.Json and EF Core integrations; the `--core-only` (or `-CoreOnly`) profile verifies only the defined package-free subset. Reusing `--no-restore` requires a previously successful restore and does not validate dependency availability.

The suite families cover expression and mapping regressions, maintained semantic fixtures, adversarial inputs, source generation, JSON DOM adapters, EF Core tracked/proxy behavior, relational SQLite behavior and package consumers. Test counts and results must be taken from the current execution output, not from historical local runs.

## Consumer smoke test

After packaging into `artifacts/packages`, validate restoration and usage from the local feed:

```sh
dotnet restore tests/PackageConsumer/Consumer.csproj --configfile tests/PackageConsumer/NuGet.Config --packages artifacts/consumer-cache --force
dotnet run --project tests/PackageConsumer/Consumer.csproj -c Release --no-restore
```

CI is configured to run the full workflow on Linux, Windows and macOS and retain validation/package artifacts. Confirm the actual workflow result for the revision being released; configuration alone does not establish portability.

## Evidence and qualification boundaries

- **Source check:** proves that files match the recorded source snapshot, not that binaries were built.
- **Build and tests:** prove behavior only for the executed environment, dependency graph and exercised scenarios.
- **Security:** run vulnerability/dependency checks separately; do not equate input-exposure allowlists with authorization or trusted-host-function execution with a sandbox.
- **Provider integration:** validate generated SQL and database behavior on every supported provider, not only in-memory or SQLite profiles.
- **Performance:** follow [benchmark instructions](PERFORMANCE.md); report platform, runtime, dataset, repetitions, raw measurements and allocations when making quantified claims.
- **Unqualified by these scripts:** full JSONata language equivalence, arbitrary database SQL translation, Native AOT/trimming, production latency/capacity and production operational safety.

A release approval must rely on execution evidence for the precise source revision, runtime and dependency versions.
