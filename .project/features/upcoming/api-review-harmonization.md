# API Review and Harmonization

**Status:** 📋 Planned  
**Priority:** Medium  
**Created:** 2025-01-09

## Problem Statement

The API has grown organically and now has overlapping/inconsistent endpoints:

### Current Duplication Examples

**Clearing data:**

- `DELETE /api/rag` - Clears entire RAG index
- `DELETE /api/workspace?path=` - Clears workspace-specific chunks, graph, metadata
- `DELETE /api/code-graph/{repositoryPath}` - Clears code graph only

**Indexing:**

- `POST /api/rag/index` - Index a directory (sync)
- `POST /api/rag/index/background` - Index a directory (async)
- `POST /api/workspace/onboard` - Onboard workspace (starts indexing)

**Stats:**

- `GET /api/rag/stats` - RAG statistics
- `GET /api/code-graph/{repositoryPath}/stats` - Graph statistics
- `GET /api/workspace/status?path=` - Combined workspace status

### Issues

1. **Inconsistent path handling** - Some use query params, some use route params, some use URL encoding
2. **Overlapping functionality** - Multiple ways to do the same thing
3. **Inconsistent naming** - Mix of `rag`, `workspace`, `code-graph` for related concepts
4. **No clear resource hierarchy** - Workspace → Index → Chunks/Graph relationship not reflected

## Architecture Observations (2025-01-10)

### Current Indexing Architecture

We have **two separate indexing systems** with different purposes:

#### 1. RAG Index (Embeddings for Semantic Search)

- **Purpose**: Semantic search - "find code related to authentication"
- **Storage**: Text chunks + vector embeddings in pgvector
- **Selection**: Dynamic via `IAgentRegistry.GetBestForCapability("ingest:{extension}")`
- **Ingesters available**:
  - `CSharpIngesterAgent` - Uses Roslyn for C# semantic parsing
  - `TreeSitterIngesterAgent` - Uses Tree-sitter for 30+ languages (Python, Rust, Go, etc.)
  - `TextIngesterAgent` - Simple text chunking for Markdown, plain text
  - `FallbackIngesterAgent` - Line-based chunking when nothing else matches
- **Result**: Chunks stored in `rag_chunks` table with embeddings

#### 2. Code Graph (Symbols for Navigation)

- **Purpose**: Structural navigation - "find all usages of UserService"
- **Storage**: Nodes (classes, methods) + edges (calls, inherits) in `code_nodes`/`code_edges`
- **Implementation**: `CodeGraphIndexer` - **Roslyn-only, C# only**
- **No dynamic selection**: Only works for .NET solutions/projects
- **Result**: Symbol graph with relationships

### The Problem

These are **two completely separate indexing passes**:

1. RAG indexing calls the right ingester for each file type → embeddings
2. Code graph indexing only works for C# → symbol graph

For non-C# languages (Python, Rust, etc.):

- ✅ RAG: Tree-sitter ingester extracts semantic chunks correctly
- ❌ Code Graph: No symbol extraction (Roslyn doesn't understand them)

### Proposed Unification

The `TreeSitterIngesterAgent` already extracts AST nodes (functions, classes, etc.) for chunking. This same information could populate the code graph for non-C# languages.

**Option A: Extract graph data during RAG indexing**

- When Tree-sitter parses a file, also emit symbol definitions
- Store in code graph alongside Roslyn-extracted C# symbols
- Single pass, unified data

**Option B: Separate Tree-sitter graph builder**

- Create `TreeSitterCodeGraphIndexer` parallel to `CodeGraphIndexer`
- Build graph for non-C# files
- More duplication but clearer separation

**Option C: Keep them separate, accept the limitation**

- Code graph is C#-only feature for now
- Document this clearly
- Other languages get RAG search only

## File Discovery for New Projects

When indexing a workspace, we need to handle projects at various stages:

| Scenario | Current Behavior | Desired Behavior |
|----------|------------------|------------------|
| Git repo with commits | ✅ `git ls-files` | Same |
| Git repo, nothing committed | ❌ Empty list or fallback to glob | `git ls-files --others --exclude-standard` |
| No git, has `.gitignore` | ❌ Ignores `.gitignore` | Honor `.gitignore` patterns |
| No git, no `.gitignore` | ❌ Indexes everything | Built-in excludes (node_modules, bin, obj, .venv, etc.) |

**Proposed `DiscoverFilesAsync` logic:**

```csharp
if (await gitService.HasCommitsAsync(path))
    return await gitService.GetTrackedFilesAsync(path);  // committed files
    
if (await gitService.IsRepositoryAsync(path))
    return await gitService.GetUntrackedNonIgnoredFilesAsync(path);  // git ls-files --others --exclude-standard

if (File.Exists(Path.Combine(path, ".gitignore")))
    return GlobWithGitignore(path);  // respect .gitignore even without git

return GlobWithDefaultExcludes(path);  // sensible defaults
```

**Default excludes:**

- `**/node_modules/**`
- `**/bin/**`, `**/obj/**`
- `**/.venv/**`, `**/venv/**`, `**/__pycache__/**`
- `**/target/**` (Rust)
- `**/.git/**`
- `**/dist/**`, `**/build/**`

## Proposed Solution

### Resource-Oriented API Design

```text
/api/workspaces
  GET    /                     - List all workspaces
  POST   /                     - Onboard a workspace
  GET    /{id}                 - Get workspace details + status
  DELETE /{id}                 - Remove workspace (clears all data)
  
/api/workspaces/{id}/index
  GET    /                     - Get index status/stats
  POST   /                     - Trigger re-index
  DELETE /                     - Clear index only (keep workspace)
  GET    /jobs                 - List indexing jobs
  GET    /jobs/{jobId}         - Get job progress

/api/workspaces/{id}/graph
  GET    /                     - Get graph stats
  GET    /symbols              - Query symbols
  DELETE /                     - Clear graph only

/api/workspaces/{id}/search
  POST   /                     - RAG search within workspace
```

### Path Normalization

- Use workspace ID (hash of normalized path) instead of encoding paths in URLs
- Store canonical path in workspace record
- Consistent normalization on input
- **Note**: `PathNormalizer` already exists in `Aura.Foundation.Rag` - use it consistently!

### Backwards Compatibility

- Keep old endpoints during transition with deprecation warnings
- Add `X-Deprecated` header pointing to new endpoint
- Remove old endpoints in next major version

## Tasks

1. [ ] Audit all existing endpoints and document current behavior
2. [ ] Design new resource-oriented API schema
3. [ ] Implement workspace ID concept
4. [ ] Create new endpoints alongside old ones
5. [ ] Add deprecation warnings to old endpoints
6. [ ] Update extension to use new endpoints
7. [ ] Update documentation
8. [ ] Remove deprecated endpoints (major version bump)

## Success Criteria

- [ ] No duplicate functionality in API
- [ ] Consistent path handling across all endpoints
- [ ] Clear resource hierarchy
- [ ] OpenAPI spec validates cleanly
- [ ] Extension works with new endpoints

## Notes

- Consider generating OpenAPI spec and using it for validation
- May want to version the API (`/api/v2/workspaces`)
