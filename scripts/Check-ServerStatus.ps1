<#
.SYNOPSIS
    Check the health status of all Aura services.

.DESCRIPTION
    Checks the health of: API server, Database, RAG service, Ollama, and index status.
    Displays a summary table with status for each service.

.PARAMETER BaseUrl
    The API base URL. Defaults to http://localhost:5300

.PARAMETER DirectoryPath
    Optional directory path to check RAG index status for.

.EXAMPLE
    .\Check-ServerStatus.ps1

.EXAMPLE
    .\Check-ServerStatus.ps1 -DirectoryPath "C:\work\Brightsword"
#>

param(
    [string]$BaseUrl = "http://localhost:5300",
    [string]$DirectoryPath = ""
)

$ErrorActionPreference = "SilentlyContinue"

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET"
    )
    
    $result = @{
        Name = $Name
        Url = $Url
        Status = "Unknown"
        StatusCode = $null
        Details = ""
        Healthy = $false
    }
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method $Method -TimeoutSec 10 -ErrorAction Stop
        $result.StatusCode = $response.StatusCode
        $result.Status = "✅ Healthy"
        $result.Healthy = $true
        
        # Parse JSON response for details
        try {
            $json = $response.Content | ConvertFrom-Json
            if ($json.status) {
                $result.Details = $json.status
            } elseif ($json.healthy -ne $null) {
                # Check actual healthy flag (for agents endpoint especially)
                if (-not $json.healthy) {
                    $result.Status = "❌ Failed"
                    $result.Healthy = $false
                }
                # Build detailed message for agents endpoint
                if ($json.foundationHealthy -ne $null) {
                    $foundationStatus = if ($json.foundationHealthy) { "F:✓" } else { "F:✗" }
                    $developerStatus = if ($json.developerHealthy) { "D:✓" } else { "D:✗" }
                    $agentCount = if ($json.agents) { $json.agents.Count } else { 0 }
                    $availableCount = if ($json.agents) { ($json.agents | Where-Object { $_.available }).Count } else { 0 }
                    $result.Details = "$foundationStatus $developerStatus ($availableCount/$agentCount agents)"
                    if (-not $json.healthy -and $json.details) {
                        $result.Details += " - $($json.details)"
                    }
                } else {
                    $result.Details = if ($json.healthy) { "healthy" } else { "unhealthy" }
                }
            } elseif ($json.totalChunks -ne $null) {
                $result.Details = "Chunks: $($json.totalChunks), Files: $($json.totalFiles)"
            } elseif ($json.isIndexed -ne $null) {
                $result.Details = if ($json.isIndexed) { "Indexed ($($json.chunkCount) chunks)" } else { "Not indexed" }
            } elseif ($json.activeJobs -ne $null) {
                $result.Details = "Active: $($json.activeJobs), Completed: $($json.completedJobs)"
            } else {
                $result.Details = ($response.Content | Out-String).Substring(0, [Math]::Min(50, $response.Content.Length))
            }
        } catch {
            $result.Details = "OK"
        }
    }
    catch {
        $result.Status = "❌ Failed"
        $result.Healthy = $false
        if ($_.Exception.Response) {
            $result.StatusCode = [int]$_.Exception.Response.StatusCode
            $result.Details = $_.Exception.Response.StatusCode.ToString()
        } else {
            $result.Details = "Connection refused"
        }
    }
    
    return $result
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    AURA SERVER STATUS CHECK                      ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

# Define endpoints to check
$endpoints = @(
    @{ Name = "API Server"; Url = "$BaseUrl/health" },
    @{ Name = "Database"; Url = "$BaseUrl/health/db" },
    @{ Name = "RAG Service"; Url = "$BaseUrl/health/rag" },
    @{ Name = "Ollama LLM"; Url = "$BaseUrl/health/ollama" },
    @{ Name = "Agents"; Url = "$BaseUrl/health/agents" },
    @{ Name = "RAG Stats"; Url = "$BaseUrl/api/rag/stats" },
    @{ Name = "Index Jobs"; Url = "$BaseUrl/api/index/status" }
)

# Add directory-specific check if provided
if ($DirectoryPath) {
    $encodedPath = [System.Web.HttpUtility]::UrlEncode($DirectoryPath)
    $endpoints += @{ Name = "Directory Index"; Url = "$BaseUrl/api/rag/stats/directory?path=$encodedPath" }
}

$results = @()

foreach ($endpoint in $endpoints) {
    Write-Host "Checking $($endpoint.Name)..." -NoNewline -ForegroundColor Gray
    $result = Test-Endpoint -Name $endpoint.Name -Url $endpoint.Url
    $results += $result
    
    if ($result.Healthy) {
        Write-Host " OK" -ForegroundColor Green
    } else {
        Write-Host " FAILED" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Display results table
$tableFormat = "{0,-20} {1,-12} {2,-8} {3}"
Write-Host ($tableFormat -f "SERVICE", "STATUS", "CODE", "DETAILS") -ForegroundColor Yellow
Write-Host ("-" * 70) -ForegroundColor Gray

foreach ($result in $results) {
    $statusColor = if ($result.Healthy) { "Green" } else { "Red" }
    $code = if ($result.StatusCode) { $result.StatusCode } else { "-" }
    
    Write-Host ("{0,-20} " -f $result.Name) -NoNewline
    Write-Host ("{0,-12} " -f $result.Status) -NoNewline -ForegroundColor $statusColor
    Write-Host ("{0,-8} " -f $code) -NoNewline
    Write-Host $result.Details -ForegroundColor Gray
}

Write-Host ""

# Summary
$healthyCount = ($results | Where-Object { $_.Healthy }).Count
$totalCount = $results.Count

if ($healthyCount -eq $totalCount) {
    Write-Host "✅ All $totalCount services are healthy!" -ForegroundColor Green
} else {
    Write-Host "⚠️  $healthyCount/$totalCount services healthy" -ForegroundColor Yellow
    $failed = $results | Where-Object { -not $_.Healthy }
    Write-Host "   Failed: $($failed.Name -join ', ')" -ForegroundColor Red
}

Write-Host ""

# Return exit code
if ($healthyCount -eq $totalCount) {
    exit 0
} else {
    exit 1
}
