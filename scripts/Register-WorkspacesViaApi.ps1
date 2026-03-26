#!/usr/bin/env pwsh
# Registers all folders under C:\work as Aura workspaces via REST API
# and also adds them to the workspace registry (JSON file).
# Usage: .\scripts\Register-WorkspacesViaApi.ps1

param(
    [string]$RootPath = "C:\work",
    [string]$AuraUrl = "http://localhost:5300"
)

$ErrorActionPreference = "Stop"

$client = [System.Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]::new($AuraUrl)
$client.Timeout = [TimeSpan]::FromSeconds(30)

# Get currently registered workspaces from workspace registry
$registryPath = Join-Path $env:USERPROFILE ".config" "aura" "workspaces.json"
$existing = @()
if (Test-Path $registryPath) {
    $registry = Get-Content $registryPath -Raw | ConvertFrom-Json
    if ($registry.workspaces) {
        $existing = $registry.workspaces | ForEach-Object { $_.path }
    }
}

$dirs = Get-ChildItem $RootPath -Directory | Where-Object {
    $_.Name -notlike "*-workflow-*"
}

Write-Host "Found $($dirs.Count) folders, $($existing.Count) already registered`n"

$added = 0
$skipped = 0

foreach ($dir in $dirs) {
    $name = $dir.Name
    $path = ($dir.FullName -replace '\\', '/').ToLower()

    if ($existing -contains $path) {
        Write-Host "  [HAVE] $name" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    # Register via REST API (triggers DB creation + indexing)
    $body = "{`"path`":`"$($dir.FullName -replace '\\','/')`"}"
    $content = [System.Net.Http.StringContent]::new(
        $body,
        [System.Text.Encoding]::UTF8,
        "application/json"
    )

    try {
        $resp = $client.PostAsync("/api/workspaces", $content).Result
        $result = $resp.Content.ReadAsStringAsync().Result
        $json = $result | ConvertFrom-Json

        if ($resp.IsSuccessStatusCode -and $json.id) {
            Write-Host "  [OK]   $name (id: $($json.id))" -ForegroundColor Green
            $added++
        }
        else {
            Write-Host "  [SKIP] $name" -ForegroundColor Yellow
            $skipped++
        }
    }
    catch {
        Write-Host "  [ERR]  $name - $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}

$client.Dispose()

Write-Host "`n--- Summary ---"
Write-Host "Added:   $added" -ForegroundColor Green
Write-Host "Skipped: $skipped" -ForegroundColor Yellow
Write-Host "`nBackground indexing in progress. Monitor:"
Write-Host "  aura_workspace list          (via MCP)"
Write-Host "  aura_workspace status        (via MCP)"
