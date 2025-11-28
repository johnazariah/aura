#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish Aura for production deployment
.DESCRIPTION
    Creates self-contained builds for Aura.Api and Aura.Tray
.PARAMETER Version
    Version string (default: 1.0.0)
.PARAMETER OutputDir
    Output directory (default: publish)
.EXAMPLE
    .\scripts\Publish-Release.ps1 -Version "1.0.0"
#>

param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing Aura $Version" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    # Clean output
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    # Publish Aura.Api (Windows Service)
    Write-Host "`nPublishing Aura.Api..." -ForegroundColor Green
    dotnet publish src/Aura.Api/Aura.Api.csproj `
        -c Release `
        -r win-x64 `
        -p:PublishSelfContained=true `
        -p:Version=$Version `
        -o "$OutputDir/win-x64/api"

    if ($LASTEXITCODE -ne 0) { throw "Failed to publish Aura.Api" }

    # Publish Aura.Tray
    Write-Host "`nPublishing Aura.Tray..." -ForegroundColor Green
    dotnet publish src/Aura.Tray/Aura.Tray.csproj `
        -c Release `
        -r win-x64 `
        -p:PublishSelfContained=true `
        -p:Version=$Version `
        -o "$OutputDir/win-x64/tray"

    if ($LASTEXITCODE -ne 0) { throw "Failed to publish Aura.Tray" }

    # Copy agents
    Write-Host "`nCopying agents..." -ForegroundColor Green
    Copy-Item -Path "agents" -Destination "$OutputDir/win-x64/agents" -Recurse

    # Create version file
    @{
        version = $Version
        buildDate = (Get-Date -Format "o")
        commit = (git rev-parse --short HEAD 2>$null) ?? "unknown"
    } | ConvertTo-Json | Set-Content "$OutputDir/win-x64/version.json"

    Write-Host "`n✓ Published to $OutputDir/win-x64" -ForegroundColor Green
    Write-Host "  - api/Aura.Api.exe (Windows Service)" -ForegroundColor Gray
    Write-Host "  - tray/Aura.Tray.exe (System Tray)" -ForegroundColor Gray
    Write-Host "  - agents/ (Agent definitions)" -ForegroundColor Gray

} finally {
    Pop-Location
}
