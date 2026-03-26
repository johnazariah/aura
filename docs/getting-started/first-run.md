# First Run

After installation, verify that all components are healthy before indexing your first project.

## 1. Check Services (Windows)

```powershell
Get-Service AuraDB, AuraService | Format-Table Name, Status
```

Both should show **Running**. If not:

```powershell
Start-Service AuraDB
Start-Service AuraService
```

## 2. Verify Health Endpoints

```powershell
# Overall health
curl http://localhost:5300/health

# Database connection
curl http://localhost:5300/health/db

# RAG subsystem
curl http://localhost:5300/health/rag

# MCP handler and tool count
curl http://localhost:5300/health/mcp
```

All should return `200 OK`. The `/health/mcp` response lists the registered tools and their count (expect 10).

## 3. Confirm Ollama Models

```powershell
ollama list
```

You should see `nomic-embed-text` (required for embeddings). If missing:

```powershell
ollama pull nomic-embed-text
```

Optional but recommended for code generation tasks:

```powershell
ollama pull qwen2.5-coder:7b
```

## 4. Index Your First Folder

Pick a project directory and index it via the REST API:

```powershell
# Register a workspace
curl -X POST http://localhost:5300/api/workspaces `
  -H "Content-Type: application/json" `
  -d '{"path": "C:/projects/my-app", "startIndexing": true}'
```

This creates the workspace and immediately begins indexing. Check progress:

```powershell
curl http://localhost:5300/api/workspaces/{workspaceId}/index
```

The response shows `fresh`, `stale`, or `not-indexed` status, plus chunk and graph node counts.

## 5. Run a Test Search

```powershell
curl -X POST http://localhost:5300/api/workspaces/{workspaceId}/search `
  -H "Content-Type: application/json" `
  -d '{"query": "main entry point", "topK": 3}'
```

You should see results with file paths, content snippets, and similarity scores.

## Troubleshooting First Run

| Symptom | Cause | Fix |
|---|---|---|
| `/health/db` returns 503 | PostgreSQL not running | `Start-Service AuraDB` (Windows) or `brew services start postgresql@17` (macOS) |
| `/health/rag` returns 503 | Ollama not running or model missing | Start Ollama; `ollama pull nomic-embed-text` |
| Port 5300 refused | AuraService not running | `Start-Service AuraService` or check logs |
| No search results | Workspace not indexed | Trigger indexing and wait for completion |

## Next Steps

- [Quick Start](quick-start.md) — register and search via Copilot MCP tools
- [Configuration](../configuration/settings.md) — tune RAG, embedding, and LLM settings

