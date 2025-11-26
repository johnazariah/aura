# Start Aura API Server

Start the Aura API server in a new terminal for local development.

## Prerequisites
- .NET 9+ SDK installed
- Run from the `aura` repository root

## Command

```powershell
cd c:\work\aura
dotnet run --project src/Aura.Api
```

## Expected Output

The server will start on `http://localhost:5300` with:
- `/health` - Overall health check
- `/health/db` - Database health
- `/health/rag` - RAG index health  
- `/health/ollama` - Ollama LLM health
- `/api/agents` - List registered agents
- `/api/agents/{id}` - Get agent details
- `/api/agents/{id}/execute` - Execute an agent

## Verification

Test the health endpoint:
```powershell
Invoke-RestMethod http://localhost:5300/health | ConvertTo-Json
```

Test the agents endpoint:
```powershell
Invoke-RestMethod http://localhost:5300/api/agents | ConvertTo-Json
```

## Notes
- The server uses the stub LLM provider by default (no Ollama required)
- Agent files are loaded from `../../agents` relative to the API project
- Hot-reload is enabled - add/modify `.md` files in `agents/` to see changes
