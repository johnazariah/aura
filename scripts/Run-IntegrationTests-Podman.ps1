#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs Aura integration tests with Podman as the container runtime.

.DESCRIPTION
    This script ensures Podman is running, starts the API proxy if needed,
    and runs the integration tests with the correct environment variables.

.NOTES
    Requires Podman to be installed. On Windows, this uses Podman Machine.
#>

param(
    [switch]$NoBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "=== Aura Integration Tests (Podman) ===" -ForegroundColor Cyan

# Step 1: Verify Podman is available
Write-Host "`n[1/5] Checking Podman installation..." -ForegroundColor Yellow
$podman = Get-Command podman -ErrorAction SilentlyContinue
if (-not $podman) {
    Write-Error "Podman is not installed. Please install Podman from https://podman.io"
    exit 1
}
Write-Host "  Podman found at: $($podman.Source)" -ForegroundColor Green

# Step 2: Ensure Podman machine is running
Write-Host "`n[2/5] Ensuring Podman machine is running..." -ForegroundColor Yellow
$machineStatus = podman machine inspect podman-machine-default 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  No Podman machine found. Creating one..." -ForegroundColor Yellow
    podman machine init
    podman machine start
} else {
    $isRunning = ($machineStatus | ConvertFrom-Json -AsHashtable).State -eq "running"
    if (-not $isRunning) {
        Write-Host "  Starting Podman machine..." -ForegroundColor Yellow
        podman machine start
    } else {
        Write-Host "  Podman machine is running" -ForegroundColor Green
    }
}

# Step 3: Get the Podman machine IP address
Write-Host "`n[3/5] Getting Podman machine IP address..." -ForegroundColor Yellow
$ipOutput = podman machine ssh "ip addr show eth0" 2>&1
$ipMatch = [regex]::Match($ipOutput, 'inet (\d+\.\d+\.\d+\.\d+)')
if (-not $ipMatch.Success) {
    Write-Error "Could not determine Podman machine IP address"
    exit 1
}
$podmanIP = $ipMatch.Groups[1].Value
Write-Host "  Podman machine IP: $podmanIP" -ForegroundColor Green

# Step 4: Start Podman API service with TCP listener
Write-Host "`n[4/5] Starting Podman API service on TCP..." -ForegroundColor Yellow

# Check if API is already listening
$apiCheck = curl -s "http://${podmanIP}:2375/version" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Podman API already listening on tcp://${podmanIP}:2375" -ForegroundColor Green
} else {
    Write-Host "  Starting Podman system service in background..." -ForegroundColor Yellow
    # Start the service in background (it will run until machine stops)
    $job = Start-Job -ScriptBlock {
        param($podmanIP)
        podman machine ssh "podman system service --time=0 tcp:0.0.0.0:2375" 2>&1
    } -ArgumentList $podmanIP

    # Wait a moment for it to start
    Start-Sleep -Seconds 2

    # Verify it's running
    $apiCheck = curl -s "http://${podmanIP}:2375/version" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Podman API service. Check if port 2375 is blocked."
        exit 1
    }
    Write-Host "  Podman API service started" -ForegroundColor Green
}

# Step 5: Run integration tests
Write-Host "`n[5/5] Running integration tests..." -ForegroundColor Yellow
Write-Host "  DOCKER_HOST=tcp://${podmanIP}:2375" -ForegroundColor Gray
Write-Host "  TESTCONTAINERS_RYUK_DISABLED=true" -ForegroundColor Gray

$env:DOCKER_HOST = "tcp://${podmanIP}:2375"
$env:TESTCONTAINERS_RYUK_DISABLED = "true"

$buildArg = if ($NoBuild) { "--no-build" } else { "" }
$verbosityArg = if ($Verbose) { "--verbosity detailed" } else { "--verbosity minimal" }

$projectPath = Join-Path $PSScriptRoot "..\tests\Aura.Api.IntegrationTests"
$cmd = "dotnet test `"$projectPath`" $buildArg $verbosityArg"

Write-Host "`n  Running: $cmd`n" -ForegroundColor Gray
Invoke-Expression $cmd

$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    Write-Host "`n=== All integration tests passed! ===" -ForegroundColor Green
} else {
    Write-Host "`n=== Some tests failed (exit code: $exitCode) ===" -ForegroundColor Red
}

exit $exitCode
