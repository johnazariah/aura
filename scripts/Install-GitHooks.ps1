<#
.SYNOPSIS
    Installs Git hooks for the Aura repository.

.DESCRIPTION
    Copies pre-commit and other Git hooks from scripts/hooks to .git/hooks.
    
    The pre-commit hook performs these checks:
    - Secrets detection (API keys, passwords, connection strings)
    - Code formatting (dotnet format)
    - Feature file validation
    - Common mistake detection (Console.WriteLine, invalid JSON)

.PARAMETER Force
    Overwrite existing hooks without prompting.

.EXAMPLE
    .\scripts\Install-GitHooks.ps1

.EXAMPLE
    .\scripts\Install-GitHooks.ps1 -Force

.NOTES
    To bypass the hook temporarily: git commit --no-verify
    To enable build checking: $env:AURA_PRECOMMIT_BUILD = "1"
#>

param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$hooksSource = Join-Path $repoRoot 'scripts\hooks'
$hooksTarget = Join-Path $repoRoot '.git\hooks'

if (-not (Test-Path $hooksTarget)) {
    Write-Error "Git hooks directory not found. Are you in a Git repository?"
    exit 1
}

$hooks = Get-ChildItem -Path $hooksSource -File

foreach ($hook in $hooks) {
    $target = Join-Path $hooksTarget $hook.Name
    
    if ((Test-Path $target) -and -not $Force) {
        $existing = Get-Item $target
        if ($existing.Length -gt 0) {
            $response = Read-Host "Hook '$($hook.Name)' already exists. Overwrite? (y/N)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Host "⏭️  Skipped $($hook.Name)" -ForegroundColor Yellow
                continue
            }
        }
    }
    
    Copy-Item -Path $hook.FullName -Destination $target -Force
    Write-Host "✅ Installed $($hook.Name) hook" -ForegroundColor Green
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Git hooks installed successfully!" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pre-commit checks include:" -ForegroundColor White
Write-Host "  • Secrets detection (API keys, passwords)" -ForegroundColor Gray
Write-Host "  • Code formatting (dotnet format)" -ForegroundColor Gray
Write-Host "  • Feature file validation" -ForegroundColor Gray
Write-Host "  • Common mistake detection" -ForegroundColor Gray
Write-Host ""
Write-Host "Tips:" -ForegroundColor White
Write-Host "  • Bypass once: git commit --no-verify" -ForegroundColor Gray
Write-Host "  • Enable build: `$env:AURA_PRECOMMIT_BUILD = '1'" -ForegroundColor Gray
