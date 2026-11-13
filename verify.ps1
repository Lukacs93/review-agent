#!/usr/bin/env pwsh
# Runs the same checks as CI: restore, build (warnings as errors), tests and formatting.
# Usage: ./verify.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string]$name, [scriptblock]$command) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $name" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Invoke-Step 'Restore' { dotnet restore }
Invoke-Step 'Build'   { dotnet build --no-restore --configuration Release }
Invoke-Step 'Test'    { dotnet test --no-build --configuration Release }
Invoke-Step 'Format'  { dotnet format --no-restore --verify-no-changes }

Write-Host 'All checks passed.' -ForegroundColor Green
