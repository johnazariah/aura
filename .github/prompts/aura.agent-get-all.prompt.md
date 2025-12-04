# Get All Agents

Retrieve all registered agents from the Aura system.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Agents.ps1 -Action List

# Filter by capability and language
c:\work\aura\scripts\Manage-Agents.ps1 -Action List -Capability coding -Language rust
```

## API Endpoint

```
GET http://localhost:5300/api/agents
```

## Optional Query Parameters

- `capability`: Filter by capability (e.g., "coding", "testing", "documentation")
- `language`: Filter by programming language (e.g., "csharp", "typescript", "rust")

## Commands

### Get All Agents
```powershell
curl http://localhost:5300/api/agents
```

### Filter by Capability
```powershell
curl "http://localhost:5300/api/agents?capability=coding"
```

### Filter by Language
```powershell
curl "http://localhost:5300/api/agents?language=csharp"
```

### Filter by Both
```powershell
curl "http://localhost:5300/api/agents?capability=coding&language=rust"
```

## Response

Returns an array of agent definitions:
```json
[
  {
    "id": "coding-agent",
    "name": "Coding Agent",
    "description": "General-purpose coding agent",
    "capabilities": ["coding", "refactoring"],
    "languages": ["csharp", "typescript"],
    "model": "qwen2.5-coder:7b",
    "provider": "ollama"
  }
]
```
