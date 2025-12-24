#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manually install the Aura VS Code extension
.DESCRIPTION
    Finds the VS Code installation and installs the bundled Aura extension.
    Use this if VS Code was not detected during Aura installation.
.EXAMPLE
    .\install-extension.ps1
#>

$ErrorActionPreference = "Stop"

# Find the VSIX file
$scriptDir = $PSScriptRoot
$extensionDir = Join-Path (Split-Path $scriptDir -Parent) "extension"
$vsixFiles = Get-ChildItem -Path $extensionDir -Filter "aura-*.vsix" -ErrorAction SilentlyContinue

if (-not $vsixFiles -or $vsixFiles.Count -eq 0) {
    Write-Host "ERROR: Extension not found in $extensionDir" -ForegroundColor Red
    Write-Host "Please reinstall Aura or download the extension from GitHub releases." -ForegroundColor Yellow
    exit 1
}

$vsix = $vsixFiles | Sort-Object Name -Descending | Select-Object -First 1
Write-Host "Found extension: $($vsix.Name)" -ForegroundColor Cyan

# Try to find VS Code
$codePaths = @(
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd",
    "C:\Program Files\Microsoft VS Code\bin\code.cmd",
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code Insiders\bin\code-insiders.cmd",
    "C:\Program Files\Microsoft VS Code Insiders\bin\code-insiders.cmd"
)

$code = $codePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $code) {
    # Try PATH as fallback
    $codeInPath = Get-Command code -ErrorAction SilentlyContinue
    if ($codeInPath) {
        $code = $codeInPath.Source
    }
}

if (-not $code) {
    Write-Host "`nVS Code not found!" -ForegroundColor Red
    Write-Host "`nPlease install VS Code from https://code.visualstudio.com/" -ForegroundColor Yellow
    Write-Host "Then run this script again, or install manually:" -ForegroundColor Yellow
    Write-Host "  code --install-extension `"$($vsix.FullName)`"" -ForegroundColor Cyan
    exit 1
}

Write-Host "`nInstalling Aura extension..." -ForegroundColor Green
Write-Host "Using: $code" -ForegroundColor Gray

& $code --install-extension $vsix.FullName --force

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Extension installed successfully!" -ForegroundColor Green
    Write-Host "  Restart VS Code to activate the Aura extension." -ForegroundColor Cyan
} else {
    Write-Host "`nInstallation may have failed. Try running manually:" -ForegroundColor Yellow
    Write-Host "  code --install-extension `"$($vsix.FullName)`"" -ForegroundColor Cyan
    exit 1
}
