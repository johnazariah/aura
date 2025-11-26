<#
.SYNOPSIS
    Start the Aura AppHost server for local development.

.DESCRIPTION
    Runs the Aura API on http://localhost:5300 with hot-reload enabled.
    Press Ctrl+C to stop the server.

.EXAMPLE
    .\scripts\Start-Api.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Starting Aura API..." -ForegroundColor Cyan
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
