#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update a local Aura installation with the current dev build
.DESCRIPTION
    Builds and deploys the current source to an installed Aura instance.
    Useful for testing new features without going through a full release cycle.
.PARAMETER InstallPath
    Path to the Aura installation (default: C:\Program Files\Aura)
.PARAMETER SkipApi
    Skip updating the API service
.PARAMETER SkipExtension
    Skip updating the VS Code extension
.PARAMETER SkipAgents
    Skip updating agents
.PARAMETER StopService
    Stop the AuraService before updating (default: true)
.EXAMPLE
    .\scripts\Update-LocalInstall.ps1
    
    Updates all components in the default install location
.EXAMPLE
    .\scripts\Update-LocalInstall.ps1 -SkipExtension
    
    Updates API and agents only
#>

param(
    [string]$InstallPath = "C:\Program Files\Aura",
    [switch]$SkipApi,
    [switch]$SkipExtension,
    [switch]$SkipAgents,
    [switch]$StopService = $true
)

$ErrorActionPreference = "Stop"

function Write-Header($message) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " $message" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

function Write-Step($message) {
    Write-Host ">> $message" -ForegroundColor Green
}

function Write-Skip($message) {
    Write-Host "-- Skipping $message" -ForegroundColor DarkGray
}

# =============================================================================
# Validate Installation
# =============================================================================
Write-Header "Validating Aura Installation"

if (-not (Test-Path $InstallPath)) {
    throw "Aura installation not found at $InstallPath. Run the installer first."
}

$apiPath = Join-Path $InstallPath "api"
$agentsPath = Join-Path $InstallPath "agents"
$extensionPath = Join-Path $InstallPath "extension"

if (-not (Test-Path $apiPath)) {
    throw "API directory not found at $apiPath"
}

Write-Step "Found Aura installation at $InstallPath"

$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    # =============================================================================
    # Stop Services
    # =============================================================================
    if ($StopService -and -not $SkipApi) {
        Write-Header "Stopping Aura Services"
        
        # Stop Windows Service if exists
        $service = Get-Service -Name "AuraService" -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq "Running") {
            Write-Step "Stopping AuraService..."
            Stop-Service -Name "AuraService" -Force
            Start-Sleep -Seconds 2
        }
        
        # Stop tray app if running
        $tray = Get-Process -Name "Aura.Tray" -ErrorAction SilentlyContinue
        if ($tray) {
            Write-Step "Stopping Aura.Tray..."
            Stop-Process -Name "Aura.Tray" -Force
        }
        
        # Stop API process if running standalone
        $api = Get-Process -Name "Aura.Api" -ErrorAction SilentlyContinue
        if ($api) {
            Write-Step "Stopping Aura.Api..."
            Stop-Process -Name "Aura.Api" -Force
        }
    }

    # =============================================================================
    # Build and Deploy API
    # =============================================================================
    if (-not $SkipApi) {
        Write-Header "Building Aura.Api"
        
        dotnet publish src/Aura.Api/Aura.Api.csproj `
            -c Release `
            -r win-x64 `
            -p:PublishSelfContained=true `
            -o ".update-temp/api"
        
        if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Api" }
        
        Write-Step "Deploying to $apiPath..."
        
        # Copy new files (preserving appsettings.json if customized)
        $files = Get-ChildItem ".update-temp/api" -Recurse
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring((Resolve-Path ".update-temp/api").Path.Length + 1)
            $destPath = Join-Path $apiPath $relativePath
            
            # Skip appsettings.json if it exists in destination
            if ($relativePath -eq "appsettings.json" -and (Test-Path $destPath)) {
                Write-Host "   Preserving existing appsettings.json" -ForegroundColor Yellow
                continue
            }
            
            $destDir = Split-Path $destPath -Parent
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            
            Copy-Item $file.FullName $destPath -Force
        }
        
        Write-Step "API updated"
    } else {
        Write-Skip "API"
    }

    # =============================================================================
    # Deploy Agents
    # =============================================================================
    if (-not $SkipAgents) {
        Write-Header "Deploying Agents"
        
        # Copy agents directory
        if (Test-Path $agentsPath) {
            Remove-Item $agentsPath -Recurse -Force
        }
        Copy-Item -Path "agents" -Destination $agentsPath -Recurse
        
        Write-Step "Agents updated"
    } else {
        Write-Skip "Agents"
    }

    # =============================================================================
    # Build and Deploy Extension
    # =============================================================================
    if (-not $SkipExtension) {
        Write-Header "Building VS Code Extension"
        
        Push-Location extension
        
        # Build extension
        npm run package 2>$null
        if ($LASTEXITCODE -ne 0) { 
            npm ci
            npm run package
        }
        
        # Package as VSIX
        $version = (Get-Content package.json -Raw | ConvertFrom-Json).version
        npx @vscode/vsce package --out "aura-dev.vsix" 2>$null
        
        if ($LASTEXITCODE -ne 0) { throw "Failed to package extension" }
        
        Pop-Location
        
        Write-Step "Installing extension..."
        code --install-extension "extension/aura-dev.vsix" --force
        
        if ($LASTEXITCODE -ne 0) { throw "Failed to install extension" }
        
        Write-Step "Extension updated - reload VS Code to activate"
    } else {
        Write-Skip "Extension"
    }

    # =============================================================================
    # Restart Services
    # =============================================================================
    if ($StopService -and -not $SkipApi) {
        Write-Header "Restarting Aura Services"
        
        $service = Get-Service -Name "AuraService" -ErrorAction SilentlyContinue
        if ($service) {
            Write-Step "Starting AuraService..."
            Start-Service -Name "AuraService"
        } else {
            Write-Step "Starting Aura.Api manually..."
            Start-Process -FilePath (Join-Path $apiPath "Aura.Api.exe") -WindowStyle Hidden
        }
        
        # Wait for API to be ready
        Write-Step "Waiting for API..."
        $maxAttempts = 30
        for ($i = 0; $i -lt $maxAttempts; $i++) {
            try {
                Invoke-RestMethod -Uri "http://localhost:5300/health" -ErrorAction Stop | Out-Null
                Write-Step "API is ready!"
                break
            } catch {
                Start-Sleep -Seconds 1
            }
        }
    }

    # =============================================================================
    # Cleanup
    # =============================================================================
    if (Test-Path ".update-temp") {
        Remove-Item ".update-temp" -Recurse -Force
    }

    Write-Header "Update Complete!"
    Write-Host ""
    Write-Host "Updated components:" -ForegroundColor Green
    if (-not $SkipApi) { Write-Host "  - API service" }
    if (-not $SkipAgents) { Write-Host "  - Agents" }
    if (-not $SkipExtension) { Write-Host "  - VS Code extension (reload VS Code to activate)" }
    Write-Host ""

} finally {
    Pop-Location
}
