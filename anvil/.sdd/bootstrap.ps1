#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Bootstrap a new project with the SDD framework.

.DESCRIPTION
    Creates the .project/ directory structure and copies starter templates.
    Run this script from your project root after copying .sdd/ into it.

.PARAMETER ProjectName
    Name of your project (used in VISION.md and AGENTS.md).

.PARAMETER SkipGitConfig
    Skip setting up the git commit template.

.PARAMETER Force
    Overwrite existing files if they exist.

.EXAMPLE
    .sdd/bootstrap.ps1 -ProjectName "MyApp"

.EXAMPLE
    .sdd/bootstrap.ps1 -ProjectName "MyApp" -SkipGitConfig
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectName,

    [switch]$SkipGitConfig,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Step { param($msg) Write-Host "→ $msg" -ForegroundColor Cyan }
function Write-Done { param($msg) Write-Host "✓ $msg" -ForegroundColor Green }
function Write-Skip { param($msg) Write-Host "○ $msg (already exists)" -ForegroundColor Yellow }
function Write-Info { param($msg) Write-Host "  $msg" -ForegroundColor Gray }

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║           SDD Framework Bootstrap                        ║" -ForegroundColor Blue
Write-Host "║           Spec-Driven Development                        ║" -ForegroundColor Blue
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Blue
Write-Host ""

# Verify we're in the right place
if (-not (Test-Path ".sdd/templates")) {
    Write-Error "Cannot find .sdd/templates/. Run this script from your project root after copying .sdd/ into it."
    exit 1
}

# Create .project directory structure
Write-Step "Creating .project/ directory structure..."

$directories = @(
    ".project/adr",
    ".project/architecture",
    ".project/coding-guidelines",
    ".project/backlog",
    ".project/research",
    ".project/plans",
    ".project/changes",
    ".project/reviews",
    ".project/handoffs",
    ".project/completed/backlog",
    ".project/completed/research",
    ".project/completed/plans",
    ".project/completed/changes",
    ".project/completed/reviews"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Info "Created $dir"
    }
}
Write-Done "Directory structure created"

# Copy templates with project name substitution
Write-Step "Setting up project files from templates..."

function Copy-Template {
    param(
        [string]$Source,
        [string]$Destination,
        [switch]$Substitute
    )

    if ((Test-Path $Destination) -and -not $Force) {
        Write-Skip $Destination
        return
    }

    $content = Get-Content $Source -Raw

    if ($Substitute) {
        $content = $content -replace '\[Project Name\]', $ProjectName
        $content = $content -replace '\[project-name\]', $ProjectName.ToLower()
        $content = $content -replace 'YYYY-MM-DD', (Get-Date -Format 'yyyy-MM-dd')
    }

    Set-Content -Path $Destination -Value $content -NoNewline
    Write-Done "Created $Destination"
}

# Core project files
Copy-Template -Source ".sdd/templates/VISION.md" -Destination ".project/VISION.md" -Substitute
Copy-Template -Source ".sdd/templates/STATUS.md" -Destination ".project/STATUS.md" -Substitute
Copy-Template -Source ".sdd/templates/AGENTS.md" -Destination "AGENTS.md" -Substitute

# Create .project/README.md
$projectReadme = @"
# $ProjectName Project Documentation

This directory contains all project-specific documentation and work tracking.

## Quick Links

| Document | Purpose |
|----------|---------|
| [VISION.md](VISION.md) | Product vision and success criteria |
| [STATUS.md](STATUS.md) | Current project state |
| [architecture/](architecture/) | System architecture |
| [adr/](adr/) | Architecture Decision Records |
| [coding-guidelines/](coding-guidelines/) | Language-specific standards |

## Work Tracking

| Folder | Contains |
|--------|----------|
| [backlog/](backlog/) | Work items to be done |
| [research/](research/) | Research artifacts |
| [plans/](plans/) | Implementation plans |
| [changes/](changes/) | Change documentation |
| [reviews/](reviews/) | Review results |
| [handoffs/](handoffs/) | Session handoff notes |
| [completed/](completed/) | Archived completed work |

## For AI Assistants

Read these in order:
1. ``VISION.md`` - What we're building
2. ``STATUS.md`` - Where we are
3. ``architecture/`` - How it works
4. Relevant ADRs for your task
"@

if (-not (Test-Path ".project/README.md") -or $Force) {
    Set-Content -Path ".project/README.md" -Value $projectReadme -NoNewline
    Write-Done "Created .project/README.md"
} else {
    Write-Skip ".project/README.md"
}

# Git commit template
if (-not $SkipGitConfig) {
    Write-Step "Configuring git commit template..."
    if (Test-Path ".git") {
        git config commit.template .sdd/templates/commit-message.txt
        Write-Done "Git commit template configured"
    } else {
        Write-Info "No .git directory found, skipping git config"
    }
}

# Copy .github/agents if they exist in reference and not in project
if ((Test-Path ".sdd/../.github/agents") -and -not (Test-Path ".github/agents")) {
    Write-Step "Copying agent definitions..."
    New-Item -ItemType Directory -Path ".github/agents" -Force | Out-Null
    Copy-Item ".sdd/../.github/agents/*" ".github/agents/" -Force
    Write-Done "Agent definitions copied to .github/agents/"
}

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║           Bootstrap Complete! 🎉                         ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Edit .project/VISION.md with your project vision" -ForegroundColor Gray
Write-Host "  2. Run @backlog-builder to create initial backlog items" -ForegroundColor Gray
Write-Host "  3. Use @next-backlog-item to pick your first task" -ForegroundColor Gray
Write-Host ""
Write-Host "See .sdd/README.md for the full workflow." -ForegroundColor Gray
Write-Host ""
