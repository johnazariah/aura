# Worktree-Aware Indexing

**Status:** 🚧 In Progress  
**Created:** 2026-01-16  
**Priority:** High (blocking refactoring in worktrees)

## Problem Statement

Aura's code indexing and refactoring tools fail when invoked from a git worktree because:

1. **Path Resolution**: The Roslyn workspace service uses solution paths as cache keys, but doesn't account for worktrees pointing to the same logical repository
2. **Stale Cache**: The `ConcurrentDictionary<string, MSBuildWorkspace>` cache in `RoslynWorkspaceService` persists stale data even after MCP server restart
3. **Index Location**: `aura_search` returns paths from the main repository even when `workspacePath` points to a worktree

## Root Cause Analysis

Git worktrees have a `.git` **file** (not directory) containing:
```
gitdir: C:/work/aura/.git/worktrees/aura-workflow-add-health-check-endpoint-documentation-6f19e75aac274a
```

This means:
- Worktree path: `c:\work\aura-workflow-add-health-check-endpoint-documentation-6f19e75aac274a`
- Main repo path: `c:\work\aura`
- Both contain identical solution structure but may have different file contents

Current behavior:
- Aura indexes/caches based on main repo path
- Refactoring operations find symbols in cached (potentially stale) state
- Results reference main repo paths, not worktree paths

## Architecture: Shared Index, Isolated Workspaces

### Key Constraint
Code graph and RAG index extraction is **expensive** (minutes for large repos). We cannot rebuild for every worktree.

### Solution: Two-Tier Architecture

| Operation Type | Data Source | Why |
|---------------|-------------|-----|
| `aura_search` | **Shared** RAG index | Semantic embeddings are repo-wide |
| `aura_navigate` | **Shared** code graph | Symbol relationships don't change per-worktree |
| `aura_inspect` | **Shared** code graph | Type structure is stable |
| `aura_refactor` | **Fresh** Roslyn workspace | Must see worktree's actual file contents |
| `aura_generate` | **Fresh** Roslyn workspace | Must write to worktree paths |
| `aura_validate` | **Fresh** build | Must compile worktree's code |

### Path Translation Strategy

**For READ operations** (search, navigate, inspect):
1. Resolve worktree → main repo for index lookup
2. Query shared index using main repo path
3. Translate paths in results: `mainRepoPath` → `worktreePath`

**For WRITE operations** (refactor, generate, validate):
1. Load MSBuildWorkspace from WORKTREE solution path directly
2. Cache key = worktree solution path (NOT main repo)
3. No path translation needed - already using worktree paths

## Implementation Plan

### Phase 1: Worktree Detection Utility

```csharp
public record WorktreeInfo(
    string WorktreePath,      // The worktree location (e.g., c:\work\aura-workflow-xyz)
    string MainRepoPath,      // The main repository (e.g., c:\work\aura)
    string GitDir,            // The worktree-specific git dir
    bool IsWorktree           // True if this is a worktree, not the main repo
);

public static class GitWorktreeDetector
{
    /// <summary>
    /// Detects if a path is within a git worktree.
    /// Parses .git file (not directory) to find main repo.
    /// </summary>
    public static WorktreeInfo? DetectWorktree(string path);
    
    /// <summary>
    /// Translates a path from main repo to worktree.
    /// </summary>
    public static string TranslatePath(string mainRepoPath, WorktreeInfo worktree);
}
```

### Phase 2: Update RoslynWorkspaceService

```csharp
// Cache key changes from:
private readonly ConcurrentDictionary<string, MSBuildWorkspace> _workspaces;

// To include worktree awareness:
private readonly ConcurrentDictionary<string, MSBuildWorkspace> _workspaces;
// Key = normalized absolute path (worktree path if in worktree, main repo otherwise)
// Each worktree gets its OWN workspace instance
```

Key changes:
- `GetSolutionAsync(solutionPath)` → detects worktree, uses worktree path as cache key
- Each worktree loads its own MSBuildWorkspace (cheap - just loads from disk)
- Main repo workspace is NOT shared with worktrees

### Phase 3: Path Translation in Read Operations

Update `McpHandler` for read operations:

```csharp
// aura_search, aura_navigate, aura_inspect
private async Task<object> SearchAsync(JsonElement? args, CancellationToken ct)
{
    var workspacePath = args?.GetProperty("workspacePath").GetString();
    var worktreeInfo = GitWorktreeDetector.DetectWorktree(workspacePath);
    
    // Query using main repo path (where index lives)
    var queryPath = worktreeInfo?.MainRepoPath ?? workspacePath;
    var results = await _ragService.QueryAsync(query, queryPath, ct);
    
    // Translate results back to worktree paths
    if (worktreeInfo?.IsWorktree == true)
    {
        results = results.Select(r => r with 
        { 
            FilePath = GitWorktreeDetector.TranslatePath(r.FilePath, worktreeInfo) 
        });
    }
    
    return results;
}
```

### Phase 4: Cache Management MCP Tool

Add `aura_workspace` tool:

```csharp
// Operations:
// - detect_worktree: Return worktree info for current workspace
// - invalidate_cache: Clear cached Roslyn workspace
// - status: Show what's cached and index state
```

## Files to Modify

| File | Change |
|------|--------|
| `src/Aura.Foundation/Git/GitWorktreeDetector.cs` | **NEW** - Worktree detection utility |
| `src/Aura.Module.Developer/Services/RoslynWorkspaceService.cs` | Use worktree path as cache key |
| `src/Aura.Module.Developer/Services/IRoslynWorkspaceService.cs` | Add `InvalidateCache(path)` method |
| `src/Aura.Api/Mcp/McpHandler.cs` | Path translation for read operations |
| `tests/Aura.Foundation.Tests/Git/GitWorktreeDetectorTests.cs` | **NEW** - Unit tests |

## Success Criteria

1. ✅ `aura_refactor` works correctly when invoked from a worktree
2. ✅ `aura_search` returns paths translated to the requesting worktree
3. ✅ `aura_inspect` shows current file state from worktree, not stale cache
4. ✅ Each worktree has isolated Roslyn workspace (no cross-contamination)
5. ✅ RAG index is shared (no re-indexing per worktree)
6. ✅ Cache can be invalidated via MCP tool
7. ✅ No performance regression for non-worktree usage

## Safety: Worktree Isolation

**CRITICAL**: Agents must NEVER write to files outside the worktree.

The MCP instructions now include:
```
If you are working in a git worktree:
1. NEVER navigate or write to the parent/main repository
2. All file operations must use paths within the current workspace
3. Do not use `..` or absolute paths that escape the worktree
```

## References

- Git worktree documentation: https://git-scm.com/docs/git-worktree
- Issue discovered during Workflow→Story rename attempt
- `git rev-parse --git-common-dir` returns main repo .git path
