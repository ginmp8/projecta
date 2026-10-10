param([switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $solution = if ($CoreOnly) { 'Projecta.Core.slnx' } else { 'Projecta.slnx' }
    & dotnet pack $solution -c Release --no-build -o artifacts/packages
    if ($LASTEXITCODE -ne 0) { throw "pack failed with exit code $LASTEXITCODE" }
} finally { Pop-Location }
