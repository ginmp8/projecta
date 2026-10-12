#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
core_only=false
no_restore=false
for arg in "$@"; do
  case "$arg" in
    --core-only) core_only=true ;;
    --no-restore) no_restore=true ;;
    *) echo "Usage: scripts/verify.sh [--core-only] [--no-restore]" >&2; exit 2 ;;
  esac
done
solution=Projecta.slnx
if "$core_only"; then solution=Projecta.Core.slnx; fi
mkdir -p artifacts/validation
if "$no_restore"; then dotnet build "$solution" -c Release --no-restore
else dotnet build "$solution" -c Release; fi
dotnet run --project tests/Projecta.Tests -c Release --no-build -- --out artifacts/validation/core-tests.json
dotnet run --project tests/Projecta.Evolution.Tests -c Release --no-build -- --out artifacts/validation/evolution-tests.json
dotnet run --project tests/Projecta.Semantics.Tests -c Release --no-build -- --out artifacts/validation/semantics.json
dotnet run --project tests/Projecta.Generated.Tests -c Release --no-build
dotnet run --project samples/Projecta.Sample -c Release --no-build
dotnet run --project src/Projecta.Tooling -c Release --no-build -- help
if ! "$core_only"; then
  dotnet run --project tests/Projecta.NewtonsoftIntegration.Tests -c Release --no-build
  dotnet run --project tests/Projecta.EntityFrameworkCore.Tests -c Release --no-build
  dotnet run --project tests/Projecta.Relational.Tests -c Release --no-build
  for project in Projecta.Compatibility.Tests Projecta.SystemTextJson.Tests Projecta.NewtonsoftJson.Tests; do
    dotnet test "tests/$project/$project.csproj" -c Release --no-build \
      --logger "trx;LogFileName=$project.trx" --results-directory artifacts/validation \
      -- RunConfiguration.TreatNoTestsAsError=true
  done
fi
if "$core_only"; then echo 'PASS: package-free verification. Optional integrations and NUnit were not run.'
else echo 'PASS: full build and configured verification suites.'; fi
