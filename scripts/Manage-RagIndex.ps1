<#
.SYNOPSIS
    Manage the Aura RAG index.

.DESCRIPTION
    Index directories, query content, view stats, and clear the RAG index.

.PARAMETER Action
    The action to perform: Index, Query, Stats, Clear

.PARAMETER Path
    Directory path to index (required for Index action, optional for Stats)

.PARAMETER Query
    Search query (required for Query action)

.PARAMETER TopK
    Number of results to return for Query action (default: 5)

.PARAMETER Recursive
    Index directories recursively (default: true)

.PARAMETER Force
    Skip confirmation for Clear action

.PARAMETER BaseUrl
    The API base URL. Defaults to http://localhost:5300

.EXAMPLE
    .\Manage-RagIndex.ps1 -Action Stats

.EXAMPLE
    .\Manage-RagIndex.ps1 -Action Stats -Path "C:\work\Brightsword"

.EXAMPLE
    .\Manage-RagIndex.ps1 -Action Index -Path "C:\work\Brightsword"

.EXAMPLE
    .\Manage-RagIndex.ps1 -Action Query -Query "How does authentication work?"

.EXAMPLE
    .\Manage-RagIndex.ps1 -Action Clear -Force
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Index", "Query", "Stats", "Clear")]
    [string]$Action,
    
    [string]$Path,
    [string]$Query,
    [int]$TopK = 5,
    [bool]$Recursive = $true,
    [switch]$Force,
    [string]$BaseUrl = "http://localhost:5300"
)

$ErrorActionPreference = "Stop"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

try {
    switch ($Action) {
        "Stats" {
            Write-Header "RAG INDEX STATISTICS"
            
            # Global stats
            $stats = Invoke-RestMethod -Uri "$BaseUrl/api/rag/stats" -Method Get
            
            Write-Host "Global Index Stats:" -ForegroundColor Yellow
            Write-Host "  Total Chunks:  " -NoNewline -ForegroundColor Gray
            Write-Host $stats.totalChunks
            Write-Host "  Total Files:   " -NoNewline -ForegroundColor Gray
            Write-Host $stats.totalFiles
            
            if ($stats.directories -and $stats.directories.Count -gt 0) {
                Write-Host "  Directories:   " -ForegroundColor Gray
                foreach ($dir in $stats.directories) {
                    Write-Host "    - $dir" -ForegroundColor Gray
                }
            }
            
            # Directory-specific stats if path provided
            if ($Path) {
                Write-Host ""
                Write-Host "Directory Stats for: $Path" -ForegroundColor Yellow
                
                $encodedPath = [System.Web.HttpUtility]::UrlEncode($Path)
                $dirStats = Invoke-RestMethod -Uri "$BaseUrl/api/rag/stats/directory?path=$encodedPath" -Method Get
                
                $indexStatus = if ($dirStats.isIndexed) { "✅ Indexed" } else { "❌ Not Indexed" }
                Write-Host "  Status:        " -NoNewline -ForegroundColor Gray
                Write-Host $indexStatus
                Write-Host "  Chunk Count:   " -NoNewline -ForegroundColor Gray
                Write-Host $dirStats.chunkCount
                Write-Host "  File Count:    " -NoNewline -ForegroundColor Gray
                Write-Host $dirStats.fileCount
                if ($dirStats.lastIndexedAt) {
                    Write-Host "  Last Indexed:  " -NoNewline -ForegroundColor Gray
                    Write-Host $dirStats.lastIndexedAt
                }
            }
        }
        
        "Index" {
            if (-not $Path) {
                Write-Host "Error: -Path is required for Index action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "INDEX DIRECTORY"
            
            Write-Host "Indexing: $Path" -ForegroundColor Yellow
            Write-Host "Recursive: $Recursive" -ForegroundColor Gray
            Write-Host ""
            
            $body = @{
                path = $Path
                recursive = $Recursive
            } | ConvertTo-Json
            
            Write-Host "Starting indexing..." -ForegroundColor Gray
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            
            $result = Invoke-RestMethod -Uri "$BaseUrl/api/rag/index/directory" -Method Post -Body $body -ContentType "application/json"
            
            $stopwatch.Stop()
            
            Write-Host ""
            Write-Host "✅ Indexing complete!" -ForegroundColor Green
            Write-Host "  Files indexed: " -NoNewline -ForegroundColor Gray
            Write-Host $result.filesIndexed
            Write-Host "  Chunks created:" -NoNewline -ForegroundColor Gray
            Write-Host $result.chunksCreated
            Write-Host "  Duration:      " -NoNewline -ForegroundColor Gray
            Write-Host "$($stopwatch.Elapsed.TotalSeconds.ToString('F2')) seconds"
        }
        
        "Query" {
            if (-not $Query) {
                Write-Host "Error: -Query is required for Query action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "RAG QUERY"
            
            Write-Host "Query: $Query" -ForegroundColor Yellow
            Write-Host "TopK:  $TopK" -ForegroundColor Gray
            Write-Host ""
            
            $body = @{
                query = $Query
                topK = $TopK
            }
            if ($Path) {
                $body.sourcePath = $Path
            }
            
            $json = $body | ConvertTo-Json
            $result = Invoke-RestMethod -Uri "$BaseUrl/api/rag/query" -Method Post -Body $json -ContentType "application/json"
            
            if ($result.results -and $result.results.Count -gt 0) {
                Write-Host "Found $($result.results.Count) result(s):" -ForegroundColor Green
                Write-Host ""
                
                $i = 1
                foreach ($chunk in $result.results) {
                    Write-Host "[$i] Score: $($chunk.score.ToString('F3'))" -ForegroundColor Yellow
                    Write-Host "    Source: $($chunk.source)" -ForegroundColor Gray
                    Write-Host "    Content:" -ForegroundColor Gray
                    $preview = $chunk.content
                    if ($preview.Length -gt 200) {
                        $preview = $preview.Substring(0, 200) + "..."
                    }
                    Write-Host "    $preview" -ForegroundColor White
                    Write-Host ""
                    $i++
                }
            } else {
                Write-Host "No results found." -ForegroundColor Yellow
            }
        }
        
        "Clear" {
            Write-Header "CLEAR RAG INDEX"
            
            # Get current stats first
            $stats = Invoke-RestMethod -Uri "$BaseUrl/api/rag/stats" -Method Get
            
            if ($stats.totalChunks -eq 0) {
                Write-Host "Index is already empty." -ForegroundColor Yellow
                exit 0
            }
            
            Write-Host "Current index contains:" -ForegroundColor Yellow
            Write-Host "  Chunks: $($stats.totalChunks)" -ForegroundColor Gray
            Write-Host "  Files:  $($stats.totalFiles)" -ForegroundColor Gray
            Write-Host ""
            
            if (-not $Force) {
                $confirm = Read-Host "Are you sure you want to clear the entire index? (y/N)"
                if ($confirm -ne "y" -and $confirm -ne "Y") {
                    Write-Host "Cancelled." -ForegroundColor Yellow
                    exit 0
                }
            }
            
            Write-Host ""
            Write-Host "Clearing index..." -NoNewline
            Invoke-RestMethod -Uri "$BaseUrl/api/rag" -Method Delete | Out-Null
            Write-Host " ✅" -ForegroundColor Green
            
            Write-Host ""
            Write-Host "✅ RAG index cleared successfully!" -ForegroundColor Green
        }
    }
}
catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
