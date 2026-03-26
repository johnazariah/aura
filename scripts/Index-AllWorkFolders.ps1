#!/usr/bin/env pwsh
# Registers all folders under C:\work as Aura workspaces and triggers indexing.
# Usage: .\scripts\Index-AllWorkFolders.ps1 [-RootPath C:\work] [-SkipPattern '*-workflow-*']

param(
    [string]$RootPath = "C:\work",
    [string]$SkipPattern = "*-workflow-*",
    [string]$AuraUrl = "http://localhost:5300"
)

$ErrorActionPreference = "Stop"

$client = [System.Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]::new($AuraUrl)
$client.Timeout = [TimeSpan]::FromSeconds(30)

# Health check
try {
    $health = $client.GetAsync("/health").Result
    if (-not $health.IsSuccessStatusCode) {
        Write-Error "Aura is not healthy at $AuraUrl"
        exit 1
    }
    Write-Host "Aura is healthy at $AuraUrl" -ForegroundColor Green
}
catch {
    Write-Error "Cannot reach Aura at $AuraUrl - is the service running?"
    exit 1
}

$dirs = Get-ChildItem $RootPath -Directory | Where-Object {
    $_.Name -notlike $SkipPattern
}

Write-Host "Found $($dirs.Count) folders under $RootPath`n"

$ok = 0
$skip = 0
$err = 0

foreach ($dir in $dirs) {
    $name = $dir.Name
    $path = ($dir.FullName -replace '\\', '/')
    $body = "{`"path`":`"$path`",`"alias`":`"$name`",`"tags`":[`"code`"]}"
    $content = [System.Net.Http.StringContent]::new(
        $body,
        [System.Text.Encoding]::UTF8,
        "application/json"
    )

    try {
        $resp = $client.PostAsync("/api/workspaces", $content).Result
        $result = $resp.Content.ReadAsStringAsync().Result
        if ($resp.IsSuccessStatusCode) {
            Write-Host "  [OK]   $name" -ForegroundColor Green
            $ok++
        }
        else {
            Write-Host "  [SKIP] $name ($($resp.StatusCode))" -ForegroundColor Yellow
            $skip++
        }
    }
    catch {
        Write-Host "  [ERR]  $name - $($_.Exception.InnerException.Message)" -ForegroundColor Red
        $err++
    }
}

$client.Dispose()

Write-Host "`n--- Summary ---"
Write-Host "Registered: $ok" -ForegroundColor Green
Write-Host "Skipped:    $skip" -ForegroundColor Yellow
Write-Host "Errors:     $err" -ForegroundColor Red

# Now check indexing status
Write-Host "`nIndexing jobs will process in the background."
Write-Host "Monitor with: curl $AuraUrl/api/workspaces"
