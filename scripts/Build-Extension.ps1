<#
.SYNOPSIS
    Build and install the Aura VS Code extension as a VSIX.

.DESCRIPTION
    Compiles the TypeScript extension, packages it as a VSIX, and optionally installs it.

.PARAMETER SkipInstall
    Build the VSIX but don't install it

.PARAMETER NoOpen
    Don't open VS Code after installation

.EXAMPLE
    .\scripts\Build-Extension.ps1

.EXAMPLE
    .\scripts\Build-Extension.ps1 -SkipInstall

.EXAMPLE
    .\scripts\Build-Extension.ps1 -NoOpen
#>

[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$extensionDir = Join-Path $projectRoot "extension"

Write-Host "Building Aura VS Code Extension..." -ForegroundColor Cyan
Write-Host ""

Push-Location $extensionDir
try {
    # Check for node_modules
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed"
        }
    }

    # Check for vsce
    $vsceInstalled = $null -ne (Get-Command vsce -ErrorAction SilentlyContinue)
    if (-not $vsceInstalled) {
        Write-Host "Installing vsce globally..." -ForegroundColor Yellow
        npm install -g @vscode/vsce
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install vsce"
        }
    }

    # Compile TypeScript
    Write-Host "Compiling TypeScript..." -ForegroundColor Yellow
    npm run compile
    if ($LASTEXITCODE -ne 0) {
        throw "TypeScript compilation failed"
    }

    # Package as VSIX
    Write-Host ""
    Write-Host "Packaging VSIX..." -ForegroundColor Yellow
    
    # Remove old VSIX files
    Get-ChildItem -Path . -Filter "*.vsix" | Remove-Item -Force

    vsce package --no-dependencies
    if ($LASTEXITCODE -ne 0) {
        throw "VSIX packaging failed"
    }

    # Find the generated VSIX
    $vsixFile = Get-ChildItem -Path . -Filter "*.vsix" | Select-Object -First 1
    if (-not $vsixFile) {
        throw "VSIX file not found after packaging"
    }

    Write-Host ""
    Write-Host "Created: $($vsixFile.Name)" -ForegroundColor Green

    if (-not $SkipInstall) {
        Write-Host ""
        Write-Host "Installing extension..." -ForegroundColor Yellow
        
        code --install-extension $vsixFile.FullName --force
        if ($LASTEXITCODE -ne 0) {
            throw "Extension installation failed"
        }

        Write-Host ""
        Write-Host "Extension installed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "To activate:" -ForegroundColor Cyan
        Write-Host "  1. Reload VS Code (Ctrl+Shift+P -> 'Developer: Reload Window')" -ForegroundColor White
        Write-Host "  2. Look for the Aura icon in the Activity Bar" -ForegroundColor White
        Write-Host "  3. Start the API: .\scripts\Start-Api.ps1" -ForegroundColor White

        if (-not $NoOpen) {
            Write-Host ""
            Write-Host "Opening VS Code..." -ForegroundColor Yellow
            code $projectRoot
        }
    }
    else {
        Write-Host ""
        Write-Host "VSIX ready at: $($vsixFile.FullName)" -ForegroundColor Cyan
        Write-Host "To install manually: code --install-extension $($vsixFile.Name)" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}
