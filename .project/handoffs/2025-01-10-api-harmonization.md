# Agent Handoff: API Harmonization

**Date:** 2025-01-10  
**Previous Session:** Workspace onboarding implementation + investigation  
**Next Task:** Implement API harmonization from `.project/features/upcoming/api-review-harmonization.md`

---

## What Was Done

### Commits (chronological)
1. `d587eaa` - Workspace onboarding spec + git safe.directory auto-fix
2. `1163081` - Added API review and path normalization stories  
3. `64919c2` - Added workspace status/onboarding endpoints to API
4. `76a4b6f` - Added extension welcome view with onboarding flow

### Working Features
- **Workspace onboarding API endpoints** (in `Program.cs`):
  - `GET /api/workspace/status?path=` - Check if workspace is indexed
  - `POST /api/workspace/onboard` - Start indexing
  - `DELETE /api/workspace?path=` - Clear all workspace data
- **Extension welcome view** - Shows before onboarding, hides after
- **RAG indexing** - Working end-to-end (391 files, 6346 chunks for Aura repo)
- **Code graph** - Working for C# only (2584 nodes, 2258 edges)

### Known Issues (documented but not fixed)
1. **Path case sensitivity** - `c:\work\aura` vs `C:\work\aura` causes mismatches
   - Quick fix applied in workspace status endpoint
   - Needs proper fix via `PathNormalizer` everywhere
   - Story: `.project/features/upcoming/path-normalization-review.md`

2. **Workspace status inconsistency** - Status bar shows 89% but panel shows "Not Indexed"
   - Root cause: `IndexMetadata` table has orphaned/mismatched paths
   - The workspace status endpoint checks `IndexMetadata.SourceDirectory` but paths don't match

3. **Code graph not auto-triggered** - Onboard only starts RAG indexing
   - Code graph needs a `.sln` or `.csproj` path, not just a directory
   - TODO in code to detect solution file

---

## The Task: API Harmonization

**Spec Location:** [.project/features/upcoming/api-review-harmonization.md](.project/features/upcoming/api-review-harmonization.md)

### Problem Summary

The API grew organically with duplicate/inconsistent endpoints:

| Category | Endpoints | Issue |
|----------|-----------|-------|
| Clearing | `DELETE /api/rag`, `DELETE /api/workspace`, `DELETE /api/code-graph/{path}` | 3 ways to clear, inconsistent params |
| Indexing | `POST /api/rag/index`, `POST /api/rag/index/background`, `POST /api/workspace/onboard` | 3 ways to index |
| Stats | `GET /api/rag/stats`, `GET /api/code-graph/{path}/stats`, `GET /api/workspace/status` | 3 stats endpoints |

### Proposed New API Structure

```text
/api/workspaces
  GET    /                     - List all workspaces
  POST   /                     - Onboard a workspace
  GET    /{id}                 - Get workspace details + status
  DELETE /{id}                 - Remove workspace (clears all data)
  
/api/workspaces/{id}/index
  GET    /                     - Get index status/stats
  POST   /                     - Trigger re-index
  DELETE /                     - Clear index only
  
/api/workspaces/{id}/graph
  GET    /                     - Get graph stats
  DELETE /                     - Clear graph only

/api/workspaces/{id}/search
  POST   /                     - RAG search within workspace
```

### Key Design Decisions

1. **Workspace ID** - Hash of normalized path instead of encoding path in URLs
2. **Unified Workspace entity** - Single source of truth for workspace state
3. **Backwards compatibility** - Keep old endpoints with `X-Deprecated` header during transition

---

## Architecture Context

### Two Separate Indexing Systems

| System | Purpose | Technology | Languages |
|--------|---------|------------|-----------|
| **RAG Index** | Semantic search ("find auth code") | pgvector embeddings | All (via Tree-sitter) |
| **Code Graph** | Symbol navigation ("usages of X") | Roslyn | C# only |

The RAG ingester selection is dynamic:
```csharp
var ingester = agentRegistry.GetBestForCapability($"ingest:{extension}");
// Returns: CSharpIngesterAgent, TreeSitterIngesterAgent, TextIngesterAgent, etc.
```

### File Discovery

Currently uses `git ls-files` which fails for:
- New repos with no commits
- Non-git directories

Proposed fallback chain in spec (not yet implemented).

---

## Key Files to Modify

| File | Purpose |
|------|---------|
| `src/Aura.Api/Program.cs` | All API endpoints (single file) |
| `src/Aura.Foundation/Data/AuraDbContext.cs` | Add `Workspace` entity if needed |
| `src/Aura.Foundation/Rag/PathNormalizer.cs` | Use this consistently |
| `extension/src/services/auraApiService.ts` | Update to use new endpoints |

---

## Suggested Approach

### Phase 1: Audit & Design
1. List all current endpoints with their parameters and behavior
2. Design the Workspace entity schema
3. Decide on workspace ID generation (path hash vs GUID)

### Phase 2: Implement New Endpoints
1. Create `Workspace` entity and migrations
2. Add new `/api/workspaces` endpoints alongside existing
3. Use `PathNormalizer` consistently

### Phase 3: Migration
1. Add `X-Deprecated` headers to old endpoints
2. Update extension to use new endpoints
3. Test thoroughly

### Phase 4: Cleanup
1. Remove deprecated endpoints
2. Update documentation

---

## Testing Commands

```powershell
# Check API health
curl http://localhost:5300/health

# Current workspace status (old endpoint)
curl "http://localhost:5300/api/workspace/status?path=C:/work/aura"

# RAG stats
curl http://localhost:5300/api/rag/stats

# Start indexing (background)
curl -X POST "http://localhost:5300/api/rag/index/background?directoryPath=C:/work/aura"
```

---

## Important Constraints

1. **Never start/stop the API server** - User runs `Start-Api` manually
2. **Build extension after changes**: `.\scripts\Build-Extension.ps1`
3. **Follow conventional commits**: `feat(api):`, `fix(api):`, `refactor(api):`
4. **Update STATUS.md** after significant changes

---

## Questions to Resolve

1. Should workspace ID be path-hash or UUID?
2. Should we version the API (`/api/v2/workspaces`) or use inline deprecation?
3. How to handle workspaces indexed with old paths (migration)?

Good luck! 🚀
