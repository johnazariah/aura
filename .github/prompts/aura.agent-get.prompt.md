# Get Agent by ID

Retrieve a specific agent by its ID.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Agents.ps1 -Action Get -AgentId coding-agent
```

## API Endpoint

```
GET http://localhost:5300/api/agents/{agentId}
```

## Command

```powershell
curl http://localhost:5300/api/agents/coding-agent
```

Other examples:
```powershell
curl http://localhost:5300/api/agents/echo-agent
curl http://localhost:5300/api/agents/code-review-agent
curl http://localhost:5300/api/agents/documentation-agent
curl http://localhost:5300/api/agents/rust-coding-agent
```

## Response

Returns the agent definition:
```json
{
  "id": "coding-agent",
  "name": "Coding Agent",
  "description": "General-purpose coding agent for writing and modifying code",
  "capabilities": ["coding", "refactoring", "debugging"],
  "languages": ["csharp", "typescript", "python"],
  "systemPrompt": "You are an expert software developer...",
  "model": "qwen2.5-coder:7b",
  "provider": "ollama",
  "temperature": 0.7
}
```

## Error Responses

- `404 Not Found`: Agent with the specified ID does not exist
