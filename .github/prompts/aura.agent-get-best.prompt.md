# Get Best Agent for Capability

Find the best agent for a specific capability, optionally filtered by language.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Agents.ps1 -Action Best -Capability coding

# With language preference
c:\work\aura\scripts\Manage-Agents.ps1 -Action Best -Capability coding -Language rust
```

## API Endpoint

```
GET http://localhost:5300/api/agents/best?capability={capability}&language={language}
```

## Required Query Parameters

- `capability`: The capability needed (e.g., "coding", "testing", "documentation", "code-review")

## Optional Query Parameters

- `language`: Programming language preference (e.g., "csharp", "typescript", "rust", "python")

## Commands

### Get Best Coding Agent
```powershell
curl "http://localhost:5300/api/agents/best?capability=coding"
```

### Get Best Agent for C# Coding
```powershell
curl "http://localhost:5300/api/agents/best?capability=coding&language=csharp"
```

### Get Best Agent for Rust
```powershell
curl "http://localhost:5300/api/agents/best?capability=coding&language=rust"
```

### Get Best Code Review Agent
```powershell
curl "http://localhost:5300/api/agents/best?capability=code-review"
```

### Get Best Documentation Agent
```powershell
curl "http://localhost:5300/api/agents/best?capability=documentation"
```

## Response

Returns the best matching agent:
```json
{
  "id": "rust-coding-agent",
  "name": "Rust Coding Agent",
  "description": "Specialized agent for Rust development",
  "capabilities": ["coding"],
  "languages": ["rust"],
  "model": "qwen2.5-coder:7b",
  "provider": "ollama"
}
```

## Error Responses

- `404 Not Found`: No agent found matching the specified capability/language
