#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick deploy of Aura from source for development.
.DESCRIPTION
    Stops the running AuraService, publishes Aura.Api from the current branch
    to the install directory, restarts the service, and ensures the global
    Copilot MCP config is set up.

    Must be run as Administrator (service operations require elevation).
.PARAMETER SkipBuild
    Skip dotnet publish and only restart the service (useful after manual build).
.EXAMPLE
    # From elevated PowerShell:
    .\scripts\Deploy-Dev.ps1

    # Skip build (just restart with existing publish output):
    .\scripts\Deploy-Dev.ps1 -SkipBuild
#>

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$installDir = "C:\Program Files\Aura"
$root = Split-Path $PSScriptRoot -Parent

# Check elevation
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: Must run as Administrator (service operations require elevation)" -ForegroundColor Red
    exit 1
}

Write-Host "=== Aura Dev Deploy ===" -ForegroundColor Cyan
Write-Host "Source: $root" -ForegroundColor Gray
Write-Host "Target: $installDir" -ForegroundColor Gray

# 1. Stop the service
Write-Host "`n[1/4] Stopping AuraService..." -ForegroundColor Yellow
sc.exe stop AuraService 2>$null | Out-Null
Start-Sleep -Seconds 2

# Kill any lingering processes
Get-Process -Name "Aura.Api" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Killing lingering Aura.Api process (PID $($_.Id))" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force
}
Get-Process -Name "Aura.Tray" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Killing lingering Aura.Tray process (PID $($_.Id))" -ForegroundColor Gray
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Seconds 1

# 2. Publish
if (-not $SkipBuild) {
    Write-Host "`n[2/4] Publishing Aura.Api..." -ForegroundColor Yellow
    Push-Location $root

    dotnet publish src/Aura.Api/Aura.Api.csproj `
        -c Release `
        -r win-x64 `
        -p:PublishSelfContained=true `
        -o "$installDir\api" `
        --nologo -v q

    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        throw "Failed to publish Aura.Api"
    }

    # Also publish tray app
    Write-Host "  Publishing Aura.Tray..." -ForegroundColor Gray
    dotnet publish src/Aura.Tray/Aura.Tray.csproj `
        -c Release `
        -r win-x64 `
        -p:PublishSelfContained=true `
        -o "$installDir\tray" `
        --nologo -v q

    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        throw "Failed to publish Aura.Tray"
    }

    Pop-Location
    Write-Host "  Published successfully" -ForegroundColor Green
} else {
    Write-Host "`n[2/4] Skipping build (--SkipBuild)" -ForegroundColor Gray
}

# 3. Start the service
Write-Host "`n[3/4] Starting AuraService..." -ForegroundColor Yellow
sc.exe start AuraService | Out-Null
Start-Sleep -Seconds 3

# Verify health
$maxRetries = 10
$healthy = $false
for ($i = 0; $i -lt $maxRetries; $i++) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5300/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.status -eq "healthy") {
            $healthy = $true
            break
        }
    } catch { }
    Start-Sleep -Seconds 1
}

if ($healthy) {
    Write-Host "  AuraService is healthy" -ForegroundColor Green

    # Show MCP tools
    try {
        $mcp = Invoke-RestMethod -Uri "http://localhost:5300/health/mcp" -TimeoutSec 2
        Write-Host "  MCP tools: $($mcp.mcpTools -join ', ')" -ForegroundColor Gray
    } catch { }
} else {
    Write-Host "  WARNING: AuraService may not be healthy yet" -ForegroundColor Yellow
}

# 4. Ensure global Copilot MCP config
Write-Host "`n[4/4] Configuring global Copilot MCP..." -ForegroundColor Yellow

$copilotDir = Join-Path $env:USERPROFILE ".copilot"
$mcpConfigPath = Join-Path $copilotDir "mcp-config.json"

if (-not (Test-Path $copilotDir)) {
    New-Item -ItemType Directory -Path $copilotDir -Force | Out-Null
}

$mcpConfig = @{
    mcpServers = @{
        aura = @{
            type = "http"
            url  = "http://localhost:5300/mcp"
        }
    }
}

if (Test-Path $mcpConfigPath) {
    # Merge with existing config
    try {
        $existing = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json -AsHashtable
        if (-not $existing.ContainsKey("mcpServers")) {
            $existing["mcpServers"] = @{}
        }
        $existing["mcpServers"]["aura"] = $mcpConfig.mcpServers.aura
        $json = $existing | ConvertTo-Json -Depth 5
    } catch {
        $json = $mcpConfig | ConvertTo-Json -Depth 5
    }
} else {
    $json = $mcpConfig | ConvertTo-Json -Depth 5
}

[System.IO.File]::WriteAllText($mcpConfigPath, ($json -replace "`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
Write-Host "  Global MCP config: $mcpConfigPath" -ForegroundColor Green

# Also ensure .vscode/mcp.json in the repo (for workspace-level)
$vscodeMcp = Join-Path $root ".vscode\mcp.json"
$vscodeConfig = @{
    servers = @{
        aura = @{
            type = "http"
            url  = "http://localhost:5300/mcp"
        }
    }
} | ConvertTo-Json -Depth 5

[System.IO.File]::WriteAllText($vscodeMcp, ($vscodeConfig -replace "`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host "`n=== Deploy Complete ===" -ForegroundColor Cyan
Write-Host "Aura MCP server available at http://localhost:5300/mcp" -ForegroundColor Green
Write-Host "All Copilot CLI sessions on this machine will discover Aura automatically." -ForegroundColor Green
