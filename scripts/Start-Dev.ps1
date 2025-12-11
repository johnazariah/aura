<#
.SYNOPSIS
    Start the complete Aura development environment.

.DESCRIPTION
    This script sets up the full development environment:
    1. Checks that Ollama is running (required for LLM)
    2. Checks container runtime (Podman/Docker for PostgreSQL)
    3. Builds the .NET solution
    4. Starts the Aspire AppHost (PostgreSQL + API) in background
    5. Builds and installs the VS Code extension
    6. Opens a new VS Code window for testing

    Press Ctrl+C to stop all services.

.PARAMETER SkipOllamaCheck
    Skip the Ollama availability check (use if using Azure OpenAI)

.PARAMETER SkipBuild
    Skip the .NET build step (use if already built)

.PARAMETER TestWorkspace
    Path to open in the test VS Code window (default: current project)

.EXAMPLE
    .\scripts\Start-Dev.ps1

.EXAMPLE
    .\scripts\Start-Dev.ps1 -SkipBuild

.EXAMPLE
    .\scripts\Start-Dev.ps1 -TestWorkspace "C:\my-project"
#>

[CmdletBinding()]
param(
    [switch]$SkipOllamaCheck,
    [switch]$SkipBuild,
    [string]$TestWorkspace
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$extensionDir = Join-Path $projectRoot "extension"

# Track background jobs for cleanup
$script:appHostJob = $null

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor Gray
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

function Test-OllamaRunning {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 2 -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-ContainerRuntime {
    # Check Podman first (preferred on Windows per project guidelines)
    try {
        $podmanResult = podman info 2>&1
        if ($LASTEXITCODE -eq 0) {
            return @{ Available = $true; Runtime = "podman" }
        }
    }
    catch { }
    
    # Fall back to Docker
    try {
        $dockerResult = docker info 2>&1
        if ($LASTEXITCODE -eq 0) {
            return @{ Available = $true; Runtime = "docker" }
        }
    }
    catch { }
    
    return @{ Available = $false; Runtime = $null }
}

function Test-RequiredModels {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
        $models = $response.models | ForEach-Object { $_.name }
        
        $hasLlm = $models | Where-Object { $_ -match "llama|qwen|mistral" }
        $hasEmbed = $models | Where-Object { $_ -match "nomic-embed" }
        
        return @{
            HasLlm = $null -ne $hasLlm
            HasEmbed = $null -ne $hasEmbed
            Models = $models
        }
    }
    catch {
        return @{ HasLlm = $false; HasEmbed = $false; Models = @() }
    }
}

function Stop-Demo {
    Write-Host ""
    Write-Host "Stopping demo environment..." -ForegroundColor Yellow
    
    if ($script:appHostJob) {
        Write-Info "Stopping AppHost..."
        Stop-Job -Job $script:appHostJob -ErrorAction SilentlyContinue
        Remove-Job -Job $script:appHostJob -Force -ErrorAction SilentlyContinue
    }
    
    # Also try to stop any orphaned dotnet processes running AppHost
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
        Where-Object { $_.CommandLine -match "Aura.AppHost" } | 
        Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "Demo stopped." -ForegroundColor Green
}

# Register cleanup on script exit
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Stop-Demo }
trap { Stop-Demo; break }

try {
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "  ║                                                           ║" -ForegroundColor Magenta
    Write-Host "  ║            🌟  AURA DEV ENVIRONMENT  🌟                   ║" -ForegroundColor Magenta
    Write-Host "  ║                                                           ║" -ForegroundColor Magenta
    Write-Host "  ║   Local-first AI for knowledge work                       ║" -ForegroundColor Magenta
    Write-Host "  ║                                                           ║" -ForegroundColor Magenta
    Write-Host "  ╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Magenta

    # Step 1: Check Ollama
    if (-not $SkipOllamaCheck) {
        Write-Step "Checking Ollama"
        
        if (-not (Test-OllamaRunning)) {
            Write-Warning "Ollama is not running!"
            Write-Host ""
            Write-Host "  Please start Ollama first:" -ForegroundColor White
            Write-Host "    > ollama serve" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  Then pull required models:" -ForegroundColor White
            Write-Host "    > ollama pull llama3.2:3b" -ForegroundColor Yellow
            Write-Host "    > ollama pull nomic-embed-text" -ForegroundColor Yellow
            Write-Host ""
            throw "Ollama not running. Start it and try again."
        }
        
        Write-Success "Ollama is running"
        
        $modelCheck = Test-RequiredModels
        if ($modelCheck.HasLlm) {
            Write-Success "LLM model available"
        }
        else {
            Write-Warning "No LLM model found. Run: ollama pull llama3.2:3b"
        }
        
        if ($modelCheck.HasEmbed) {
            Write-Success "Embedding model available"
        }
        else {
            Write-Warning "No embedding model found. Run: ollama pull nomic-embed-text"
        }
    }

    # Step 1b: Check container runtime (needed for PostgreSQL)
    Write-Step "Checking Container Runtime"
    
    $containerCheck = Test-ContainerRuntime
    if (-not $containerCheck.Available) {
        Write-Warning "No container runtime available!"
        Write-Host ""
        Write-Host "  Aspire needs Podman or Docker to run PostgreSQL." -ForegroundColor White
        Write-Host ""
        Write-Host "  For Podman (recommended):" -ForegroundColor White
        Write-Host "    > podman machine start" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  For Docker:" -ForegroundColor White
        Write-Host "    > Start Docker Desktop" -ForegroundColor Yellow
        Write-Host ""
        throw "Container runtime not available. Start Podman or Docker and try again."
    }
    
    Write-Success "$($containerCheck.Runtime) is running"

    # Step 2: Build .NET solution
    if (-not $SkipBuild) {
        Write-Step "Building .NET Solution"
        
        Push-Location $projectRoot
        try {
            dotnet build --verbosity quiet
            if ($LASTEXITCODE -ne 0) {
                throw ".NET build failed"
            }
            Write-Success "Solution built successfully"
        }
        finally {
            Pop-Location
        }
    }

    # Step 3: Start AppHost in background
    Write-Step "Starting Aspire AppHost"
    
    Write-Info "Starting PostgreSQL and API..."
    
    $script:appHostJob = Start-Job -ScriptBlock {
        param($root)
        Set-Location $root
        dotnet run --project src/Aura.AppHost --no-build 2>&1
    } -ArgumentList $projectRoot
    
    # Wait for API to be ready
    Write-Info "Waiting for API to be ready (this may take 1-2 minutes on first run)..."
    $maxAttempts = 60  # 2 minutes total
    $attempt = 0
    $apiReady = $false
    
    while ($attempt -lt $maxAttempts -and -not $apiReady) {
        Start-Sleep -Seconds 2
        $attempt++
        
        # Show progress every 10 seconds
        if ($attempt % 5 -eq 0) {
            Write-Host "  ... $($attempt * 2)s" -ForegroundColor DarkGray
        }
        
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:5300/health" -Method Get -TimeoutSec 2 -ErrorAction Stop
            if ($response.healthy) {
                $apiReady = $true
            }
        }
        catch {
            # Still starting up
        }
    }
    
    if (-not $apiReady) {
        # Check if job failed
        if ($script:appHostJob.State -eq 'Failed') {
            Write-Host ""
            Write-Host "  AppHost job failed:" -ForegroundColor Red
            $jobOutput = Receive-Job -Job $script:appHostJob
            Write-Host $jobOutput -ForegroundColor Red
        }
        throw "API failed to start within 2 minutes. Try running 'dotnet run --project src/Aura.AppHost' directly to see errors."
    }
    
    Write-Success "API is running at http://localhost:5300"
    Write-Info "Aspire Dashboard: https://localhost:17071"
    
    # Check RAG health
    Write-Info "Checking RAG service..."
    try {
        $ragHealth = Invoke-RestMethod -Uri "http://localhost:5300/health/rag" -Method Get -TimeoutSec 5 -ErrorAction Stop
        if ($ragHealth.healthy) {
            Write-Success "RAG service is healthy ($($ragHealth.totalChunks) chunks indexed)"
        }
        else {
            Write-Warning "RAG service not ready: $($ragHealth.details)"
            Write-Info "This may be due to pgvector extension not being installed in PostgreSQL"
            Write-Info "You can still use the chat - RAG will work once the database is set up"
        }
    }
    catch {
        Write-Warning "Could not check RAG health"
    }

    # Step 4: Build and install extension
    Write-Step "Building VS Code Extension"
    
    Push-Location $extensionDir
    try {
        # Check for node_modules
        if (-not (Test-Path "node_modules")) {
            Write-Info "Installing npm dependencies..."
            npm install --silent 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "npm install failed"
            }
        }
        
        # Compile TypeScript
        Write-Info "Compiling TypeScript..."
        npm run compile 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "TypeScript compilation failed"
        }
        Write-Success "Extension compiled"
        
        # Package as VSIX
        Write-Info "Packaging VSIX..."
        
        # Check for vsce
        $vsceCmd = Get-Command vsce -ErrorAction SilentlyContinue
        if (-not $vsceCmd) {
            npm install -g @vscode/vsce 2>&1 | Out-Null
        }
        
        # Remove old VSIX files
        Get-ChildItem -Path . -Filter "*.vsix" | Remove-Item -Force
        
        vsce package --no-dependencies --allow-missing-repository 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "VSIX packaging failed"
        }
        
        $vsixFile = Get-ChildItem -Path . -Filter "*.vsix" | Select-Object -First 1
        Write-Success "Created $($vsixFile.Name)"
        
        # Install extension
        Write-Info "Installing extension..."
        code --install-extension $vsixFile.FullName --force 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Extension installation failed"
        }
        Write-Success "Extension installed"
    }
    finally {
        Pop-Location
    }

    # Step 5: Open VS Code for testing
    Write-Step "Launching Test Environment"
    
    $testPath = if ($TestWorkspace) { $TestWorkspace } else { $projectRoot }
    
    Write-Info "Opening VS Code at: $testPath"
    code --new-window $testPath
    
    Write-Success "VS Code launched"

    # Done!
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "  ║                                                           ║" -ForegroundColor Green
    Write-Host "  ║              ✓  DEMO READY!                               ║" -ForegroundColor Green
    Write-Host "  ║                                                           ║" -ForegroundColor Green
    Write-Host "  ╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  In the new VS Code window:" -ForegroundColor White
    Write-Host "    1. Click the Aura icon in the Activity Bar" -ForegroundColor Gray
    Write-Host "    2. Use the Chat panel to talk to agents" -ForegroundColor Gray
    Write-Host "    3. Run 'Aura: Index Workspace' to enable RAG" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Services:" -ForegroundColor White
    Write-Host "    • API:              http://localhost:5300" -ForegroundColor Gray
    Write-Host "    • Health:           http://localhost:5300/health" -ForegroundColor Gray
    Write-Host "    • Aspire Dashboard: http://localhost:15888" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Press Ctrl+C to stop all services" -ForegroundColor Yellow
    Write-Host ""

    # Keep script running to maintain the background job
    Write-Host "AppHost output:" -ForegroundColor DarkGray
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    
    while ($true) {
        # Stream AppHost output
        $output = Receive-Job -Job $script:appHostJob -ErrorAction SilentlyContinue
        if ($output) {
            $output | ForEach-Object { Write-Host $_ -ForegroundColor DarkGray }
        }
        
        # Check if job died
        if ($script:appHostJob.State -eq 'Failed' -or $script:appHostJob.State -eq 'Completed') {
            Write-Warning "AppHost stopped unexpectedly"
            $finalOutput = Receive-Job -Job $script:appHostJob
            if ($finalOutput) {
                Write-Host $finalOutput -ForegroundColor Red
            }
            break
        }
        
        Start-Sleep -Seconds 1
    }
}
catch {
    Write-Host ""
    Write-Host "  ✗ Error: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
finally {
    Stop-Demo
}
