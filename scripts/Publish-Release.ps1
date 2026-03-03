#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish Aura for production deployment
.DESCRIPTION
    Creates self-contained builds for Aura.Api and Aura.Tray.
    Supports Windows (win-x64) and macOS (osx-arm64) targets.
.PARAMETER Version
    Version string (default: 1.0.0)
.PARAMETER Runtime
    Target runtime: win-x64, osx-arm64, or all (default: win-x64)
.PARAMETER OutputDir
    Output directory (default: publish)
.PARAMETER SkipPostgres
    Skip PostgreSQL download/bundling (for CI where PG is separate)
.EXAMPLE
    .\scripts\Publish-Release.ps1 -Version "1.0.0"
    .\scripts\Publish-Release.ps1 -Version "1.0.0" -Runtime osx-arm64
    .\scripts\Publish-Release.ps1 -Version "1.0.0" -Runtime all
#>

param(
    [string]$Version = "1.0.0",
    [ValidateSet("win-x64", "osx-arm64", "all")]
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish",
    [switch]$SkipPostgres
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing Aura $Version ($Runtime)" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

function Publish-Platform {
    param([string]$rid)

    $platformDir = "$OutputDir/$rid"

    # Publish Aura.Api
    Write-Host "`nPublishing Aura.Api ($rid)..." -ForegroundColor Green
    dotnet publish src/Aura.Api/Aura.Api.csproj `
        -c Release `
        -r $rid `
        -p:PublishSelfContained=true `
        -p:Version=$Version `
        -o "$platformDir/api"

    if ($LASTEXITCODE -ne 0) { throw "Failed to publish Aura.Api ($rid)" }

    # Publish Aura.Tray
    Write-Host "`nPublishing Aura.Tray ($rid)..." -ForegroundColor Green
    dotnet publish src/Aura.Tray/Aura.Tray.csproj `
        -c Release `
        -r $rid `
        -p:PublishSelfContained=true `
        -p:Version=$Version `
        -o "$platformDir/tray"

    if ($LASTEXITCODE -ne 0) { throw "Failed to publish Aura.Tray ($rid)" }

    # Copy patterns
    if (Test-Path "patterns") {
        Write-Host "`nCopying patterns..." -ForegroundColor Green
        Copy-Item -Path "patterns" -Destination "$platformDir/patterns" -Recurse
    }

    # Copy language tool scripts
    New-Item -ItemType Directory -Path "$platformDir/scripts" -Force | Out-Null
    if (Test-Path "scripts/typescript/dist") {
        New-Item -ItemType Directory -Path "$platformDir/scripts/typescript/dist" -Force | Out-Null
        Copy-Item "scripts/typescript/dist/*" "$platformDir/scripts/typescript/dist/"
        if (Test-Path "scripts/typescript/node_modules") {
            Copy-Item -Path "scripts/typescript/node_modules" -Destination "$platformDir/scripts/typescript/node_modules" -Recurse
        }
    }
    if (Test-Path "scripts/python/refactor.py") {
        New-Item -ItemType Directory -Path "$platformDir/scripts/python" -Force | Out-Null
        Copy-Item "scripts/python/refactor.py" "$platformDir/scripts/python/"
        if (Test-Path "scripts/python/requirements.txt") {
            Copy-Item "scripts/python/requirements.txt" "$platformDir/scripts/python/"
        }
    }

    # Create version file
    @{
        version = $Version
        runtime = $rid
        buildDate = (Get-Date -Format "o")
        commit = (git rev-parse --short HEAD 2>$null) ?? "unknown"
    } | ConvertTo-Json | Set-Content "$platformDir/version.json"

    Write-Host "`n✓ Published to $platformDir" -ForegroundColor Green
    Write-Host "  - api/ (API + MCP Server)" -ForegroundColor Gray
    Write-Host "  - tray/ (System Tray)" -ForegroundColor Gray
}

function Bundle-WindowsPostgres {
    param([string]$platformDir)

    $cacheDir = "cache"
    if (-not (Test-Path $cacheDir)) {
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
    }

    # Download PostgreSQL
    Write-Host "`nPreparing PostgreSQL..." -ForegroundColor Green
    $pgVersion = "16.4-1"
    $pgZip = "postgresql-$pgVersion-windows-x64-binaries.zip"
    $pgUrl = "https://get.enterprisedb.com/postgresql/$pgZip"

    if (-not (Test-Path "$cacheDir/$pgZip")) {
        Write-Host "  Downloading PostgreSQL $pgVersion (~100MB)..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pgUrl -OutFile "$cacheDir/$pgZip"
    } else {
        Write-Host "  Using cached PostgreSQL $pgVersion" -ForegroundColor Gray
    }

    Write-Host "  Extracting PostgreSQL..." -ForegroundColor Gray
    $pgTempDir = "$cacheDir/pgsql-temp"
    if (Test-Path $pgTempDir) { Remove-Item $pgTempDir -Recurse -Force }
    Expand-Archive "$cacheDir/$pgZip" -DestinationPath $pgTempDir -Force

    $pgSourceDir = Get-ChildItem $pgTempDir -Directory | Where-Object { $_.Name -eq "pgsql" } | Select-Object -First 1
    if (-not $pgSourceDir) {
        $pgSourceDir = Get-ChildItem $pgTempDir -Directory | Select-Object -First 1
    }

    New-Item -ItemType Directory -Path "$platformDir/pgsql" -Force | Out-Null
    Copy-Item -Path "$($pgSourceDir.FullName)/*" -Destination "$platformDir/pgsql" -Recurse -Force

    # Remove unnecessary folders
    foreach ($unneeded in @("pgAdmin 4", "symbols", "doc", "include", "StackBuilder")) {
        $unneededDir = "$platformDir/pgsql/$unneeded"
        if (Test-Path $unneededDir) {
            Write-Host "  Removing $unneeded..." -ForegroundColor Gray
            Remove-Item $unneededDir -Recurse -Force
        }
    }

    # Download pgvector
    Write-Host "`nPreparing pgvector extension..." -ForegroundColor Green
    $pgvectorZip = "pgvector-pg16.zip"
    $pgvectorUrl = "https://github.com/andreiramani/pgvector_pgsql_windows/releases/download/0.8.1_16/vector.v0.8.1-pg16.zip"

    if (-not (Test-Path "$cacheDir/$pgvectorZip")) {
        Write-Host "  Downloading pgvector 0.8.1..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pgvectorUrl -OutFile "$cacheDir/$pgvectorZip"
    } else {
        Write-Host "  Using cached pgvector" -ForegroundColor Gray
    }

    $pgvectorTempDir = "$cacheDir/pgvector-temp"
    if (Test-Path $pgvectorTempDir) { Remove-Item $pgvectorTempDir -Recurse -Force }
    Expand-Archive "$cacheDir/$pgvectorZip" -DestinationPath $pgvectorTempDir -Force

    Copy-Item "$pgvectorTempDir/lib/vector.dll" "$platformDir/pgsql/lib/"
    Copy-Item "$pgvectorTempDir/share/extension/vector.control" "$platformDir/pgsql/share/extension/"
    Copy-Item "$pgvectorTempDir/share/extension/vector--*.sql" "$platformDir/pgsql/share/extension/"

    # Copy diagnostic script
    Copy-Item "installers/windows/Diagnose-Aura.ps1" "$platformDir/scripts/" -ErrorAction SilentlyContinue
}

try {
    # Clean output
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    $runtimes = if ($Runtime -eq "all") { @("win-x64", "osx-arm64") } else { @($Runtime) }

    foreach ($rid in $runtimes) {
        Publish-Platform -rid $rid

        # Bundle PostgreSQL for Windows only
        if ($rid -eq "win-x64" -and -not $SkipPostgres) {
            Bundle-WindowsPostgres -platformDir "$OutputDir/$rid"
        }
    }

    Write-Host "`n✓ All platforms published successfully" -ForegroundColor Green

} finally {
    Pop-Location
}
