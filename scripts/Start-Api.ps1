<#
.SYNOPSIS
    Start the Aura AppHost server for local development.

.DESCRIPTION
    Runs the Aura API on http://localhost:5300 in a new terminal window.
    The API runs in its own process so you can continue working in this terminal.

.EXAMPLE
    .\scripts\Start-Api.ps1
#>

[CmdletBinding()]
param(
    [switch]$InProcess  # Run in current process instead of new window
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$appHostProject = Join-Path $projectRoot "src/Aura.AppHost"

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
