<#
.SYNOPSIS
    List and execute Aura agents.

.DESCRIPTION
    List available agents, get agent details, find the best agent for a capability, and execute agents.

.PARAMETER Action
    The action to perform: List, Get, Best, Execute, ExecuteRag

.PARAMETER AgentId
    Agent ID (required for Get, Execute, ExecuteRag actions)

.PARAMETER Capability
    Capability to search for (required for Best action)

.PARAMETER Language
    Programming language filter (optional for List, Best actions)

.PARAMETER Prompt
    Prompt to send to agent (required for Execute, ExecuteRag actions)

.PARAMETER WorkspacePath
    Workspace path for context (optional for Execute, required for ExecuteRag)

.PARAMETER TopK
    Number of RAG results to use (default: 5, for ExecuteRag)

.PARAMETER BaseUrl
    The API base URL. Defaults to http://localhost:5300

.EXAMPLE
    .\Manage-Agents.ps1 -Action List

.EXAMPLE
    .\Manage-Agents.ps1 -Action List -Capability coding -Language rust

.EXAMPLE
    .\Manage-Agents.ps1 -Action Get -AgentId coding-agent

.EXAMPLE
    .\Manage-Agents.ps1 -Action Best -Capability coding -Language csharp

.EXAMPLE
    .\Manage-Agents.ps1 -Action Execute -AgentId echo-agent -Prompt "Hello!"

.EXAMPLE
    .\Manage-Agents.ps1 -Action ExecuteRag -AgentId coding-agent -Prompt "Explain the architecture" -WorkspacePath "C:\work\Brightsword"
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("List", "Get", "Best", "Execute", "ExecuteRag")]
    [string]$Action,
    
    [string]$AgentId,
    [string]$Capability,
    [string]$Language,
    [string]$Prompt,
    [string]$WorkspacePath,
    [int]$TopK = 5,
    [string]$BaseUrl = "http://localhost:5300"
)

$ErrorActionPreference = "Stop"
$apiUrl = "$BaseUrl/api/agents"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Format-Agent {
    param($Agent, [switch]$Brief)
    
    if ($Brief) {
        $caps = if ($Agent.capabilities) { $Agent.capabilities -join ", " } else { "-" }
        $langs = if ($Agent.languages) { $Agent.languages -join ", " } else { "-" }
        Write-Host "  $($Agent.id)" -ForegroundColor Yellow -NoNewline
        Write-Host " - $($Agent.name)" -ForegroundColor White
        Write-Host "    Capabilities: $caps" -ForegroundColor Gray
        Write-Host "    Languages:    $langs" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host "ID:           " -NoNewline -ForegroundColor Gray
        Write-Host $Agent.id -ForegroundColor Yellow
        Write-Host "Name:         " -NoNewline -ForegroundColor Gray
        Write-Host $Agent.name
        if ($Agent.description) {
            Write-Host "Description:  " -NoNewline -ForegroundColor Gray
            Write-Host $Agent.description
        }
        if ($Agent.capabilities) {
            Write-Host "Capabilities: " -NoNewline -ForegroundColor Gray
            Write-Host ($Agent.capabilities -join ", ")
        }
        if ($Agent.languages) {
            Write-Host "Languages:    " -NoNewline -ForegroundColor Gray
            Write-Host ($Agent.languages -join ", ")
        }
        Write-Host "Model:        " -NoNewline -ForegroundColor Gray
        Write-Host "$($Agent.model) ($($Agent.provider))"
        if ($Agent.temperature) {
            Write-Host "Temperature:  " -NoNewline -ForegroundColor Gray
            Write-Host $Agent.temperature
        }
        Write-Host ""
    }
}

try {
    switch ($Action) {
        "List" {
            Write-Header "AVAILABLE AGENTS"
            
            $url = $apiUrl
            $params = @()
            if ($Capability) { $params += "capability=$Capability" }
            if ($Language) { $params += "language=$Language" }
            if ($params.Count -gt 0) { $url = "$apiUrl?$($params -join '&')" }
            
            $agents = Invoke-RestMethod -Uri $url -Method Get
            
            if ($agents.Count -eq 0) {
                Write-Host "No agents found." -ForegroundColor Yellow
            } else {
                Write-Host "Found $($agents.Count) agent(s):" -ForegroundColor Green
                Write-Host ""
                foreach ($agent in $agents) {
                    Format-Agent $agent -Brief
                }
            }
        }
        
        "Get" {
            if (-not $AgentId) {
                Write-Host "Error: -AgentId is required for Get action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "AGENT DETAILS"
            
            $agent = Invoke-RestMethod -Uri "$apiUrl/$AgentId" -Method Get
            Format-Agent $agent
        }
        
        "Best" {
            if (-not $Capability) {
                Write-Host "Error: -Capability is required for Best action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "BEST AGENT FOR: $Capability"
            
            $url = "$apiUrl/best?capability=$Capability"
            if ($Language) { $url += "&language=$Language" }
            
            $agent = Invoke-RestMethod -Uri $url -Method Get
            
            Write-Host "Best match:" -ForegroundColor Green
            Write-Host ""
            Format-Agent $agent
        }
        
        "Execute" {
            if (-not $AgentId) {
                Write-Host "Error: -AgentId is required for Execute action" -ForegroundColor Red
                exit 1
            }
            if (-not $Prompt) {
                Write-Host "Error: -Prompt is required for Execute action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "EXECUTE AGENT: $AgentId"
            
            Write-Host "Prompt: $Prompt" -ForegroundColor Yellow
            if ($WorkspacePath) {
                Write-Host "Workspace: $WorkspacePath" -ForegroundColor Gray
            }
            Write-Host ""
            Write-Host "Executing..." -ForegroundColor Gray
            
            $body = @{ prompt = $Prompt }
            if ($WorkspacePath) { $body.workspacePath = $WorkspacePath }
            $json = $body | ConvertTo-Json
            
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $result = Invoke-RestMethod -Uri "$apiUrl/$AgentId/execute" -Method Post -Body $json -ContentType "application/json"
            $stopwatch.Stop()
            
            Write-Host ""
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
            Write-Host "  RESPONSE" -ForegroundColor Green
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
            Write-Host ""
            Write-Host $result.response
            Write-Host ""
            Write-Host "---" -ForegroundColor Gray
            Write-Host "Duration: $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Gray
        }
        
        "ExecuteRag" {
            if (-not $AgentId) {
                Write-Host "Error: -AgentId is required for ExecuteRag action" -ForegroundColor Red
                exit 1
            }
            if (-not $Prompt) {
                Write-Host "Error: -Prompt is required for ExecuteRag action" -ForegroundColor Red
                exit 1
            }
            if (-not $WorkspacePath) {
                Write-Host "Error: -WorkspacePath is required for ExecuteRag action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "EXECUTE AGENT WITH RAG: $AgentId"
            
            Write-Host "Prompt:    $Prompt" -ForegroundColor Yellow
            Write-Host "Workspace: $WorkspacePath" -ForegroundColor Gray
            Write-Host "TopK:      $TopK" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Executing with RAG context..." -ForegroundColor Gray
            
            $body = @{
                prompt = $Prompt
                workspacePath = $WorkspacePath
                topK = $TopK
            } | ConvertTo-Json
            
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $result = Invoke-RestMethod -Uri "$apiUrl/$AgentId/execute/rag" -Method Post -Body $json -ContentType "application/json"
            $stopwatch.Stop()
            
            Write-Host ""
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
            Write-Host "  RESPONSE (with RAG)" -ForegroundColor Green
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
            Write-Host ""
            Write-Host $result.response
            Write-Host ""
            Write-Host "---" -ForegroundColor Gray
            Write-Host "Duration: $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Gray
            if ($result.ragContext) {
                Write-Host "RAG chunks used: $($result.ragContext.chunksUsed)" -ForegroundColor Gray
            }
        }
    }
}
catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
