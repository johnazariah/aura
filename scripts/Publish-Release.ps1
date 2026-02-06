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

    # Copy prompts
    Write-Host "`nCopying prompts..." -ForegroundColor Green
    Copy-Item -Path "prompts" -Destination "$OutputDir/win-x64/prompts" -Recurse

    # Copy patterns
    Write-Host "`nCopying patterns..." -ForegroundColor Green
    Copy-Item -Path "patterns" -Destination "$OutputDir/win-x64/patterns" -Recurse

    # Build VS Code extension
    Write-Host "`nBuilding VS Code Extension..." -ForegroundColor Green
    Push-Location extension
    
    # Update extension version to match release version
    $packageJsonPath = "package.json"
    $packageJson = Get-Content $packageJsonPath -Raw | ConvertFrom-Json
    $packageJson.version = $Version
    $packageJson | ConvertTo-Json -Depth 10 | Set-Content $packageJsonPath
    
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "Failed to install extension dependencies" }
    
    npm run package
    if ($LASTEXITCODE -ne 0) { throw "Failed to package extension" }
    
    # Create VSIX package using vsce
    npx @vscode/vsce package --out "aura-$Version.vsix"
    if ($LASTEXITCODE -ne 0) { throw "Failed to create VSIX package" }
    Pop-Location
    
    # Copy VSIX to publish folder
    New-Item -ItemType Directory -Path "$OutputDir/win-x64/extension" -Force | Out-Null
    Copy-Item "extension/aura-$Version.vsix" "$OutputDir/win-x64/extension/"
    
    # Copy scripts for installer
    New-Item -ItemType Directory -Path "$OutputDir/win-x64/scripts" -Force | Out-Null
    Copy-Item "installers/windows/install-extension.ps1" "$OutputDir/win-x64/scripts/"
    Copy-Item "scripts/Create-ServiceAccount.ps1" "$OutputDir/win-x64/scripts/"
    Copy-Item "scripts/Get-ServiceAccountCredential.ps1" "$OutputDir/win-x64/scripts/"
    Copy-Item "scripts/Install-AuraService.ps1" "$OutputDir/win-x64/scripts/"

    # Copy language tool scripts
    New-Item -ItemType Directory -Path "$OutputDir/win-x64/scripts/typescript/dist" -Force | Out-Null
    Copy-Item "scripts/typescript/dist/*" "$OutputDir/win-x64/scripts/typescript/dist/"
    New-Item -ItemType Directory -Path "$OutputDir/win-x64/scripts/python" -Force | Out-Null
    Copy-Item "scripts/python/refactor.py" "$OutputDir/win-x64/scripts/python/"
    if (Test-Path "scripts/python/requirements.txt") {
        Copy-Item "scripts/python/requirements.txt" "$OutputDir/win-x64/scripts/python/"
    }

    # Download and bundle PostgreSQL
    Write-Host "`nPreparing PostgreSQL..." -ForegroundColor Green
    $pgVersion = "16.4-1"
    $pgZip = "postgresql-$pgVersion-windows-x64-binaries.zip"
    $pgUrl = "https://get.enterprisedb.com/postgresql/$pgZip"
    $cacheDir = "cache"
    
    if (-not (Test-Path $cacheDir)) {
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
    }
    
    if (-not (Test-Path "$cacheDir/$pgZip")) {
        Write-Host "  Downloading PostgreSQL $pgVersion (~100MB)..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pgUrl -OutFile "$cacheDir/$pgZip"
    } else {
        Write-Host "  Using cached PostgreSQL $pgVersion" -ForegroundColor Gray
    }
    
    # Extract PostgreSQL binaries
    Write-Host "  Extracting PostgreSQL..." -ForegroundColor Gray
    $pgTempDir = "$cacheDir/pgsql-temp"
    if (Test-Path $pgTempDir) { Remove-Item $pgTempDir -Recurse -Force }
    Expand-Archive "$cacheDir/$pgZip" -DestinationPath $pgTempDir -Force
    
    # Copy only the pgsql folder (not the full extracted structure)
    $pgSourceDir = Get-ChildItem $pgTempDir -Directory | Where-Object { $_.Name -eq "pgsql" } | Select-Object -First 1
    if (-not $pgSourceDir) {
        $pgSourceDir = Get-ChildItem $pgTempDir -Directory | Select-Object -First 1
    }
    
    New-Item -ItemType Directory -Path "$OutputDir/win-x64/pgsql" -Force | Out-Null
    Copy-Item -Path "$($pgSourceDir.FullName)/*" -Destination "$OutputDir/win-x64/pgsql" -Recurse -Force
    
    # Remove pgAdmin 4 - we don't need a web GUI, saves ~500MB
    $pgAdminDir = "$OutputDir/win-x64/pgsql/pgAdmin 4"
    if (Test-Path $pgAdminDir) {
        Write-Host "  Removing pgAdmin 4 (not needed, saves ~500MB)..." -ForegroundColor Gray
        Remove-Item $pgAdminDir -Recurse -Force
    }
    
    # Remove other unnecessary folders to reduce installer size
    foreach ($unneeded in @("symbols", "doc", "include", "StackBuilder")) {
        $unneededDir = "$OutputDir/win-x64/pgsql/$unneeded"
        if (Test-Path $unneededDir) {
            Write-Host "  Removing $unneeded (not needed for runtime)..." -ForegroundColor Gray
            Remove-Item $unneededDir -Recurse -Force
        }
    }
    
    # Download and bundle pgvector extension
    Write-Host "`nPreparing pgvector extension..." -ForegroundColor Green
    $pgvectorZip = "pgvector-pg16.zip"
    $pgvectorUrl = "https://github.com/andreiramani/pgvector_pgsql_windows/releases/download/0.8.1_16/vector.v0.8.1-pg16.zip"
    
    if (-not (Test-Path "$cacheDir/$pgvectorZip")) {
        Write-Host "  Downloading pgvector 0.8.1 for PostgreSQL 16..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pgvectorUrl -OutFile "$cacheDir/$pgvectorZip"
    } else {
        Write-Host "  Using cached pgvector" -ForegroundColor Gray
    }
    
    # Extract pgvector
    $pgvectorTempDir = "$cacheDir/pgvector-temp"
    if (Test-Path $pgvectorTempDir) { Remove-Item $pgvectorTempDir -Recurse -Force }
    Expand-Archive "$cacheDir/$pgvectorZip" -DestinationPath $pgvectorTempDir -Force
    
    # Copy pgvector files to PostgreSQL directories
    Write-Host "  Bundling pgvector extension..." -ForegroundColor Gray
    Copy-Item "$pgvectorTempDir/lib/vector.dll" "$OutputDir/win-x64/pgsql/lib/"
    Copy-Item "$pgvectorTempDir/share/extension/vector.control" "$OutputDir/win-x64/pgsql/share/extension/"
    Copy-Item "$pgvectorTempDir/share/extension/vector--*.sql" "$OutputDir/win-x64/pgsql/share/extension/"

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
    Write-Host "  - prompts/ (Prompt templates)" -ForegroundColor Gray
    Write-Host "  - extension/aura-$Version.vsix (VS Code Extension)" -ForegroundColor Gray
    Write-Host "  - scripts/install-extension.ps1 (Manual install helper)" -ForegroundColor Gray
    Write-Host "  - pgsql/ (PostgreSQL $pgVersion)" -ForegroundColor Gray

} finally {
    Pop-Location
}
