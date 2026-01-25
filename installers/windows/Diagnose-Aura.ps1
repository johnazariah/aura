#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Diagnose Aura installation issues
.DESCRIPTION
    Checks all Aura components and reports their status
.EXAMPLE
    .\Diagnose-Aura.ps1
#>

$ErrorActionPreference = "Continue"

function Write-Status {
    param([string]$Name, [bool]$Ok, [string]$Details = "")
    if ($Ok) {
        Write-Host "  [OK] " -ForegroundColor Green -NoNewline
    } else {
        Write-Host "  [FAIL] " -ForegroundColor Red -NoNewline
    }
    Write-Host "$Name" -NoNewline
    if ($Details) {
        Write-Host " - $Details" -ForegroundColor Gray
    } else {
        Write-Host ""
    }
    return $Ok
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Aura Installation Diagnostics" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# ------------------------------------------
# 1. Installation Directory
# ------------------------------------------
Write-Host "1. Installation Files" -ForegroundColor Yellow

$installDir = "$env:ProgramFiles\Aura"
$allPassed = (Write-Status "Install directory exists" (Test-Path $installDir) $installDir) -and $allPassed
$allPassed = (Write-Status "Aura.Api.exe exists" (Test-Path "$installDir\api\Aura.Api.exe")) -and $allPassed
$allPassed = (Write-Status "Aura.Tray.exe exists" (Test-Path "$installDir\tray\Aura.Tray.exe")) -and $allPassed
$allPassed = (Write-Status "PostgreSQL binaries exist" (Test-Path "$installDir\pgsql\bin\postgres.exe")) -and $allPassed
$allPassed = (Write-Status "Agents directory exists" (Test-Path "$installDir\agents")) -and $allPassed

Write-Host ""

# ------------------------------------------
# 2. PostgreSQL Database
# ------------------------------------------
Write-Host "2. PostgreSQL Database" -ForegroundColor Yellow

$pgDataDir = "$env:ProgramData\Aura\data"
$allPassed = (Write-Status "Data directory exists" (Test-Path $pgDataDir) $pgDataDir) -and $allPassed

# Check AuraDB service
$auraDbService = Get-Service -Name "AuraDB" -ErrorAction SilentlyContinue
if ($auraDbService) {
    $allPassed = (Write-Status "AuraDB service exists" $true $auraDbService.Status) -and $allPassed
    $allPassed = (Write-Status "AuraDB service running" ($auraDbService.Status -eq "Running")) -and $allPassed
} else {
    $allPassed = (Write-Status "AuraDB service exists" $false "Not found") -and $allPassed
}

# Check port 5433
$port5433 = Get-NetTCPConnection -LocalPort 5433 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($port5433) {
    $process = Get-Process -Id $port5433.OwningProcess -ErrorAction SilentlyContinue
    $allPassed = (Write-Status "Port 5433 listening" $true "PID: $($port5433.OwningProcess) ($($process.ProcessName))") -and $allPassed
} else {
    $allPassed = (Write-Status "Port 5433 listening" $false "PostgreSQL may not be running") -and $allPassed
}

# Check database connection
if (Test-Path "$installDir\pgsql\bin\psql.exe") {
    $result = & "$installDir\pgsql\bin\psql.exe" -h localhost -p 5433 -U postgres -d auradb -c "SELECT 1" -t 2>&1
    $dbConnected = $LASTEXITCODE -eq 0
    $allPassed = (Write-Status "Can connect to auradb" $dbConnected) -and $allPassed
    
    if ($dbConnected) {
        $vectorCheck = & "$installDir\pgsql\bin\psql.exe" -h localhost -p 5433 -U postgres -d auradb -c "SELECT extname FROM pg_extension WHERE extname='vector'" -t 2>&1
        $hasVector = $vectorCheck -match "vector"
        $allPassed = (Write-Status "pgvector extension enabled" $hasVector) -and $allPassed
    }
}

Write-Host ""

# ------------------------------------------
# 3. Aura Service
# ------------------------------------------
Write-Host "3. Aura API Service" -ForegroundColor Yellow

$auraService = Get-Service -Name "AuraService" -ErrorAction SilentlyContinue
if ($auraService) {
    $allPassed = (Write-Status "AuraService exists" $true $auraService.Status) -and $allPassed
    $allPassed = (Write-Status "AuraService running" ($auraService.Status -eq "Running")) -and $allPassed
} else {
    Write-Status "AuraService exists" $false "Not installed (may be running manually)"
}

# Check port 5300
$port5300 = Get-NetTCPConnection -LocalPort 5300 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($port5300) {
    $process = Get-Process -Id $port5300.OwningProcess -ErrorAction SilentlyContinue
    $allPassed = (Write-Status "Port 5300 listening" $true "PID: $($port5300.OwningProcess) ($($process.ProcessName))") -and $allPassed
} else {
    $allPassed = (Write-Status "Port 5300 listening" $false "Aura API may not be running") -and $allPassed
}

# Check API health
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5300/health" -TimeoutSec 5 -ErrorAction Stop
    $allPassed = (Write-Status "API health check" $true) -and $allPassed
} catch {
    $allPassed = (Write-Status "API health check" $false $_.Exception.Message) -and $allPassed
}

Write-Host ""

# ------------------------------------------
# 4. Ollama
# ------------------------------------------
Write-Host "4. Ollama (LLM Provider)" -ForegroundColor Yellow

$ollamaPath = if (Test-Path "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe") {
    "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"
} elseif (Test-Path "C:\Program Files\Ollama\ollama.exe") {
    "C:\Program Files\Ollama\ollama.exe"
} else { $null }

$allPassed = (Write-Status "Ollama installed" ($null -ne $ollamaPath) $ollamaPath) -and $allPassed

# Check Ollama API
try {
    $ollamaHealth = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 5 -ErrorAction Stop
    $modelCount = $ollamaHealth.models.Count
    $allPassed = (Write-Status "Ollama API responding" $true "$modelCount models available") -and $allPassed
} catch {
    $allPassed = (Write-Status "Ollama API responding" $false "Not running or not accessible") -and $allPassed
}

Write-Host ""

# ------------------------------------------
# 5. VS Code Extension
# ------------------------------------------
Write-Host "5. VS Code Extension" -ForegroundColor Yellow

$vscodePath = if (Test-Path "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd") {
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd"
} elseif (Test-Path "C:\Program Files\Microsoft VS Code\bin\code.cmd") {
    "C:\Program Files\Microsoft VS Code\bin\code.cmd"
} else { $null }

if ($vscodePath) {
    Write-Status "VS Code installed" $true $vscodePath
    $extensions = & $vscodePath --list-extensions 2>$null
    $auraInstalled = $extensions -contains "aura.aura"
    $allPassed = (Write-Status "Aura extension installed" $auraInstalled) -and $allPassed
} else {
    Write-Status "VS Code installed" $false "VS Code not found"
}

Write-Host ""

# ------------------------------------------
# Summary
# ------------------------------------------
Write-Host "=====================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "  All checks passed!" -ForegroundColor Green
} else {
    Write-Host "  Some checks failed - see above" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting tips:" -ForegroundColor Yellow
    Write-Host "  1. Run this script as Administrator" -ForegroundColor Gray
    Write-Host "  2. Check Windows Event Viewer for errors" -ForegroundColor Gray
    Write-Host "  3. Try restarting services:" -ForegroundColor Gray
    Write-Host "     Restart-Service AuraDB" -ForegroundColor DarkGray
    Write-Host "     Restart-Service AuraService" -ForegroundColor DarkGray
    Write-Host "  4. Check install log: %TEMP%\Setup Log *.txt" -ForegroundColor Gray
}
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Keep window open if double-clicked
if ($Host.Name -eq "ConsoleHost") {
    Write-Host "Press any key to exit..." -ForegroundColor DarkGray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
