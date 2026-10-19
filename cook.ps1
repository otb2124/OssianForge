# Cooks Content/ into bin/Debug/net8.0/Content.pak
# Run from anywhere: .\cook.ps1   (extra args are passed to the cooker, e.g. .\cook.ps1 -v --no-cache)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot   # solution root, so relative paths below resolve

dotnet run --project OssianCooker -c Release -- cook `
    --content Content `
    --engine-dir bin/Debug/net8.0 `
    --out bin/Debug/net8.0/Content.pak `
    @args

if ($LASTEXITCODE -ne 0) {
    Write-Host "Cook failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Cook finished: bin/Debug/net8.0/Content.pak" -ForegroundColor Green