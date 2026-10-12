# Validation report - Projecta 0.1.0

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

## Post-distribution build repair (2026-10-09)

A Windows `scripts/verify.ps1` build supplied by a consumer reported 8 compile
errors: three CS0118 errors in `ObjectParsingTests.cs` where the `TestData`
namespace was used like a type, and five CS0121 ambiguous overload calls in
`Projecta.EntityFrameworkCore.Tests/Program.cs` using target-typed `new()`.

Four test source files were updated: the three `ObjectParsingTests.cs` suites
now use an explicit alias for `global::Projecta.TestData.TestData`, and all five
EF Core calls now explicitly construct `ProjectionDefinition`. No production
code, package versions or public API signatures were changed.

**Validation for this repair:** static verification of the eight replacements,
archive file integrity and manifest checks were executed. A fresh .NET 10
build/test was **not executed** in the repair sandbox because the SDK was not
available and direct SDK download DNS was blocked. Consumer Windows compilation
and runtime test verification remain pending; the earlier evidence in this
document applies to the previous source snapshot only.

Verify on a machine with the .NET 10 SDK:

```powershell
./scripts/verify.ps1
dotnet test tests/Projecta.Upstream.Tests/Projecta.Upstream.Tests.csproj -c Release
dotnet test tests/Projecta.SystemTextJson.Tests/Projecta.SystemTextJson.Tests.csproj -c Release
dotnet test tests/Projecta.NewtonsoftJson.Tests/Projecta.NewtonsoftJson.Tests.csproj -c Release
```

Note: `verify.ps1` compiles the NUnit suites but does not execute them; the
explicit `dotnet test` commands above cover them. Upstream assertion changes
may require independent evaluation rather than modifying production behavior
to match older assumptions.
