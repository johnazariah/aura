# Check Server Status

Check the health of all Aura services: API server, RAG, Ollama, and PostgreSQL.

## Quick Check (Recommended)

Run the PowerShell script for a complete status summary:

```powershell
c:\work\aura\scripts\Check-ServerStatus.ps1
```

With directory index check:
```powershell
c:\work\aura\scripts\Check-ServerStatus.ps1 -DirectoryPath "C:\work\Brightsword"
```

## API Base URL

```
http://localhost:5300
```

## Individual Commands

### Overall Health
```powershell
curl http://localhost:5300/health
```

### Database Health
```powershell
curl http://localhost:5300/health/db
```

### RAG Service Health
```powershell
curl http://localhost:5300/health/rag
```

### Ollama Health
```powershell
curl http://localhost:5300/health/ollama
```

### Critical Agents Health
Check if required agents (enrichment, analysis, coding, chat) are available:
```powershell
curl http://localhost:5300/health/agents
```

### RAG Index Stats (content summary)
```powershell
curl http://localhost:5300/api/rag/stats
```

### RAG Directory Index Status
Check if a specific directory has been indexed:
```powershell
curl "http://localhost:5300/api/rag/stats/directory?path=C:\work\Brightsword"
```

### Background Index Status
```powershell
curl http://localhost:5300/api/index/status
```

## Expected Responses

- **Healthy**: `200 OK` with status details
- **Unhealthy**: `503 Service Unavailable` or connection refused
