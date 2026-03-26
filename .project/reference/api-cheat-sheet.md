# Aura API Cheat Sheet

**Base URL**: `http://localhost:5300`

## Health

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/health` | Overall status (`status`, `startedAt`, `deployTag`) |
| GET | `/health/db` | PostgreSQL connectivity |
| GET | `/health/rag` | RAG service status + chunk count |
| GET | `/health/mcp` | MCP server readiness + tool list |

## MCP

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/mcp` | JSON-RPC 2.0 — routes to `McpHandler.HandleAsync()` |

## Workspaces

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/workspaces` | List workspaces (`?limit=N`) |
| GET | `/api/workspaces/{idOrPath}` | Get workspace by ID or URL-encoded path |
| POST | `/api/workspaces` | Create workspace |
| DELETE | `/api/workspaces/{id}` | Remove workspace + RAG chunks + graph |

## Index (`/api/workspaces/{workspaceId}/index`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/workspaces/{workspaceId}/index/` | Index status (RAG + graph health, staleness) |
| POST | `/api/workspaces/{workspaceId}/index/` | Trigger reindex → `202 Accepted` + jobId |
| DELETE | `/api/workspaces/{workspaceId}/index/` | Clear RAG index (workspace preserved) |
| GET | `/api/workspaces/{workspaceId}/index/jobs` | List active jobs |
| GET | `/api/workspaces/{workspaceId}/index/jobs/{jobId:guid}` | Get specific job status |

## Search

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/workspaces/{workspaceId}/search` | Semantic search |

## Graph (`/api/workspaces/{workspaceId}/graph`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `.../graph/` | Graph stats (nodes, edges by type) |
| DELETE | `.../graph/` | Clear code graph + metadata |
| GET | `.../graph/symbols/{name}` | Find symbols (`?nodeType=`) |
| GET | `.../graph/implementations/{interfaceName}` | Find implementations |
| GET | `.../graph/callers/{methodName}` | Find callers (`?containingType=`) |
| GET | `.../graph/members/{typeName}` | Get type members |
| GET | `.../graph/namespaces/{namespaceName}` | List types in namespace |

---

## Request / Response Bodies

### Create Workspace

```json
{
  "path": "C:\\work\\my-repo",
  "alias": "my-repo",
  "tags": ["dotnet"]
}
```

### Search

```json
{
  "query": "search text",
  "topK": 5,
  "minScore": 0.7
}
```

Response:

```json
{
  "workspaceId": "...",
  "query": "...",
  "resultCount": 3,
  "results": [
    {
      "contentId": "...",
      "chunkIndex": 0,
      "text": "...",
      "score": 0.92,
      "sourcePath": "...",
      "contentType": "Code"
    }
  ]
}
```

---

## Quick Start

```powershell
# Health check
curl -s http://localhost:5300/health

# Create workspace
curl -s -X POST http://localhost:5300/api/workspaces `
  -H "Content-Type: application/json" `
  -d '{"path":"C:\\work\\my-repo"}'

# Trigger indexing
curl -s -X POST http://localhost:5300/api/workspaces/{id}/index/

# Search
curl -s -X POST http://localhost:5300/api/workspaces/{id}/search `
  -H "Content-Type: application/json" `
  -d '{"query":"authentication middleware","topK":5}'

# Find implementations
curl -s http://localhost:5300/api/workspaces/{id}/graph/implementations/IService
```
```
