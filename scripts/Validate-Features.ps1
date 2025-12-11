<#
.SYNOPSIS
    Validates feature documentation conventions before commit.

.DESCRIPTION
    This script checks that:
    1. Files in features/completed/ have proper headers (Status, Completed date)
    2. The features/README.md index is consistent with completed features
    
    Run manually: .\scripts\Validate-Features.ps1
    Use as pre-commit hook: Copy to .git/hooks/pre-commit

.EXAMPLE
    .\scripts\Validate-Features.ps1
    
.EXAMPLE
    # Install as git hook
    .\scripts\Validate-Features.ps1 -Install
#>

param(
    [switch]$Install,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:hasErrors = $false

function Write-ValidationError {
    param([string]$Message)
    Write-Host "❌ ERROR: $Message" -ForegroundColor Red
    $script:hasErrors = $true
}

function Write-ValidationWarning {
    param([string]$Message)
    Write-Host "⚠️  WARNING: $Message" -ForegroundColor Yellow
}

function Write-ValidationSuccess {
    param([string]$Message)
    if ($Verbose) {
        Write-Host "✅ $Message" -ForegroundColor Green
    }
}

# Find repo root
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-ValidationError "Not in a git repository"
    exit 1
}

$featuresPath = Join-Path $repoRoot ".project/features"
$completedPath = Join-Path $featuresPath "completed"
$readmePath = Join-Path $featuresPath "README.md"

# Install mode - copy script as pre-commit hook
if ($Install) {
    $hookPath = Join-Path $repoRoot ".git/hooks/pre-commit"
    $scriptContent = @"
#!/bin/sh
# Feature documentation validation hook
powershell.exe -ExecutionPolicy Bypass -File "$repoRoot/scripts/Validate-Features.ps1"
"@
    Set-Content -Path $hookPath -Value $scriptContent -Encoding UTF8
    Write-Host "✅ Installed pre-commit hook at $hookPath" -ForegroundColor Green
    exit 0
}

Write-Host "🔍 Validating feature documentation..." -ForegroundColor Cyan

# Check 1: All completed features have required headers
Write-Host "`n📋 Checking completed feature headers..." -ForegroundColor Cyan

$completedFiles = Get-ChildItem -Path $completedPath -Filter "*.md" -ErrorAction SilentlyContinue

foreach ($file in $completedFiles) {
    $content = Get-Content $file.FullName -Raw
    $fileName = $file.Name
    
    # Check for Status header
    if ($content -notmatch '\*\*Status:\*\*.*✅.*Complete') {
        Write-ValidationError "$fileName missing '**Status:** ✅ Complete' header"
    } else {
        Write-ValidationSuccess "$fileName has Status header"
    }
    
    # Check for Completed date
    if ($content -notmatch '\*\*Completed:\*\*\s*\d{4}-\d{2}-\d{2}') {
        Write-ValidationError "$fileName missing '**Completed:** YYYY-MM-DD' header"
    } else {
        Write-ValidationSuccess "$fileName has Completed date"
    }
}

# Check 2: README index exists and has entries for all completed features
Write-Host "`n📋 Checking README index consistency..." -ForegroundColor Cyan

if (-not (Test-Path $readmePath)) {
    Write-ValidationError "features/README.md not found"
} else {
    $readmeContent = Get-Content $readmePath -Raw
    
    foreach ($file in $completedFiles) {
        $baseName = $file.BaseName
        $expectedLink = "completed/$($file.Name)"
        
        if ($readmeContent -notmatch [regex]::Escape($expectedLink)) {
            Write-ValidationError "README.md missing link to $expectedLink"
        } else {
            Write-ValidationSuccess "README.md has link to $expectedLink"
        }
    }
}

# Check 3: No orphaned links in README (links to files that don't exist)
Write-Host "`n📋 Checking for broken links..." -ForegroundColor Cyan

$linkPattern = '\[([^\]]+)\]\(completed/([^)]+\.md)\)'
$matches = [regex]::Matches($readmeContent, $linkPattern)

foreach ($match in $matches) {
    $linkedFile = $match.Groups[2].Value
    $fullPath = Join-Path $completedPath $linkedFile
    
    if (-not (Test-Path $fullPath)) {
        Write-ValidationError "README.md has broken link to completed/$linkedFile"
    } else {
        Write-ValidationSuccess "Link to completed/$linkedFile is valid"
    }
}

# Check 4: No numbered files in completed (convention violation)
Write-Host "`n📋 Checking naming conventions..." -ForegroundColor Cyan

foreach ($file in $completedFiles) {
    if ($file.Name -match '^\d+-') {
        Write-ValidationWarning "$($file.Name) uses numbered prefix (convention is kebab-case without numbers)"
    }
}

# Summary
Write-Host ""
if ($script:hasErrors) {
    Write-Host "❌ Validation FAILED - fix errors before committing" -ForegroundColor Red
    Write-Host ""
    Write-Host "To complete a feature properly, use the ceremony:" -ForegroundColor Yellow
    Write-Host "  1. Move file to features/completed/" -ForegroundColor Yellow
    Write-Host "  2. Add **Status:** ✅ Complete header" -ForegroundColor Yellow
    Write-Host "  3. Add **Completed:** YYYY-MM-DD header" -ForegroundColor Yellow
    Write-Host "  4. Update features/README.md index" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "See prompts/complete-feature.prompt for full ceremony." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "✅ All feature documentation conventions validated" -ForegroundColor Green
    exit 0
}
