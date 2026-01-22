<#
.SYNOPSIS
    Build and run all unit tests.

.DESCRIPTION
    Builds the solution and runs all unit tests with detailed output.

.PARAMETER Filter
    Optional test filter expression (e.g., "FullyQualifiedName~AgentRegistry")

.PARAMETER Coverage
    Generate code coverage report

.EXAMPLE
    .\scripts\Run-UnitTests.ps1

.EXAMPLE
    .\scripts\Run-UnitTests.ps1 -Filter "FullyQualifiedName~MarkdownLoader"

.EXAMPLE
    .\scripts\Run-UnitTests.ps1 -Coverage
#>

[CmdletBinding()]
param(
    [string]$Filter,
    [switch]$Coverage
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Building and running unit tests..." -ForegroundColor Cyan
Write-Host ""

Push-Location $projectRoot
try {
    # Build first
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    # Run tests (exclude integration tests)
    Write-Host ""
    Write-Host "Running unit tests..." -ForegroundColor Yellow
    
    $unitTestProjects = @(
        "tests/Aura.Foundation.Tests"
        "tests/Aura.Module.Developer.Tests"
    )

    $allPassed = $true
    foreach ($project in $unitTestProjects) {
        $testArgs = @(
            "test"
            $project
            "--no-build"
            "--verbosity", "normal"
            "--logger", "console;verbosity=detailed"
        )

        if ($Filter) {
            $testArgs += "--filter"
            $testArgs += $Filter
        }

        if ($Coverage) {
            $testArgs += "--collect:XPlat Code Coverage"
        }

        & dotnet @testArgs

        if ($LASTEXITCODE -ne 0) {
            $allPassed = $false
        }
    }

    if (-not $allPassed) {
        Write-Host ""
        Write-Host "Some tests failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
}
finally {
    Pop-Location
}
