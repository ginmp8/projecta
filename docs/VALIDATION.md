# Validation report - Projecta 0.2.0-alpha.1

## Executed

- .NET SDK 10.0.302 / runtime 10.0.10 on Linux x64, installed from the supplied offline SDK archive.
- Core, System.Text.Json bridge and main Projecta package compiled in Release with warnings as errors.
- **110 passed / 0 failed** in `tests/Projecta.Tests`, including a 200-object JSON differential corpus, source traversal, limits, parser sync/async, cardinality, JSONata functions, DTO contracts, cached plans and parallel evaluation.
- Baseline red reproduced indexer invocation, unrecognized enumerable/enum and stack overflow for a GUID. The new reader passes those cases. A separate red test reproduced `int.MinValue` padding width handling before the fix.
- **12 passed / 0 failed** in the new Newtonsoft adapter smoke runner against the SDK-bundled Newtonsoft DLL, file version **13.0.3.27908**. The same delivered adapter/test source was compiled in an external validation harness. This does not prove restore/test of declared Newtonsoft 13.0.4.
- Runnable end-to-end sample and synthetic microbenchmarks executed.
- Three core NuGet packages generated and consumed successfully by a separate clean-cache console application using package references only.
- Structural verification passed for all 14 project references, .NET 10 targets, centralized dependency versions and renamed production namespaces.
- An EF restore against an empty offline feed failed with NU1101, confirming the required EF package is unavailable in the current sandbox. Native NuGet DNS and service-side package transport did not provide the required bytes.

Fresh final build/test/package commands and artifact hashes are recorded in the accompanying validation files and distribution manifest.

## Not executed / blocked

- Exact Newtonsoft 13.0.4 NuGet restore/build/test.
- EF Core 10.0.12 restore/build and the 9 provider/proxy integration cases.
- Retained upstream NUnit suites and external JSONata/JSON-test-suite git-submodule corpora. Some upstream assertions describe intentionally changed defaults, and external corpus fixtures were missing from the supplied ZIP.
- Windows/macOS CI runs, current dependency vulnerability audit, full conformance, production provider benchmarks, trimming/Native AOT, hard isolation or deployment validation.

## How to reproduce

With a stable .NET 10 SDK and normal package access:

```sh
./scripts/verify.sh
# PowerShell: ./scripts/verify.ps1
dotnet pack Projecta.slnx -c Release --no-build -o artifacts/packages
```

Core-only checks are `./scripts/verify.sh --core-only`; never report that subset as EF/Newtonsoft dependency validation. Verification executables use `dotnet run`; do not mistake a zero-test `dotnet test` invocation for their execution.

## Verdict

**Source deliverable with executed core evidence; full integration/production gate blocked.** The ZIP contains all implementation sources, tests and dependency declarations needed for the new standalone project. It is not a claim that every optional integration has compiled or that all security/performance risks are eliminated.
