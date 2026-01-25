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
    [string]$InstallPath,
    [switch]$SkipApi,
    [switch]$SkipExtension,
    [switch]$SkipAgents,
    [switch]$StopService = $true
)

$ErrorActionPreference = "Stop"

# Set platform-specific defaults
if (-not $InstallPath) {
    if ($IsMacOS) {
        $InstallPath = "/usr/local/share/aura"
    } elseif ($IsLinux) {
        $InstallPath = "/usr/local/share/aura"
    } else {
        $InstallPath = "C:\Program Files\Aura"
    }
}

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

# Platform-specific paths
if ($IsMacOS -or $IsLinux) {
    # macOS/Linux: source layout
    $apiPath = Join-Path $InstallPath "src/Aura.Api"
    $agentsPath = Join-Path $InstallPath "agents"
    $patternsPath = Join-Path $InstallPath "patterns"
    $promptsPath = Join-Path $InstallPath "prompts"
    $extensionPath = Join-Path $InstallPath "extension"
} else {
    # Windows: published layout
    $apiPath = Join-Path $InstallPath "api"
    $agentsPath = Join-Path $InstallPath "agents"
    $patternsPath = Join-Path $InstallPath "patterns"
    $promptsPath = Join-Path $InstallPath "prompts"
    $extensionPath = Join-Path $InstallPath "extension"
}

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
        
        if ($IsMacOS) {
            # macOS: Stop launchd service
            $plistPath = "$HOME/Library/LaunchAgents/com.aura.api.plist"
            if (Test-Path $plistPath) {
                Write-Step "Stopping Aura launchd service..."
                launchctl unload $plistPath 2>$null
                Start-Sleep -Seconds 2
            }
            
            # Also kill any stray dotnet processes running Aura.Api
            $auraProcs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
                try { $_.CommandLine -match "Aura.Api" } catch { $false }
            }
            if ($auraProcs) {
                Write-Step "Stopping Aura.Api processes..."
                $auraProcs | Stop-Process -Force
            }
        } else {
            # Windows: Stop Windows Service if exists
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
    }

    # =============================================================================
    # Ensure Service Account Exists (Windows only)
    # =============================================================================
    if (-not ($IsMacOS -or $IsLinux)) {
        # The Aura service runs as a dedicated AuraService user account for proper user context.
        # This provides a real user profile with standard tool caches for all languages.
        Write-Header "Checking Service Account"
        
        $serviceAccountScript = Join-Path $root "scripts\Create-ServiceAccount.ps1"
        if (Test-Path $serviceAccountScript) {
            $existingUser = Get-LocalUser -Name "AuraService" -ErrorAction SilentlyContinue
            $hasStoredPassword = (Test-Path "HKLM:\SOFTWARE\Aura") -and 
                (Get-ItemProperty -Path "HKLM:\SOFTWARE\Aura" -Name "ServiceAccountPassword" -ErrorAction SilentlyContinue)
            
            if (-not $existingUser) {
                Write-Step "Creating AuraService account..."
                & $serviceAccountScript
            } elseif (-not $hasStoredPassword) {
                Write-Step "AuraService account exists but password not stored - fixing..."
                & $serviceAccountScript
            } else {
                Write-Step "AuraService account exists"
            }
        } else {
            Write-Warning "Create-ServiceAccount.ps1 not found. Service may run as LocalSystem."
        }
    }

    # =============================================================================
    # Build API and Extension in PARALLEL for speed (Windows) or sequentially (macOS)
    # =============================================================================
    $apiJob = $null
    $extensionJob = $null
    $tempDir = Join-Path $root ".update-temp"
    if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Path $tempDir -Force | Out-Null }
    
    # Track if we built API directly (macOS) vs in background (Windows)
    $apiBuiltDirectly = $false
    
    if (-not $SkipApi) {
        if ($IsMacOS -or $IsLinux) {
            # macOS/Linux: build directly (simpler, avoids job issues)
            Write-Step "Building Aura.Api..."
            dotnet build src/Aura.Api/Aura.Api.csproj -c Release --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Api" }
            Write-Step "API build complete"
            $apiBuiltDirectly = $true
        } else {
            # Windows: background job for parallel builds
            Write-Step "Building Aura.Api (background)..."
            $apiLogFile = Join-Path $tempDir "api-build.log"
            $apiJob = Start-Job -ScriptBlock {
                param($root, $logFile)
                Set-Location $root
                $output = dotnet publish src/Aura.Api/Aura.Api.csproj `
                    -c Release `
                    -r win-x64 `
                    -p:PublishSelfContained=true `
                    -o ".update-temp/api" 2>&1
                $output | Out-File -FilePath $logFile -Encoding utf8
                if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Api. See $logFile" }
                return "API build complete"
            } -ArgumentList $root, $apiLogFile
        }
    } else {
        Write-Skip "API"
    }
    
    if (-not $SkipExtension) {
        Write-Step "Building VS Code Extension (background)..."
        $extLogFile = Join-Path $tempDir "extension-build.log"
        $extensionJob = Start-Job -ScriptBlock {
            param($root, $logFile)
            Set-Location (Join-Path $root "extension")
            
            # Build extension
            $output = @()
            $result = npm run package 2>&1
            $output += $result
            if ($LASTEXITCODE -ne 0) { 
                $output += npm ci 2>&1
                $output += npm run package 2>&1
            }
            
            # Package as VSIX
            $output += npx @vscode/vsce package --out "aura-dev.vsix" 2>&1
            $output | Out-File -FilePath $logFile -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw "Failed to package extension. See $logFile" }
            return "Extension build complete"
        } -ArgumentList $root, $extLogFile
    } else {
        Write-Skip "Extension"
    }

    # =============================================================================
    # Deploy Agents (while builds run)
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
    # Deploy Patterns (operational playbooks)
    # =============================================================================
    if (-not $SkipAgents) {
        Write-Header "Deploying Patterns"
        
        # Copy patterns directory
        if (Test-Path $patternsPath) {
            Remove-Item $patternsPath -Recurse -Force
        }
        Copy-Item -Path "patterns" -Destination $patternsPath -Recurse
        
        Write-Step "Patterns updated"
    } else {
        Write-Skip "Patterns"
    }

    # =============================================================================
    # Deploy Prompts (LLM prompt templates)
    # =============================================================================
    if (-not $SkipAgents) {
        Write-Header "Deploying Prompts"
        
        # Copy prompts directory
        if (Test-Path $promptsPath) {
            Remove-Item $promptsPath -Recurse -Force
        }
        Copy-Item -Path "prompts" -Destination $promptsPath -Recurse
        
        Write-Step "Prompts updated"
    } else {
        Write-Skip "Prompts"
    }

    # =============================================================================
    # Wait for builds with progress indicator
    # =============================================================================
    $spinner = @('⠋','⠙','⠹','⠸','⠼','⠴','⠦','⠧','⠇','⠏')
    $spinIdx = 0
    
    while (($apiJob -and $apiJob.State -eq "Running") -or ($extensionJob -and $extensionJob.State -eq "Running")) {
        $apiStatus = if ($apiJob) { if ($apiJob.State -eq "Running") { "building" } else { $apiJob.State } } else { "skipped" }
        $extStatus = if ($extensionJob) { if ($extensionJob.State -eq "Running") { "building" } else { $extensionJob.State } } else { "skipped" }
        Write-Host -NoNewline "`r$($spinner[$spinIdx]) API: $apiStatus | Extension: $extStatus    "
        $spinIdx = ($spinIdx + 1) % $spinner.Length
        Start-Sleep -Milliseconds 200
    }
    Write-Host "`r                                                        " # Clear spinner line
    
    # =============================================================================
    # Check API build result and deploy
    # =============================================================================
    if ($apiJob) {
        if ($apiJob.State -eq "Failed") {
            Write-Host "API build failed! Log:" -ForegroundColor Red
            Get-Content $apiLogFile -Tail 20
            throw "API build failed"
        }
        $apiResult = Receive-Job -Job $apiJob
        Write-Step $apiResult
        Remove-Job -Job $apiJob
        
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
    } elseif ($apiBuiltDirectly) {
        # macOS/Linux: rsync source files and rebuild in place
        Write-Step "Syncing source to $InstallPath..."
        
        # Use rsync for efficient sync (excludes bin/obj/.git)
        $rsyncExcludes = "--exclude=bin --exclude=obj --exclude=.git --exclude=node_modules --exclude=.update-temp"
        $rsyncCmd = "rsync -a $rsyncExcludes '$root/' '$InstallPath/'"
        bash -c $rsyncCmd
        
        Write-Step "Building in install location..."
        Push-Location $InstallPath
        try {
            dotnet build src/Aura.Api/Aura.Api.csproj -c Release --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Failed to build in install location" }
        } finally {
            Pop-Location
        }
        
        Write-Step "API updated"
    }

    # =============================================================================
    # Check Extension build result and install
    # =============================================================================
    if ($extensionJob) {
        if ($extensionJob.State -eq "Failed") {
            Write-Host "Extension build failed! Log:" -ForegroundColor Red
            Get-Content $extLogFile -Tail 20
            throw "Extension build failed"
        }
        $extResult = Receive-Job -Job $extensionJob
        Write-Step $extResult
        Remove-Job -Job $extensionJob
        
        Write-Step "Installing extension..."
        code --install-extension "extension/aura-dev.vsix" --force 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) { throw "Failed to install extension" }
        
        Write-Step "Extension updated - reload VS Code to activate"
    }

    # =============================================================================
    # Restart Services
    # =============================================================================
    if ($StopService -and -not $SkipApi) {
        Write-Header "Restarting Aura Services"
        
        if ($IsMacOS) {
            # macOS: Start launchd service
            $plistPath = "$HOME/Library/LaunchAgents/com.aura.api.plist"
            if (Test-Path $plistPath) {
                Write-Step "Starting Aura launchd service..."
                launchctl load $plistPath
            } else {
                Write-Step "Starting Aura.Api manually..."
                $env:ASPNETCORE_URLS = "http://localhost:5300"
                $env:ASPNETCORE_ENVIRONMENT = "Production"
                Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$InstallPath/src/Aura.Api", "--no-build" -WindowStyle Hidden
            }
        } else {
            # Windows
            $service = Get-Service -Name "AuraService" -ErrorAction SilentlyContinue
            if ($service) {
                # Check if service is using the correct account (migrate from LocalSystem if needed)
                $serviceWmi = Get-WmiObject Win32_Service -Filter "Name='AuraService'"
                if ($serviceWmi.StartName -eq "LocalSystem") {
                    Write-Step "Migrating service from LocalSystem to AuraService account..."
                    $credScript = Join-Path $root "scripts\Get-ServiceAccountCredential.ps1"
                    if (Test-Path $credScript) {
                        $cred = & $credScript
                        sc.exe config AuraService obj= $cred.FullUsername password= $cred.Password | Out-Null
                        Write-Step "Service account updated"
                    }
                }
                
                Write-Step "Starting AuraService..."
                Start-Service -Name "AuraService"
            } else {
                Write-Step "Starting Aura.Api manually..."
                Start-Process -FilePath (Join-Path $apiPath "Aura.Api.exe") -WindowStyle Hidden
            }
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
        
        if (-not $IsMacOS) {
            # Restart tray app if it was stopped (Windows only)
            $trayPath = Join-Path $InstallPath "tray\Aura.Tray.exe"
            if (Test-Path $trayPath) {
                $trayRunning = Get-Process -Name "Aura.Tray" -ErrorAction SilentlyContinue
                if (-not $trayRunning) {
                    Write-Step "Starting Aura.Tray..."
                    Start-Process -FilePath $trayPath -ArgumentList "--minimized" -WindowStyle Hidden
                }
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
