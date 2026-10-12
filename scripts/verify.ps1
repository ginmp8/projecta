param([switch]$CoreOnly, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    function Invoke-Dotnet {
        & dotnet @args
        if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
    }
    $solution = if ($CoreOnly) { 'Projecta.Core.slnx' } else { 'Projecta.slnx' }
    New-Item -ItemType Directory -Force artifacts/validation | Out-Null
    if ($NoRestore) { Invoke-Dotnet build $solution -c Release --no-restore }
    else { Invoke-Dotnet build $solution -c Release }
    Invoke-Dotnet run --project tests/Projecta.Tests -c Release --no-build -- --out artifacts/validation/core-tests.json
    Invoke-Dotnet run --project tests/Projecta.Evolution.Tests -c Release --no-build -- --out artifacts/validation/evolution-tests.json
    Invoke-Dotnet run --project tests/Projecta.Semantics.Tests -c Release --no-build -- --out artifacts/validation/semantics.json
    Invoke-Dotnet run --project tests/Projecta.Generated.Tests -c Release --no-build
    Invoke-Dotnet run --project samples/Projecta.Sample -c Release --no-build
    Invoke-Dotnet run --project src/Projecta.Tooling -c Release --no-build -- help
    if (!$CoreOnly) {
        Invoke-Dotnet run --project tests/Projecta.NewtonsoftIntegration.Tests -c Release --no-build
        Invoke-Dotnet run --project tests/Projecta.EntityFrameworkCore.Tests -c Release --no-build
        Invoke-Dotnet run --project tests/Projecta.Relational.Tests -c Release --no-build
        foreach ($project in @('Projecta.Compatibility.Tests', 'Projecta.SystemTextJson.Tests', 'Projecta.NewtonsoftJson.Tests')) {
            $trx = "$project.trx"
            $path = Join-Path 'artifacts/validation' $trx
            if (Test-Path $path) { Remove-Item $path }
            Invoke-Dotnet test "tests/$project/$project.csproj" -c Release --no-build `
                --logger "trx;LogFileName=$trx" --results-directory artifacts/validation `
                '--' RunConfiguration.TreatNoTestsAsError=true
            [xml]$result = Get-Content -LiteralPath $path -Raw
            $counters = $result.SelectSingleNode("//*[local-name()='Counters']")
            if ($null -eq $counters -or [int]$counters.executed -le 0 -or [int]$counters.failed -ne 0) {
                throw "Invalid, empty or failed test run: $project"
            }
        }
    }
    if ($CoreOnly) { Write-Output 'PASS: package-free verification. Optional integrations and NUnit were not run.' }
    else { Write-Output 'PASS: full build and configured verification suites.' }
} finally { Pop-Location }
