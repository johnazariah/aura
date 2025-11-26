<#
.SYNOPSIS
    Build and run integration tests with TRX output.

.DESCRIPTION
    Runs long-running integration tests and saves results to a timestamped TRX file.
    Use the -Monitor switch in another terminal to watch progress via the API.

.PARAMETER Filter
    Optional test filter expression

.PARAMETER Timeout
    Test timeout in minutes (default: 30)

.PARAMETER OutputDir
    Directory for TRX files (default: TestResults)

.EXAMPLE
    .\scripts\Run-IntegrationTests.ps1

.EXAMPLE
    .\scripts\Run-IntegrationTests.ps1 -Timeout 60

.EXAMPLE
    # In another terminal while tests are running:
    curl http://localhost:5300/health
#>

[CmdletBinding()]
param(
    [string]$Filter,
    [int]$Timeout = 30,
    [string]$OutputDir = "TestResults"
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$trxFileName = "IntegrationTests_$timestamp.trx"

Write-Host "Building and running integration tests..." -ForegroundColor Cyan
Write-Host "Results will be saved to: $OutputDir\$trxFileName" -ForegroundColor Gray
Write-Host "Timeout: $Timeout minutes" -ForegroundColor Gray
Write-Host ""
Write-Host "TIP: Run 'curl http://localhost:5300/health' in another terminal to check API status" -ForegroundColor DarkGray
Write-Host ""

Push-Location $projectRoot
try {
    # Ensure output directory exists
    $fullOutputDir = Join-Path $projectRoot $OutputDir
    if (-not (Test-Path $fullOutputDir)) {
        New-Item -ItemType Directory -Path $fullOutputDir | Out-Null
    }

    # Build first
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    # Run integration tests
    Write-Host ""
    Write-Host "Running integration tests (this may take a while)..." -ForegroundColor Yellow
    Write-Host "Started at: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
    
    $testArgs = @(
        "test"
        "--no-build"
        "--verbosity", "normal"
        "--logger", "trx;LogFileName=$trxFileName"
        "--logger", "console;verbosity=normal"
        "--results-directory", $fullOutputDir
        "--blame-hang-timeout", "${Timeout}m"
    )

    # Filter for integration tests (by convention, or explicit filter)
    if ($Filter) {
        $testArgs += "--filter"
        $testArgs += $Filter
    }
    else {
        # Default: run tests marked as Integration or in Integration test projects
        # Adjust this filter based on your test naming convention
        $testArgs += "--filter"
        $testArgs += "Category=Integration|FullyQualifiedName~Integration"
    }

    $startTime = Get-Date
    & dotnet @testArgs
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host ""
    Write-Host "Completed at: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
    Write-Host "Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
    Write-Host ""

    $trxPath = Join-Path $fullOutputDir $trxFileName
    if (Test-Path $trxPath) {
        Write-Host "Results saved to: $trxPath" -ForegroundColor Green
        
        # Parse TRX for summary (basic XML parsing)
        try {
            [xml]$trx = Get-Content $trxPath
            $ns = @{t = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
            $counters = Select-Xml -Xml $trx -XPath "//t:ResultSummary/t:Counters" -Namespace $ns
            if ($counters) {
                $c = $counters.Node
                Write-Host ""
                Write-Host "Test Summary:" -ForegroundColor Cyan
                Write-Host "  Total:    $($c.total)" -ForegroundColor White
                Write-Host "  Passed:   $($c.passed)" -ForegroundColor Green
                Write-Host "  Failed:   $($c.failed)" -ForegroundColor $(if ([int]$c.failed -gt 0) { "Red" } else { "Green" })
                Write-Host "  Skipped:  $($c.notExecuted)" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "Could not parse TRX summary" -ForegroundColor DarkGray
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Some integration tests failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "All integration tests passed!" -ForegroundColor Green
}
finally {
    Pop-Location
}
