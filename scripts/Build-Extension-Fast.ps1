<#
.SYNOPSIS
    Fast build and install for development - skips production packaging.

.DESCRIPTION
    Compiles TypeScript once and installs directly without production minification.
    ~3x faster than full Build-Extension.ps1

.EXAMPLE
    .\scripts\Build-Extension-Fast.ps1
#>

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$extensionDir = Join-Path $projectRoot "extension"

Write-Host "Fast building Aura extension..." -ForegroundColor Cyan

Push-Location $extensionDir
try {
    # Quick compile only (no production build)
    Write-Host "Compiling..." -ForegroundColor Yellow
    npm run compile 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        npm run compile  # Show errors if failed
        throw "Compilation failed"
    }

    # Package without prepublish (uses existing compiled output)
    Write-Host "Packaging..." -ForegroundColor Yellow
    vsce package --no-dependencies --allow-missing-repository --skip-license 2>&1 | Select-String -Pattern "DONE|Packaged"

    # Install
    $vsix = Get-ChildItem -Filter "*.vsix" | Select-Object -First 1
    if ($vsix) {
        code --install-extension $vsix.FullName --force 2>&1 | Out-Null
        Write-Host "✅ Installed! Reload VS Code to activate." -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
