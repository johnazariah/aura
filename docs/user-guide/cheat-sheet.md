# Cheat Sheet

Quick reference for Aura REST endpoints and MCP tool operations.

## Health Endpoints

| Method | Path | Returns |
|--------|------|---------|
| GET | `/health` | Status, startedAt, deployTag |
| GET | `/health/db` | Database connection check |
| GET | `/health/rag` | RAG health + chunk/document counts |
| GET | `/health/mcp` | MCP handler status + tool count |

## Workspace REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/workspaces` | List workspaces |
| GET | `/api/workspaces/{idOrPath}` | Get workspace by ID or URL-encoded path |
| POST | `/api/workspaces` | Create workspace |
| DELETE | `/api/workspaces/{id}` | Delete workspace + data |

## Index REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/workspaces/{id}/index` | Index status (fresh/stale/not-indexed) |
| POST | `/api/workspaces/{id}/index` | Trigger re-index (202 Accepted) |
| DELETE | `/api/workspaces/{id}/index` | Clear RAG index |
| GET | `/api/workspaces/{id}/index/jobs` | List indexing jobs |
| GET | `/api/workspaces/{id}/index/jobs/{jobId}` | Job status |

## Graph REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/workspaces/{id}/graph` | Graph stats (nodes/edges by type) |
| DELETE | `/api/workspaces/{id}/graph` | Clear graph |
| GET | `/api/workspaces/{id}/graph/implementations/{name}` | Find implementations |
| GET | `/api/workspaces/{id}/graph/callers/{name}` | Find callers |
| GET | `/api/workspaces/{id}/graph/members/{type}` | Type members |
| GET | `/api/workspaces/{id}/graph/namespaces/{ns}` | Types in namespace |
| GET | `/api/workspaces/{id}/graph/symbols/{name}` | Find symbols |

## Search REST API

| Method | Path | Body |
|--------|------|------|
| POST | `/api/workspaces/{id}/search` | `{ "query": "...", "topK": 5, "minScore": 0.3 }` |

## MCP Endpoint

| Method | Path | Description |
|--------|------|-------------|
| POST | `/mcp` | JSON-RPC (MCP protocol 2024-11-05) |

## MCP Tool Operations

### aura_search
No `operation` parameter — just `query`, `workspacePath`, `workspaces`, `limit`, `contentType`.

### aura_navigate
`callers` · `implementations` · `derived_types` · `usages` · `by_attribute` · `extension_methods` · `by_return_type` · `references` · `definition`

### aura_inspect
`type_members` · `list_types`

### aura_tree
`explore` · `get_node`

### aura_refactor
`rename` · `change_signature` · `extract_interface` · `extract_method` · `extract_variable` · `safe_delete` · `move_type_to_file` · `move_members_to_partial`

### aura_generate
`create_type` · `implement_interface` · `constructor` · `property` · `method` · `tests`

### aura_validate
`compilation` · `tests`

### aura_index
`index_directory` · `index_file` · `status` · `stats`

### aura_workspace
`list` · `add` · `remove` · `set_default` · `detect_worktree` · `invalidate_cache` · `status`

### aura_architect
`dependencies` · `layer_check` · `public_api` — *(coming soon)*

## Common Patterns

```bash
# Quick health check
curl http://localhost:5300/health

# Register + index a project
curl -X POST http://localhost:5300/api/workspaces \
  -H "Content-Type: application/json" \
  -d '{"path": "/projects/my-app", "startIndexing": true}'

# Search
curl -X POST http://localhost:5300/api/workspaces/{id}/search \
  -H "Content-Type: application/json" \
  -d '{"query": "error handling"}'
```

