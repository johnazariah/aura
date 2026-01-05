#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build Aura Windows installer
.DESCRIPTION
    Builds the MSI/EXE installer using Inno Setup
.PARAMETER Version
    Version string (default: 1.0.0)
.EXAMPLE
    .\scripts\Build-Installer.ps1 -Version "1.0.0"
#>

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    Write-Host "Building Aura Installer $Version" -ForegroundColor Cyan
    Write-Host "=" * 50 -ForegroundColor Cyan

    # Check for Inno Setup in various locations
    $isccPaths = @(
        "iscc",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    
    $iscc = $null
    foreach ($path in $isccPaths) {
        $cmd = Get-Command $path -ErrorAction SilentlyContinue
        if ($cmd) {
            $iscc = $cmd
            break
        }
    }
    
    if (-not $iscc) {
        Write-Host "Inno Setup not found. Install from: https://jrsoftware.org/isdl.php" -ForegroundColor Red
        Write-Host "Or: winget install -e --id JRSoftware.InnoSetup" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "Using Inno Setup: $($iscc.Source)" -ForegroundColor Gray

    # Publish first
    Write-Host "`nStep 1: Publishing release..." -ForegroundColor Green
    & "$PSScriptRoot\Publish-Release.ps1" -Version $Version
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

    # Create installer output directory
    New-Item -ItemType Directory -Path "publish/installers" -Force | Out-Null

    # Build installer
    Write-Host "`nStep 2: Building installer..." -ForegroundColor Green
    & $iscc.Source "/DMyAppVersion=$Version" "installers/windows/Aura.iss"
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

    Write-Host "`n✓ Installer created: publish/installers/Aura-Setup-$Version.exe" -ForegroundColor Green

} finally {
    Pop-Location
}
