# Aura REST API Reference

This document provides the definitive API reference for agents and tools to use when interacting with Aura.

## Base URL

```
http://localhost:5300
```

## Authentication

Most endpoints require no authentication. GitHub-related endpoints accept a `X-GitHub-Token` header for accessing private repositories.

---

## Workspace Management

Workspaces are the top-level resource representing indexed repositories/directories.

### List Workspaces

```http
GET /api/workspaces
```

**Query Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `limit` | int | Maximum number of workspaces to return |

**Response:**
```json
{
  "count": 2,
  "workspaces": [
    {
      "id": "a1b2c3d4e5f67890",
      "name": "aura",
      "path": "c:/work/aura",
      "status": "ready",
      "createdAt": "2026-01-15T10:00:00Z",
      "lastAccessedAt": "2026-01-30T14:30:00Z",
      "gitRemoteUrl": "https://github.com/user/repo",
      "defaultBranch": "main"
    }
  ]
}
```

### Get Workspace

```http
GET /api/workspaces/{idOrPath}
```

**Path Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `idOrPath` | string | 16-char hex workspace ID OR URL-encoded filesystem path |

**Response:**
```json
{
  "id": "a1b2c3d4e5f67890",
  "name": "aura",
  "path": "c:/work/aura",
  "status": "ready",
  "createdAt": "2026-01-15T10:00:00Z",
  "lastAccessedAt": "2026-01-30T14:30:00Z",
  "gitRemoteUrl": "https://github.com/user/repo",
  "defaultBranch": "main",
  "stats": {
    "files": 150,
    "chunks": 2500,
    "graphNodes": 1200,
    "graphEdges": 3500
  },
  "indexingJob": null
}
```

### Create Workspace

```http
POST /api/workspaces
```

**Request Body:**
```json
{
  "path": "c:/work/my-project",
  "name": "My Project",
  "startIndexing": true,
  "options": {
    "includePatterns": ["**/*.cs", "**/*.py"],
    "excludePatterns": ["**/bin/**", "**/obj/**"]
  }
}
```

**Response:**
```json
{
  "id": "a1b2c3d4e5f67890",
  "name": "My Project",
  "path": "c:/work/my-project",
  "status": "indexing",
  "isNew": true,
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Workspace created and indexing started"
}
```

### Delete Workspace

```http
DELETE /api/workspaces/{id}
```

Deletes the workspace and all associated data (RAG chunks, code graph, metadata).

**Response:**
```json
{
  "success": true,
  "id": "a1b2c3d4e5f67890",
  "path": "c:/work/my-project",
  "message": "Workspace deleted. Removed 2500 RAG chunks."
}
```

---

## Workspace Index

Per-workspace index management endpoints.

### Get Index Status

```http
GET /api/workspaces/{workspaceId}/index
```

Returns detailed index health and statistics.

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "workspacePath": "c:/work/aura",
  "status": "fresh",
  "isGitRepository": true,
  "currentCommitSha": "abc1234",
  "currentCommitAt": "2026-01-30T10:00:00Z",
  "rag": {
    "status": "fresh",
    "files": 150,
    "chunks": 2500,
    "indexedAt": "2026-01-30T09:00:00Z",
    "indexedCommitSha": "abc1234",
    "commitsBehind": 0
  },
  "graph": {
    "status": "fresh",
    "indexedAt": "2026-01-30T09:00:00Z",
    "indexedCommitSha": "abc1234",
    "commitsBehind": 0
  },
  "activeJob": null
}
```

**Status Values:**
- `fresh` - Index is up-to-date
- `stale` - Index is behind current commit
- `not-indexed` - No index exists

### Trigger Re-index

```http
POST /api/workspaces/{workspaceId}/index
```

Queues a re-indexing job for the workspace.

**Response (202 Accepted):**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "isNewJob": true,
  "message": "Re-indexing started"
}
```

### Clear Index

```http
DELETE /api/workspaces/{workspaceId}/index
```

Clears the RAG index for a workspace. Preserves the workspace record.

**Response:**
```json
{
  "success": true,
  "workspaceId": "a1b2c3d4e5f67890",
  "chunksRemoved": 2500,
  "message": "Index cleared. Workspace preserved."
}
```

### List Jobs

```http
GET /api/workspaces/{workspaceId}/index/jobs
```

Lists indexing jobs for this workspace.

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "count": 1,
  "jobs": [
    {
      "jobId": "550e8400-e29b-41d4-a716-446655440000",
      "state": "processing",
      "processedItems": 50,
      "totalItems": 150,
      "progressPercent": 33,
      "startedAt": "2026-01-30T14:00:00Z",
      "completedAt": null,
      "error": null
    }
  ]
}
```

### Get Job Status

```http
GET /api/workspaces/{workspaceId}/index/jobs/{jobId}
```

**Response:**
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "workspaceId": "a1b2c3d4e5f67890",
  "source": "c:/work/aura",
  "state": "completed",
  "totalItems": 150,
  "processedItems": 150,
  "failedItems": 0,
  "progressPercent": 100,
  "startedAt": "2026-01-30T14:00:00Z",
  "completedAt": "2026-01-30T14:05:00Z",
  "error": null
}
```

**Job States:**
- `queued` - Waiting to start
- `processing` - Currently indexing
- `completed` - Finished successfully
- `failed` - Finished with error

---

## Workspace Code Graph

Per-workspace code structure navigation.

### Get Graph Stats

```http
GET /api/workspaces/{workspaceId}/graph
```

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "totalNodes": 1200,
  "totalEdges": 3500,
  "nodesByType": {
    "Class": 150,
    "Method": 800,
    "Property": 200,
    "Interface": 50
  },
  "edgesByType": {
    "Calls": 2000,
    "Inherits": 100,
    "Implements": 50,
    "References": 1350
  }
}
```

### Find Implementations

```http
GET /api/workspaces/{workspaceId}/graph/implementations/{interfaceName}
```

Finds all types that implement the given interface.

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "interfaceName": "IRagService",
  "count": 2,
  "implementations": [
    {
      "name": "RagService",
      "fullName": "Aura.Foundation.Rag.RagService",
      "filePath": "c:/work/aura/src/Aura.Foundation/Rag/RagService.cs",
      "lineNumber": 15
    }
  ]
}
```

### Find Callers

```http
GET /api/workspaces/{workspaceId}/graph/callers/{methodName}
```

**Query Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `containingType` | string | Filter to callers from a specific type |

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "methodName": "QueryAsync",
  "containingType": null,
  "count": 5,
  "callers": [
    {
      "name": "ExecuteWithRag",
      "fullName": "Aura.Api.Endpoints.AgentEndpoints.ExecuteWithRag",
      "signature": "Task<IResult> ExecuteWithRag(...)",
      "filePath": "c:/work/aura/src/Aura.Api/Endpoints/AgentEndpoints.cs",
      "lineNumber": 85
    }
  ]
}
```

### Get Type Members

```http
GET /api/workspaces/{workspaceId}/graph/members/{typeName}
```

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "typeName": "RagService",
  "count": 8,
  "members": [
    {
      "name": "QueryAsync",
      "nodeType": "Method",
      "signature": "Task<IReadOnlyList<RagQueryResult>> QueryAsync(...)",
      "modifiers": "public async",
      "lineNumber": 45
    }
  ]
}
```

### Get Types in Namespace

```http
GET /api/workspaces/{workspaceId}/graph/namespaces/{namespaceName}
```

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "namespaceName": "Aura.Foundation.Rag",
  "count": 10,
  "types": [
    {
      "name": "RagService",
      "fullName": "Aura.Foundation.Rag.RagService",
      "nodeType": "Class",
      "filePath": "c:/work/aura/src/Aura.Foundation/Rag/RagService.cs",
      "lineNumber": 15
    }
  ]
}
```

### Find Symbols

```http
GET /api/workspaces/{workspaceId}/graph/symbols/{name}
```

Search for symbols by name.

**Query Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `nodeType` | string | Filter by type: Class, Method, Property, Interface, etc. |

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "query": "Query",
  "nodeType": null,
  "count": 12,
  "symbols": [
    {
      "name": "QueryAsync",
      "fullName": "Aura.Foundation.Rag.RagService.QueryAsync",
      "nodeType": "Method",
      "filePath": "c:/work/aura/src/Aura.Foundation/Rag/RagService.cs",
      "lineNumber": 45,
      "signature": "Task<IReadOnlyList<RagQueryResult>> QueryAsync(...)"
    }
  ]
}
```

### Clear Graph

```http
DELETE /api/workspaces/{workspaceId}/graph
```

Clears the code graph for a workspace.

**Response:**
```json
{
  "success": true,
  "workspaceId": "a1b2c3d4e5f67890",
  "message": "Code graph cleared. Workspace preserved."
}
```

---

## Workspace Search

Semantic search within a workspace.

### Search Workspace

```http
POST /api/workspaces/{workspaceId}/search
```

**Request Body:**
```json
{
  "query": "authentication middleware",
  "topK": 5,
  "minScore": 0.7
}
```

**Response:**
```json
{
  "workspaceId": "a1b2c3d4e5f67890",
  "query": "authentication middleware",
  "resultCount": 3,
  "results": [
    {
      "contentId": "c:/work/aura/src/middleware/auth.cs",
      "chunkIndex": 0,
      "text": "public class AuthenticationMiddleware...",
      "score": 0.92,
      "sourcePath": "c:/work/aura/src/middleware/auth.cs",
      "contentType": "CSharp"
    }
  ]
}
```

---

## Global Index Status

Global indexer queue status (not workspace-specific).

### Get Indexer Status

```http
GET /api/index/status
```

**Response:**
```json
{
  "queuedItems": 0,
  "processedItems": 1500,
  "failedItems": 2,
  "isProcessing": false,
  "activeJobs": 0
}
```

### Get Job Status (Global)

```http
GET /api/index/jobs/{jobId}
```

Get status of any job by ID.

---

## RAG Endpoints

Low-level RAG operations (for advanced use).

### Index Content

```http
POST /api/rag/index
```

Index a single content item directly.

**Request Body:**
```json
{
  "contentId": "manual-doc-1",
  "text": "Documentation content here...",
  "contentType": "PlainText",
  "sourcePath": "/docs/readme.md",
  "language": null
}
```

### Query RAG

```http
POST /api/rag/query
```

Query the RAG index directly (use workspace search for scoped queries).

**Request Body:**
```json
{
  "query": "how does authentication work",
  "topK": 5,
  "minScore": null,
  "sourcePathPrefix": null
}
```

### Get Global RAG Stats

```http
GET /api/rag/stats
```

**Response:**
```json
{
  "totalDocuments": 500,
  "totalChunks": 5000,
  "chunksByType": {
    "CSharp": 3000,
    "Python": 500,
    "Markdown": 1500
  }
}
```

### Remove Content

```http
DELETE /api/rag/{contentId}
```

Remove a specific content item by ID.

---

## Health Endpoints

### Basic Health

```http
GET /health
```

**Response:**
```json
{
  "status": "ok",
  "healthy": true
}
```

### Database Health

```http
GET /health/db
```

### RAG Health

```http
GET /health/rag
```

### LLM Health

```http
GET /health/ollama
```

### Agent Health

```http
GET /health/agents
```

### MCP Health

```http
GET /health/mcp
```

---

## Error Responses

All errors follow RFC 7807 Problem Details format:

```json
{
  "type": "https://aura.dev/problems/workspace-not-found",
  "title": "Workspace Not Found",
  "status": 404,
  "detail": "Workspace 'a1b2c3d4e5f67890' not found.",
  "instance": "/api/workspaces/a1b2c3d4e5f67890",
  "traceId": "00-abc123..."
}
```

**Common Problem Types:**
- `workspace-not-found` - Workspace doesn't exist
- `validation-failed` - Request validation error
- `internal-error` - Server error

---

## Path Handling

### Workspace IDs

Workspace IDs are 16-character hexadecimal strings derived from the normalized path hash. Example: `a1b2c3d4e5f67890`

### Path Normalization

Paths are normalized to:
- Forward slashes (`/`)
- Lowercase
- No trailing slash

Example: `C:\Work\Aura\` → `c:/work/aura`

### Using Paths in URLs

When using filesystem paths in URLs:
1. Normalize the path
2. URL-encode the result
3. Use as path parameter

Example:
```
Path: c:/work/aura
Encoded: c%3A%2Fwork%2Faura
URL: /api/workspaces/c%3A%2Fwork%2Faura
```
