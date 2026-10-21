# Cooks the content described in Cook.ini (see Cook.ini for content path, pak output, engine dir, globs).
# Run from anywhere: .\cook.ps1   (extra args go to the cooker and override Cook.ini, e.g. .\cook.ps1 -v --no-cache)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot   # solution root

dotnet run --project OssianCooker -c Release -- cook `
    --ini "$PSScriptRoot\Cook.ini" `
    @args

if ($LASTEXITCODE -ne 0) {
    Write-Host "Cook failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Cook finished (output path: OutputPath in Cook.ini)" -ForegroundColor Green