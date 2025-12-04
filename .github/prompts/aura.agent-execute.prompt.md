# Execute Agent

Execute an agent with a prompt.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Agents.ps1 -Action Execute -AgentId echo-agent -Prompt "Hello!"

# With workspace context
c:\work\aura\scripts\Manage-Agents.ps1 -Action Execute -AgentId coding-agent -Prompt "Review this code" -WorkspacePath "C:\work\Brightsword"
```

## API Endpoint

```
POST http://localhost:5300/api/agents/{agentId}/execute
```

## Request Body

```json
{
  "prompt": "Your prompt or question",
  "workspacePath": "C:\\work\\YourRepo"
}
```

## Commands

### Basic Execution
```powershell
$body = @{
    prompt = "Write a function to calculate fibonacci numbers"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/agents/coding-agent/execute" -Method Post -Body $body -ContentType "application/json"
```

### With Workspace Context
```powershell
$body = @{
    prompt = "Review the code in this project and suggest improvements"
    workspacePath = "C:\work\Brightsword"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/agents/code-review-agent/execute" -Method Post -Body $body -ContentType "application/json"
```

### With curl
```powershell
curl -X POST http://localhost:5300/api/agents/echo-agent/execute -H "Content-Type: application/json" -d "{\"prompt\":\"Hello, agent!\"}"
```

### Execute Different Agents
```powershell
# Echo agent (for testing)
curl -X POST http://localhost:5300/api/agents/echo-agent/execute -H "Content-Type: application/json" -d "{\"prompt\":\"Test message\"}"

# Documentation agent
curl -X POST http://localhost:5300/api/agents/documentation-agent/execute -H "Content-Type: application/json" -d "{\"prompt\":\"Document this class...\"}"

# Code review agent
curl -X POST http://localhost:5300/api/agents/code-review-agent/execute -H "Content-Type: application/json" -d "{\"prompt\":\"Review this code...\"}"
```

## Response

Returns the agent's response:
```json
{
  "response": "Here's the fibonacci function...",
  "agentId": "coding-agent",
  "model": "qwen2.5-coder:7b",
  "tokensUsed": 256,
  "duration": "00:00:05.123"
}
```

## Error Responses

- `404 Not Found`: Agent with the specified ID does not exist
- `500 Internal Server Error`: Agent execution failed
