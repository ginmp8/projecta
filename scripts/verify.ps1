param([switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Invoke-Dotnet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" } }
if ($CoreOnly) { Invoke-Dotnet build src/Projecta/Projecta.csproj -c Release }
else { Invoke-Dotnet build Projecta.slnx -c Release }
New-Item -ItemType Directory -Force artifacts/validation | Out-Null
Invoke-Dotnet run --project tests/Projecta.Tests -c Release -- --out artifacts/validation/core-tests.json
Invoke-Dotnet run --project samples/Projecta.Sample -c Release
if (!$CoreOnly) {
    Invoke-Dotnet run --project tests/Projecta.NewtonsoftIntegration.Tests -c Release
    Invoke-Dotnet run --project tests/Projecta.EntityFrameworkCore.Tests -c Release
}
