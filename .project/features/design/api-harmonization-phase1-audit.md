# API Harmonization Phase 1: Audit & Design

**Status:** 📐 In Design  
**Date:** 2026-01-10  
**Spec:** [api-review-harmonization.md](../upcoming/api-review-harmonization.md)

## 1. Current Endpoint Audit

### Health Endpoints (5)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/health` | Basic health check | ✅ Keep |
| GET | `/health/db` | Database connectivity | ✅ Keep |
| GET | `/health/rag` | RAG service status | ✅ Keep |
| GET | `/health/ollama` | LLM provider status | ✅ Keep |
| GET | `/health/agents` | Critical agent availability | ✅ Keep |

### Agent Endpoints (4)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/api/agents` | List agents (optional capability filter) | ✅ Keep |
| GET | `/api/agents/best` | Get best agent for capability | ✅ Keep |
| GET | `/api/agents/{agentId}` | Get agent details | ✅ Keep |
| POST | `/api/agents/{agentId}/execute` | Execute agent | ✅ Keep |
| POST | `/api/agents/{agentId}/execute/rag` | Execute agent with RAG context | ✅ Keep |

### Conversation Endpoints (4)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/api/conversations` | List conversations | ✅ Keep |
| GET | `/api/conversations/{id}` | Get conversation with messages | ✅ Keep |
| POST | `/api/conversations` | Create conversation | ✅ Keep |
| POST | `/api/conversations/{id}/messages` | Add message (triggers agent) | ✅ Keep |

### Execution Endpoints (1)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/api/executions` | List agent executions | ✅ Keep |

### RAG Endpoints (6) - **NEEDS HARMONIZATION**

| Method | Path | Params | Purpose | Issue |
|--------|------|--------|---------|-------|
| POST | `/api/rag/index` | body: text, contentId | Index single content | Sync, blocks |
| POST | `/api/rag/index/directory` | query: `directoryPath` | Index directory (legacy sync) | **Duplicate of background** |
| POST | `/api/rag/query` | body: query, topK | Semantic search | Should be workspace-scoped |
| GET | `/api/rag/stats` | - | Global RAG stats | No workspace filter |
| GET | `/api/rag/stats/directory` | query: `path` | Directory RAG stats | Inconsistent param name |
| DELETE | `/api/rag/{contentId}` | route: contentId | Delete single content | OK |
| DELETE | `/api/rag` | - | Clear ALL RAG data | **Dangerous, no scope** |

### Code Graph Endpoints (6) - **NEEDS HARMONIZATION**

| Method | Path | Params | Purpose | Issue |
|--------|------|--------|---------|-------|
| GET | `/api/graph/stats` | query: `repositoryPath` | Graph stats | Query param, not route |
| POST | `/api/graph/index` | body: projectPath | Index .NET solution/project | C# only, sync |
| GET | `/api/graph/implementations/{interfaceName}` | route + query: `repositoryPath` | Find implementations | Mixed param styles |
| GET | `/api/graph/callers/{methodName}` | route + query: `repositoryPath` | Find callers | Mixed param styles |
| GET | `/api/graph/members/{typeName}` | route + query: `repositoryPath` | Get type members | Mixed param styles |
| GET | `/api/graph/namespace/{namespaceName}` | route + query: `repositoryPath` | Types in namespace | Mixed param styles |
| GET | `/api/graph/find/{name}` | route + query: `repositoryPath` | Find by name | Mixed param styles |
| DELETE | `/api/graph/{repositoryPath}` | route: repositoryPath (encoded) | Clear graph for repo | **URL encoding issues** |

### Semantic/Combined Index Endpoints (4) - **NEEDS HARMONIZATION**

| Method | Path | Params | Purpose | Issue |
|--------|------|--------|---------|-------|
| POST | `/api/semantic/index` | body: { repositoryPath } | Combined RAG + Graph indexing | Sync |
| POST | `/api/index/background` | body: { directoryPath } | Background RAG indexing | Good async pattern |
| GET | `/api/index/status` | - | Active jobs status | OK |
| GET | `/api/index/health` | - | Index service health | OK |
| GET | `/api/index/jobs/{jobId}` | route: jobId | Job progress | OK |

### Workspace Endpoints (3) - **NEEDS HARMONIZATION**

| Method | Path | Params | Purpose | Issue |
|--------|------|--------|---------|-------|
| GET | `/api/workspace/status` | query: `path` | Workspace index status | No workspace ID |
| POST | `/api/workspace/onboard` | body: { path } | Start onboarding | **Duplicate of index/background** |
| DELETE | `/api/workspace` | query: `path` | Clear workspace data | **Inconsistent with others** |

### Tool Endpoints (3)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/api/tools` | List available tools | ✅ Keep |
| POST | `/api/tools/{toolId}/execute` | Execute tool | ✅ Keep |
| POST | `/api/tools/react` | ReAct agent execution | ✅ Keep |

### Git Endpoints (5)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| GET | `/api/git/status` | Git repo status | ✅ Keep |
| POST | `/api/git/branch` | Create branch | ✅ Keep |
| POST | `/api/git/commit` | Commit changes | ✅ Keep |
| GET | `/api/git/worktrees` | List worktrees | ✅ Keep |
| POST | `/api/git/worktrees` | Create worktree | ✅ Keep |
| DELETE | `/api/git/worktrees` | Remove worktree | ✅ Keep |

### Developer Workflow Endpoints (16)

| Method | Path | Purpose | Keep/Deprecate |
|--------|------|---------|----------------|
| POST | `/api/developer/workflows` | Create workflow | ✅ Keep |
| GET | `/api/developer/workflows` | List workflows | ✅ Keep |
| GET | `/api/developer/workflows/{id}` | Get workflow with steps | ✅ Keep |
| DELETE | `/api/developer/workflows/{id}` | Delete workflow | ✅ Keep |
| POST | `/api/developer/workflows/{id}/analyze` | Enrich with RAG | ✅ Keep |
| POST | `/api/developer/workflows/{id}/plan` | Generate steps | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps` | Add step | ✅ Keep |
| DELETE | `/api/developer/workflows/{id}/steps/{stepId}` | Remove step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/execute` | Execute step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/approve` | Approve step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/reject` | Reject step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/skip` | Skip step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/reset` | Reset step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/chat` | Chat about step | ✅ Keep |
| POST | `/api/developer/workflows/{id}/steps/{stepId}/reassign` | Reassign agent | ✅ Keep |
| PUT | `/api/developer/workflows/{id}/steps/{stepId}/description` | Update description | ✅ Keep |
| POST | `/api/developer/workflows/{id}/complete` | Mark complete | ✅ Keep |
| POST | `/api/developer/workflows/{id}/cancel` | Cancel workflow | ✅ Keep |
| POST | `/api/developer/workflows/{id}/finalize` | Finalize workflow | ✅ Keep |
| POST | `/api/developer/workflows/{id}/chat` | Chat about workflow | ✅ Keep |

---

## 2. Identified Issues

### Issue 1: Multiple Ways to Index

| Current Endpoint | Behavior | Target Action |
|------------------|----------|---------------|
| `POST /api/rag/index` | Sync, single content | Keep for programmatic use |
| `POST /api/rag/index/directory` | Sync, directory | ⚠️ Deprecate → use background |
| `POST /api/index/background` | Async, directory | ✅ Preferred |
| `POST /api/workspace/onboard` | Async, calls background | ⚠️ Deprecate → use new workspaces API |
| `POST /api/semantic/index` | Sync, RAG + Graph | ⚠️ Deprecate → integrate into background |

**Proposed:** Unify to `/api/workspaces/{id}/index` for workspace-level indexing.

### Issue 2: Multiple Ways to Clear Data

| Current Endpoint | Scope | Target Action |
|------------------|-------|---------------|
| `DELETE /api/rag` | All RAG data globally | ⚠️ Deprecate (too dangerous) |
| `DELETE /api/rag/{contentId}` | Single content | ✅ Keep |
| `DELETE /api/workspace?path=` | Workspace RAG + Graph + Metadata | ⚠️ Deprecate → use workspaces API |
| `DELETE /api/graph/{repositoryPath}` | Graph for repo | ⚠️ Deprecate → use workspaces API |

**Proposed:** Unify to `/api/workspaces/{id}` for full clear, `/api/workspaces/{id}/index` for index only.

### Issue 3: Multiple Ways to Get Stats

| Current Endpoint | Scope | Target Action |
|------------------|-------|---------------|
| `GET /api/rag/stats` | Global | ✅ Keep (admin use) |
| `GET /api/rag/stats/directory?path=` | Directory | ⚠️ Deprecate → use workspaces API |
| `GET /api/graph/stats?repositoryPath=` | Repository | ⚠️ Deprecate → use workspaces API |
| `GET /api/workspace/status?path=` | Workspace combined | ⚠️ Deprecate → use workspaces API |

**Proposed:** Unify to `GET /api/workspaces/{id}` for workspace status.

### Issue 4: Inconsistent Path Handling

| Pattern | Examples | Issue |
|---------|----------|-------|
| Query param | `?path=`, `?directoryPath=`, `?repositoryPath=` | Inconsistent naming |
| Route param | `/api/graph/{repositoryPath}` | URL encoding problems |
| Body | `{ "path": ... }`, `{ "repositoryPath": ... }` | Inconsistent naming |

**Proposed:** Use workspace ID (hash) in routes, store canonical path in DB.

---

## 3. Proposed Workspace Entity Schema

### Entity Design

```csharp
/// <summary>
/// Represents a workspace (directory/repository) that has been onboarded to Aura.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Unique identifier - SHA256 hash of the normalized path.
    /// Using a deterministic ID means the same path always maps to the same workspace.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// The canonical (normalized) path of the workspace.
    /// Always lowercase with forward slashes via PathNormalizer.
    /// </summary>
    public required string CanonicalPath { get; init; }
    
    /// <summary>
    /// Display name (usually directory name).
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// When the workspace was first onboarded.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// When the workspace was last accessed or indexed.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Current onboarding/indexing status.
    /// </summary>
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Pending;
    
    /// <summary>
    /// If Status is Error, contains the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Optional: Git remote URL if this is a git repository.
    /// </summary>
    public string? GitRemoteUrl { get; set; }
    
    /// <summary>
    /// Optional: Default branch name.
    /// </summary>
    public string? DefaultBranch { get; set; }
}

/// <summary>
/// Workspace lifecycle status.
/// </summary>
public enum WorkspaceStatus
{
    /// <summary>Registered but not yet indexed.</summary>
    Pending = 0,
    
    /// <summary>Currently being indexed.</summary>
    Indexing = 1,
    
    /// <summary>Successfully indexed and ready.</summary>
    Ready = 2,
    
    /// <summary>Indexing failed.</summary>
    Error = 3,
    
    /// <summary>Index is stale (commits since last index).</summary>
    Stale = 4
}
```

### ID Generation Strategy

**Decision: Use SHA256 hash of normalized path (first 16 chars = 64 bits)**

```csharp
public static class WorkspaceIdGenerator
{
    public static string GenerateId(string path)
    {
        var normalized = PathNormalizer.Normalize(path);
        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        // Use first 16 hex chars (64 bits) - collision probability negligible
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
```

**Rationale:**

- Deterministic: Same path always → same ID (idempotent onboarding)
- URL-safe: Hex characters only, no encoding issues
- Short: 16 chars is readable in URLs
- Case-insensitive: Works on Windows and Unix

**Example:**

```
c:\work\aura → c:/work/aura → 7a8b3f2e1d0c4a9b (hash)
C:\Work\AURA → c:/work/aura → 7a8b3f2e1d0c4a9b (same hash!)
```

### Database Table

```sql
CREATE TABLE workspaces (
    id VARCHAR(16) PRIMARY KEY,
    canonical_path VARCHAR(1024) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    error_message TEXT,
    git_remote_url VARCHAR(2048),
    default_branch VARCHAR(255)
);

CREATE INDEX idx_workspaces_status ON workspaces(status);
CREATE INDEX idx_workspaces_last_accessed ON workspaces(last_accessed_at);
```

### Relationship to Existing Tables

| Table | Current Key | New Relationship |
|-------|-------------|------------------|
| `rag_chunks` | `content_id` (contains source_path) | Add `workspace_id` FK |
| `code_nodes` | `repository_path` | Add `workspace_id` FK |
| `code_edges` | via code_nodes | Inherited |
| `index_metadata` | `workspace_path` | Rename to `workspace_id` FK |
| `workflows` (Developer) | `repository_path` | Add `workspace_id` FK |

**Migration Strategy:**

1. Add `workspace_id` column as nullable
2. Create workspaces from existing unique paths
3. Backfill `workspace_id`
4. Make `workspace_id` NOT NULL
5. Remove/deprecate path columns (or keep for display)

---

## 4. Proposed New API Structure

### Workspaces Resource

```
/api/workspaces
  GET    /                     - List all workspaces
  POST   /                     - Onboard a workspace (body: { path })
  GET    /{id}                 - Get workspace details + status
  DELETE /{id}                 - Remove workspace (clears all data)
  
/api/workspaces/{id}/index
  GET    /                     - Get index status/stats
  POST   /                     - Trigger re-index
  DELETE /                     - Clear index only
  GET    /jobs                 - List indexing jobs for workspace
  GET    /jobs/{jobId}         - Get job progress

/api/workspaces/{id}/graph
  GET    /                     - Get graph stats
  DELETE /                     - Clear graph only

/api/workspaces/{id}/search
  POST   /                     - RAG search within workspace
```

### Lookup Helper Endpoint

```
GET /api/workspaces/lookup?path={path}
```

Returns the workspace ID for a given path (creates if not exists, or 404).

---

## 5. Implementation Plan

### Phase 2: Implement New Endpoints

1. [x] Create `Workspace` entity and EF configuration
2. [x] Create migration
3. [x] Implement `WorkspaceIdGenerator`
4. [x] Add `GET/POST /api/workspaces` endpoints
5. [x] Add `GET /api/workspaces/{id}` endpoint
6. [x] Add `DELETE /api/workspaces/{id}` endpoint
7. [x] Add `POST /api/workspaces/{id}/reindex` endpoint
8. [x] Add `GET /api/workspaces/lookup` endpoint
9. [x] Add `indexingJob` to workspace responses with live progress

### Phase 3: Migration & Deprecation

1. [x] Update extension to use new endpoints
2. [x] Remove deprecated path-based endpoints
3. [x] Update documentation

### Phase 4: API Response Consistency

**Goal:** All workspace-related endpoints return consistent response shapes.

| Endpoint | Response Fields | Status |
|----------|-----------------|--------|
| `GET /api/workspaces` | `{count, workspaces: [...]}` | ✅ |
| `GET /api/workspaces/{id}` | Full workspace with stats + indexingJob | ✅ |
| `GET /api/workspaces/lookup` | Same fields as `GET /{id}` | ⏳ TODO |
| `POST /api/workspaces` | Same as `GET /{id}` + `{isNew, jobId, message}` | ⏳ TODO |
| `POST /api/workspaces/{id}/reindex` | `{jobId, message}` | ✅ |
| `DELETE /api/workspaces/{id}` | `{success, message, deletedChunks}` | ✅ |

**Consistency rules:**
1. All single-workspace responses include full stats when available
2. All indexing-capable endpoints include `indexingJob` when active
3. Use consistent field naming (camelCase throughout)
4. Error responses: `{error: string, details?: object}`

Tasks:
1. [ ] Unify `/api/workspaces/lookup` response to match `/api/workspaces/{id}`
2. [ ] Unify `POST /api/workspaces` response to include full stats
3. [ ] Add OpenAPI schema validation for response contracts
4. [ ] Add integration tests for response shape consistency

### Phase 5: Cleanup

1. [ ] Remove any remaining redundant path columns
2. [ ] Bump API version if breaking changes needed

---

## 6. Questions Resolved

| Question | Decision | Rationale |
|----------|----------|-----------|
| Workspace ID: hash vs UUID? | **Hash (SHA256, 16 chars)** | Deterministic, same path = same ID |
| API versioning? | **Inline deprecation first** | Less disruptive, version later if needed |
| Handle existing paths? | **Migration script** | Backfill workspace_id from existing paths |

---

## Next Steps

1. ✅ Complete this audit document
2. ⏳ Get approval on design before implementing
3. ⏳ Create Workspace entity and migration
4. ⏳ Implement new endpoints
