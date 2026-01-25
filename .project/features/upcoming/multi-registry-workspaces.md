# Feature: Multi-Registry - Query Multiple Workspaces

**Status:** 📋 Ready for Development
**Priority:** Medium
**Type:** Feature
**Estimated Effort:** 6 hours

## Problem Statement

Developers often work with multiple related repositories:
- Microservices in separate repos
- Monorepo + external libraries
- Main project + shared packages

Currently Aura indexes one workspace at a time. Agents can only search/navigate within the active workspace. fs2 supports multi-graph queries.

## Design

### Concept: Workspace Registry

A registry of indexed workspaces that can be queried together:

```yaml
# ~/.config/aura/workspaces.yaml (or %APPDATA%\aura\workspaces.yaml)
workspaces:
  - id: aura
    path: c:\work\aura
    alias: "aura"
    tags: [dotnet, primary]
    
  - id: aura-extension
    path: c:\work\aura\extension
    alias: "ext"
    tags: [typescript, vscode]
    
  - id: shared-libs
    path: c:\work\shared-libs
    alias: "libs"
    tags: [dotnet, nuget]

default: aura
```

### MCP Tool Changes

#### Option 1: Workspace Parameter on Existing Tools

Add optional `workspaces` parameter to `aura_search`, `aura_tree`, etc.:

```json
{
  "name": "aura_search",
  "inputSchema": {
    "properties": {
      "query": { "type": "string" },
      "workspaces": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Workspace IDs to search. Default: current workspace only. Use ['*'] for all."
      }
    }
  }
}
```

**Response includes workspace in results:**
```json
{
  "results": [
    {
      "workspace": "aura",
      "path": "src/Services/OrderService.cs",
      "content": "...",
      "score": 0.92
    },
    {
      "workspace": "shared-libs",
      "path": "src/Common/Extensions.cs",
      "content": "...",
      "score": 0.87
    }
  ]
}
```

#### Option 2: Dedicated Multi-Search Tool

```json
{
  "name": "aura_multi_search",
  "description": "Search across multiple indexed workspaces.",
  "inputSchema": {
    "properties": {
      "query": { "type": "string" },
      "workspaces": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Workspace IDs or aliases. Use ['*'] for all registered workspaces."
      },
      "limit": { "type": "integer", "default": 20 }
    },
    "required": ["query"]
  }
}
```

**Recommendation:** Option 1 - extend existing tools. Less API surface, more natural.

### New Tool: `aura_workspaces`

Manage and query the workspace registry:

```json
{
  "name": "aura_workspaces",
  "description": "List, add, or remove workspaces from the multi-workspace registry.",
  "inputSchema": {
    "properties": {
      "operation": {
        "type": "string",
        "enum": ["list", "add", "remove", "set_default"],
        "default": "list"
      },
      "path": { "type": "string", "description": "Workspace path (for add)" },
      "id": { "type": "string", "description": "Workspace ID (for remove/set_default)" },
      "alias": { "type": "string", "description": "Short alias (for add)" },
      "tags": { "type": "array", "items": { "type": "string" } }
    }
  }
}
```

**List response:**
```json
{
  "workspaces": [
    {
      "id": "aura",
      "path": "c:\\work\\aura",
      "alias": "aura",
      "tags": ["dotnet", "primary"],
      "indexed": true,
      "chunkCount": 4521,
      "lastIndexed": "2026-01-24T10:30:00Z"
    }
  ],
  "default": "aura"
}
```

### Implementation

#### 1. Workspace Registry Service

```csharp
public interface IWorkspaceRegistryService
{
    IReadOnlyList<WorkspaceInfo> ListWorkspaces();
    WorkspaceInfo? GetWorkspace(string idOrAlias);
    WorkspaceInfo GetDefaultWorkspace();
    void AddWorkspace(string path, string? alias = null, IReadOnlyList<string>? tags = null);
    void RemoveWorkspace(string id);
    void SetDefault(string id);
}

public record WorkspaceInfo(
    string Id,
    string Path,
    string? Alias,
    IReadOnlyList<string> Tags,
    bool Indexed,
    int ChunkCount,
    DateTimeOffset? LastIndexed);
```

#### 2. Storage Location

- **Windows:** `%APPDATA%\aura\workspaces.yaml`
- **macOS/Linux:** `~/.config/aura/workspaces.yaml`

#### 3. Multi-Workspace RAG Queries

Extend `IRagService`:

```csharp
/// <summary>
/// Search across multiple workspaces.
/// </summary>
Task<IReadOnlyList<MultiWorkspaceResult>> SearchMultiAsync(
    string query,
    IReadOnlyList<string> workspaceIds,
    int limit = 20,
    CancellationToken ct = default);
```

Implementation:
1. For each workspace ID, resolve to path
2. Query each workspace's RAG index in parallel
3. Merge results, sort by score
4. Include workspace ID in each result

#### 4. Database Considerations

Currently RAG chunks are stored with `SourcePath`. For multi-workspace:

**Option A: Single database, workspace column**
```sql
ALTER TABLE rag_chunks ADD COLUMN workspace_id TEXT;
CREATE INDEX idx_chunks_workspace ON rag_chunks(workspace_id);
```

**Option B: Separate database per workspace**
- Each workspace gets its own `.aura/aura.db` or connection
- Multi-query fans out to multiple DBs

**Recommendation:** Option A for simplicity. One database, partition by workspace.

#### 5. MCP Handler Updates

```csharp
// Extend SearchAsync
private async Task<object> SearchAsync(JsonElement? args, CancellationToken ct)
{
    var query = args?.GetProperty("query").GetString();
    var workspaces = args?.TryGetProperty("workspaces", out var w) == true
        ? w.EnumerateArray().Select(x => x.GetString()!).ToList()
        : null;

    if (workspaces is null || workspaces.Count == 0)
    {
        // Single workspace (current behavior)
        return await _ragService.SearchAsync(query, ct);
    }
    
    if (workspaces.Contains("*"))
    {
        workspaces = _workspaceRegistry.ListWorkspaces()
            .Select(w => w.Id).ToList();
    }

    return await _ragService.SearchMultiAsync(query, workspaces, ct);
}
```

## Workflow Examples

### Setup
```
Agent: aura_workspaces(operation="add", path="c:\\work\\shared-libs", alias="libs")
Agent: aura_workspaces(operation="list")
```

### Cross-Repo Search
```
Agent: aura_search(query="authentication middleware", workspaces=["aura", "libs"])
→ Results from both repos, ranked by relevance
```

### Explore Related Code
```
Agent: aura_tree(pattern="AuthService", workspaces=["*"])
→ AuthService implementations across all registered workspaces
```

## Files to Create/Change

| File | Change |
|------|--------|
| `src/Aura.Foundation/Services/IWorkspaceRegistryService.cs` | New interface |
| `src/Aura.Foundation/Services/WorkspaceRegistryService.cs` | New implementation |
| `src/Aura.Foundation/Data/Entities/RagChunk.cs` | Add WorkspaceId column |
| `src/Aura.Foundation/Rag/IRagService.cs` | Add SearchMultiAsync |
| `src/Aura.Foundation/Rag/RagService.cs` | Implement multi-search |
| `src/Aura.Api/Mcp/McpHandler.cs` | Add aura_workspaces, extend existing tools |
| EF Migration | Add workspace_id column |

## Acceptance Criteria

- [ ] Workspace registry persists across sessions
- [ ] `aura_workspaces list` shows all registered workspaces
- [ ] `aura_workspaces add` registers new workspace
- [ ] `aura_search` with `workspaces` parameter searches multiple
- [ ] `workspaces=["*"]` searches all registered workspaces
- [ ] Results include workspace ID for disambiguation
- [ ] Performance: parallel queries, < 500ms for 3 workspaces

## Migration Path

1. **Phase 1:** Add WorkspaceId to RagChunk, default to current behavior
2. **Phase 2:** Add workspace registry service
3. **Phase 3:** Add `aura_workspaces` tool
4. **Phase 4:** Extend search/tree tools with workspaces parameter

## Edge Cases

- **Workspace not indexed:** Return error with suggestion to run index
- **Workspace path moved:** Detect and prompt for update
- **Duplicate aliases:** Reject with error
- **Same file in multiple workspaces:** Show both, let agent choose

## Future Enhancements

- Auto-discover related workspaces (git submodules, package references)
- Workspace groups (e.g., "microservices" = [svc-a, svc-b, svc-c])
- Cross-workspace navigation (find usages across repos)
- Federated index (query remote Aura instances)
