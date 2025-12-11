<#
.SYNOPSIS
    Start the Aura AppHost server for local development.

.DESCRIPTION
    Runs the Aura API on http://localhost:5300 in a new terminal window.
    The API runs in its own process so you can continue working in this terminal.
    
    Prerequisites:
    - Podman (Windows) or Docker must be running for PostgreSQL
    - Ollama should be running for LLM inference (optional if using Azure OpenAI)

.EXAMPLE
    .\scripts\Start-Api.ps1

.EXAMPLE
    .\scripts\Start-Api.ps1 -InProcess  # Run in current terminal
#>

[CmdletBinding()]
param(
    [switch]$InProcess,  # Run in current process instead of new window
    [switch]$SkipChecks  # Skip prerequisite checks
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$appHostProject = Join-Path $projectRoot "src/Aura.AppHost"

function Test-ContainerRuntime {
    # Check if Docker-compatible named pipe exists (Podman or Docker)
    # This is what Aspire actually uses, so it's the most reliable check
    $dockerPipe = Get-ChildItem "\\.\pipe\" -ErrorAction SilentlyContinue | Where-Object Name -eq "docker_engine"
    if ($dockerPipe) {
        # Determine which runtime provides the pipe
        $podmanMachine = wsl -l -q 2>$null | Where-Object { $_ -match "podman" }
        if ($podmanMachine) {
            return @{ Available = $true; Runtime = "Podman" }
        }
        return @{ Available = $true; Runtime = "Docker" }
    }
    
    # Fallback: try CLI commands
    try {
        $null = podman info 2>&1
        if ($LASTEXITCODE -eq 0) {
            return @{ Available = $true; Runtime = "Podman" }
        }
    }
    catch { }
    
    try {
        $null = docker info 2>&1
        if ($LASTEXITCODE -eq 0) {
            return @{ Available = $true; Runtime = "Docker" }
        }
    }
    catch { }
    
    return @{ Available = $false; Runtime = $null }
}

# Check prerequisites unless skipped
if (-not $SkipChecks) {
    Write-Host "Checking prerequisites..." -ForegroundColor Gray
    
    $containerCheck = Test-ContainerRuntime
    if (-not $containerCheck.Available) {
        Write-Host ""
        Write-Host "ERROR: No container runtime available!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Aspire needs Podman or Docker to run PostgreSQL." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "For Podman (recommended on Windows):" -ForegroundColor White
        Write-Host "  podman machine start" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "For Docker:" -ForegroundColor White
        Write-Host "  Start Docker Desktop" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Then run this script again, or use -SkipChecks to bypass." -ForegroundColor Gray
        exit 1
    }
    
    Write-Host "  Container runtime: $($containerCheck.Runtime)" -ForegroundColor Green
}

if ($InProcess) {
    # Run in current terminal (blocking)
    Write-Host "Starting Aura API (in-process)..." -ForegroundColor Cyan
    Write-Host "URL: http://localhost:5300" -ForegroundColor Gray
    Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
    Write-Host ""
    
    Push-Location $projectRoot
    try {
        dotnet run --project src/Aura.AppHost
    }
    finally {
        Pop-Location
    }
}
else {
    # Launch in a new terminal window
    Write-Host "Starting Aura API in new window..." -ForegroundColor Cyan
    
    $command = "Set-Location '$projectRoot'; Write-Host 'Aura API - http://localhost:5300' -ForegroundColor Cyan; Write-Host 'Press Ctrl+C to stop' -ForegroundColor Gray; Write-Host ''; dotnet run --project src/Aura.AppHost"
    
    if ($IsWindows -or $env:OS -match 'Windows') {
        # Windows: use Windows Terminal if available, fall back to conhost
        $wtPath = Get-Command wt.exe -ErrorAction SilentlyContinue
        if ($wtPath) {
            Start-Process wt.exe -ArgumentList "pwsh", "-NoExit", "-Command", $command
        }
        else {
            Start-Process pwsh -ArgumentList "-NoExit", "-Command", $command
        }
    }
    elseif ($IsMacOS) {
        # macOS: use Terminal.app
        $escapedCommand = $command -replace "'", "\\'"
        Start-Process osascript -ArgumentList "-e", "tell app `"Terminal`" to do script `"pwsh -Command '$escapedCommand'`""
    }
    else {
        # Linux: try common terminal emulators
        $terminals = @('gnome-terminal', 'konsole', 'xterm')
        $found = $false
        foreach ($term in $terminals) {
            if (Get-Command $term -ErrorAction SilentlyContinue) {
                if ($term -eq 'gnome-terminal') {
                    Start-Process $term -ArgumentList "--", "pwsh", "-NoExit", "-Command", $command
                }
                else {
                    Start-Process $term -ArgumentList "-e", "pwsh -NoExit -Command `"$command`""
                }
                $found = $true
                break
            }
        }
        if (-not $found) {
            Write-Warning "No terminal emulator found. Running in-process..."
            & $PSCommandPath -InProcess
            return
        }
    }
    
    Write-Host ""
    Write-Host "API launching in new window." -ForegroundColor Green
    Write-Host "  URL: http://localhost:5300" -ForegroundColor Gray
    Write-Host "  Dashboard: https://localhost:17071" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Use -InProcess to run in this terminal instead." -ForegroundColor DarkGray
}
