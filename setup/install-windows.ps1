#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Install Aura and all prerequisites on Windows

.DESCRIPTION
    Installs: winget, Podman, Ollama, PostgreSQL (in container), pgvector,
    and optionally the Aura service.

.EXAMPLE
    .\setup\install-windows.ps1
#>

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
    Write-Host "-- $message (already installed)" -ForegroundColor DarkGray
}

# =============================================================================
# Check Windows
# =============================================================================
Write-Header "Checking System Requirements"

if ([Environment]::OSVersion.Platform -ne "Win32NT") {
    throw "This script is for Windows only"
}
Write-Step "Windows detected: $([Environment]::OSVersion.VersionString)"

# Check winget
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw "winget not found. Please install App Installer from Microsoft Store"
}
Write-Step "winget available"

# =============================================================================
# Install Podman
# =============================================================================
Write-Header "Installing Podman"

if (Get-Command podman -ErrorAction SilentlyContinue) {
    Write-Skip "Podman"
} else {
    Write-Step "Installing Podman..."
    winget install -e --id RedHat.Podman --accept-source-agreements --accept-package-agreements
    
    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    
    Write-Step "Initializing Podman machine..."
    podman machine init
    podman machine start
}

# =============================================================================
# Install Ollama (native for GPU access)
# =============================================================================
Write-Header "Installing Ollama"

if (Get-Command ollama -ErrorAction SilentlyContinue) {
    Write-Skip "Ollama"
} else {
    Write-Step "Installing Ollama..."
    winget install -e --id Ollama.Ollama --accept-source-agreements --accept-package-agreements
    
    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    
    Write-Step "Starting Ollama..."
    Start-Process ollama -ArgumentList "serve" -WindowStyle Hidden
    Start-Sleep -Seconds 5
}

# =============================================================================
# Pull Ollama Models
# =============================================================================
Write-Header "Pulling Ollama Models"

# Wait for Ollama to be ready
Write-Step "Waiting for Ollama..."
$maxAttempts = 30
for ($i = 0; $i -lt $maxAttempts; $i++) {
    try {
        Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -ErrorAction Stop | Out-Null
        break
    } catch {
        Start-Sleep -Seconds 1
    }
}

Write-Step "Pulling nomic-embed-text (embeddings)..."
ollama pull nomic-embed-text

Write-Step "Pulling qwen2.5-coder:7b (code generation)..."
ollama pull qwen2.5-coder:7b

# =============================================================================
# Start PostgreSQL Container with pgvector
# =============================================================================
Write-Header "Setting Up PostgreSQL"

$containerExists = podman ps -a --filter "name=aura-postgres" --format "{{.Names}}" 2>$null
if ($containerExists -eq "aura-postgres") {
    Write-Skip "PostgreSQL container"
    
    # Ensure running
    $running = podman ps --filter "name=aura-postgres" --format "{{.Names}}" 2>$null
    if ($running -ne "aura-postgres") {
        Write-Step "Starting PostgreSQL container..."
        podman start aura-postgres
    }
} else {
    Write-Step "Creating PostgreSQL container with pgvector..."
    podman run -d `
        --name aura-postgres `
        -e POSTGRES_USER=aura `
        -e POSTGRES_PASSWORD=aura `
        -e POSTGRES_DB=aura `
        -p 5432:5432 `
        -v aura-pgdata:/var/lib/postgresql/data `
        pgvector/pgvector:pg16
    
    Write-Step "Waiting for PostgreSQL to be ready..."
    Start-Sleep -Seconds 10
}

# Enable pgvector extension
Write-Step "Enabling pgvector extension..."
$env:PGPASSWORD = "aura"
podman exec aura-postgres psql -U aura -d aura -c "CREATE EXTENSION IF NOT EXISTS vector;" 2>$null

# =============================================================================
# Check .NET SDK (for development)
# =============================================================================
Write-Header "Checking .NET SDK"

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $version = dotnet --version
    Write-Skip ".NET SDK: $version"
} else {
    Write-Step "Installing .NET SDK..."
    winget install -e --id Microsoft.DotNet.SDK.9 --accept-source-agreements --accept-package-agreements
    
    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
}

# =============================================================================
# Build Aura
# =============================================================================
Write-Header "Building Aura"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Split-Path -Parent $scriptDir

if (Test-Path "$repoDir\src\Aura.Api") {
    Write-Step "Building Aura from source..."
    Push-Location $repoDir
    dotnet build
    Pop-Location
    Write-Step "Build complete"
} else {
    Write-Host "  Clone the Aura repository to build from source" -ForegroundColor DarkGray
}

# =============================================================================
# Summary
# =============================================================================
Write-Header "Installation Complete!"

Write-Host ""
Write-Host "Installed components:" -ForegroundColor White
Write-Host "  - Podman (container runtime)" -ForegroundColor DarkGray
Write-Host "  - PostgreSQL 16 with pgvector (in container)" -ForegroundColor DarkGray
Write-Host "  - Ollama (local LLM, native)" -ForegroundColor DarkGray
Write-Host "  - nomic-embed-text model" -ForegroundColor DarkGray
Write-Host "  - qwen2.5-coder:7b model" -ForegroundColor DarkGray
Write-Host "  - .NET SDK" -ForegroundColor DarkGray

Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. cd $repoDir" -ForegroundColor DarkGray
Write-Host "  2. dotnet run --project src/Aura.AppHost" -ForegroundColor DarkGray
Write-Host "  3. API available at: http://localhost:5280" -ForegroundColor DarkGray
Write-Host ""
