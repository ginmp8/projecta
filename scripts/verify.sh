#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."
if [ "${1:-}" = "--core-only" ]; then
  dotnet build src/Projecta/Projecta.csproj -c Release
else
  dotnet build Projecta.slnx -c Release
fi
mkdir -p artifacts/validation
dotnet run --project tests/Projecta.Tests -c Release -- --out artifacts/validation/core-tests.json
dotnet run --project samples/Projecta.Sample -c Release
if [ "${1:-}" != "--core-only" ]; then
  dotnet run --project tests/Projecta.NewtonsoftIntegration.Tests -c Release
  dotnet run --project tests/Projecta.EntityFrameworkCore.Tests -c Release
fi
