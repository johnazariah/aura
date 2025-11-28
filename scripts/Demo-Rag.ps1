# Aura RAG Demo - Silver Thread Experience
# This script demonstrates the end-to-end RAG workflow

param(
    [string]$ApiUrl = "http://localhost:5000"
)

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Aura RAG Demo - Ask About Your Code" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check API health
Write-Host "[1/4] Checking API health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -Method Get
    Write-Host "   API Status: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "   ERROR: API not available at $ApiUrl" -ForegroundColor Red
    Write-Host "   Start the API with: dotnet run --project src/Aura.AppHost" -ForegroundColor Yellow
    exit 1
}

# Step 2: Check RAG health
Write-Host "[2/4] Checking RAG service..." -ForegroundColor Yellow
try {
    $ragHealth = Invoke-RestMethod -Uri "$ApiUrl/health/rag" -Method Get
    Write-Host "   RAG Status: $($ragHealth.healthy)" -ForegroundColor Green
    Write-Host "   Chunks indexed: $($ragHealth.totalChunks)" -ForegroundColor Gray
} catch {
    Write-Host "   RAG service check failed" -ForegroundColor Red
}

# Step 3: Index the codebase
Write-Host "[3/4] Indexing Aura codebase..." -ForegroundColor Yellow
$indexRequest = @{
    Path = (Get-Location).Path + "/src"
    IncludePatterns = @("*.cs")
    Recursive = $true
} | ConvertTo-Json

try {
    $indexResult = Invoke-RestMethod -Uri "$ApiUrl/api/rag/index/directory" -Method Post -Body $indexRequest -ContentType "application/json"
    Write-Host "   Indexed $($indexResult.filesIndexed) files" -ForegroundColor Green
} catch {
    Write-Host "   Indexing failed: $_" -ForegroundColor Red
}

# Step 4: Ask a question about the codebase
Write-Host "[4/4] Asking about the codebase..." -ForegroundColor Yellow
Write-Host ""

$question = "How does the agent system work? What is an AgentContext?"

Write-Host "Question: $question" -ForegroundColor Cyan
Write-Host ""

$executeRequest = @{
    Prompt = $question
    UseRag = $true
    TopK = 5
} | ConvertTo-Json

try {
    Write-Host "Thinking..." -ForegroundColor Gray
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/agents/chat-agent/execute/rag" -Method Post -Body $executeRequest -ContentType "application/json"
    
    Write-Host ""
    Write-Host "Answer:" -ForegroundColor Green
    Write-Host $response.content
    Write-Host ""
    Write-Host "---" -ForegroundColor Gray
    Write-Host "RAG Enriched: $($response.ragEnriched)" -ForegroundColor Gray
    Write-Host "Tokens Used: $($response.tokensUsed)" -ForegroundColor Gray
    Write-Host "Duration: $($response.durationMs)ms" -ForegroundColor Gray
} catch {
    Write-Host "   Execution failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Demo complete!" -ForegroundColor Green
