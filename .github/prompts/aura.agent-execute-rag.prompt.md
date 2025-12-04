# Execute Agent with RAG

Execute an agent with RAG (Retrieval Augmented Generation) context from the indexed codebase.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Agents.ps1 -Action ExecuteRag -AgentId coding-agent -Prompt "Explain the architecture" -WorkspacePath "C:\work\Brightsword"

# With custom TopK
c:\work\aura\scripts\Manage-Agents.ps1 -Action ExecuteRag -AgentId coding-agent -Prompt "List all models" -WorkspacePath "C:\work\Brightsword" -TopK 10
```

## API Endpoint

```
POST http://localhost:5300/api/agents/{agentId}/execute/rag
```

## Request Body

```json
{
  "prompt": "Your prompt or question",
  "workspacePath": "C:\\work\\YourRepo",
  "useRag": true,
  "topK": 5
}
```

## Parameters

- `prompt`: The question or task for the agent
- `workspacePath`: Path to the workspace (used for RAG context)
- `useRag`: Whether to use RAG (default: true for this endpoint)
- `topK`: Number of relevant chunks to retrieve (default: 5)

## Commands

### Basic RAG Execution
```powershell
$body = @{
    prompt = "How does the authentication system work in this codebase?"
    workspacePath = "C:\work\Brightsword"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/agents/coding-agent/execute/rag" -Method Post -Body $body -ContentType "application/json"
```

### With Custom TopK
```powershell
$body = @{
    prompt = "What are all the database models in this project?"
    workspacePath = "C:\work\Brightsword"
    topK = 10
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/agents/coding-agent/execute/rag" -Method Post -Body $body -ContentType "application/json"
```

### With curl
```powershell
curl -X POST http://localhost:5300/api/agents/coding-agent/execute/rag -H "Content-Type: application/json" -d "{\"prompt\":\"Explain the main architecture\",\"workspacePath\":\"C:\\\\work\\\\Brightsword\",\"topK\":5}"
```

## Prerequisites

**Important**: The workspace must be indexed first before using RAG execution!

Check if indexed:
```powershell
curl "http://localhost:5300/api/rag/stats/directory?path=C:\work\Brightsword"
```

Index if needed:
```powershell
$body = @{ path = "C:\work\Brightsword"; recursive = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5300/api/rag/index/directory" -Method Post -Body $body -ContentType "application/json"
```

## Response

Returns the agent's response with RAG context:
```json
{
  "response": "Based on the codebase, the authentication system uses...",
  "agentId": "coding-agent",
  "model": "qwen2.5-coder:7b",
  "ragContext": {
    "chunksUsed": 5,
    "sources": [
      "Services/AuthService.cs",
      "Models/User.cs"
    ]
  },
  "tokensUsed": 512,
  "duration": "00:00:08.456"
}
```

## Error Responses

- `404 Not Found`: Agent not found
- `400 Bad Request`: Workspace path required for RAG
- `500 Internal Server Error`: Execution failed
