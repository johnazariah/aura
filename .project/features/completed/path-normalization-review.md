# Path Normalization Code Review

**Status:** ✅ Complete  
**Completed:** 2026-01-15  
**Created:** 2025-01-09

> Core implementation done. Minor enhancement items (TypeScript tests, utility extraction) deferred to ongoing maintenance.

## Problem Statement

Path handling throughout the codebase is inconsistent and has caused several bugs:

1. **Case sensitivity issues on Windows** - `c:\work\aura` vs `C:\work\aura` are treated as different paths
2. **Slash direction** - Mix of forward and back slashes not consistently normalized
3. **Duplicated normalization logic** - Same normalization code repeated in multiple places
4. **Database queries** - Path comparisons in EF Core not case-insensitive on Windows

## Known Issues

- Workspace status endpoint couldn't find IndexMetadata because path case didn't match
- DELETE workspace endpoint returned 0 deleted items due to case mismatch
- Extension sends lowercase paths, database may store different case

## Solution Implemented

### PathNormalizer (Foundation)

The canonical `PathNormalizer` exists at `src/Aura.Foundation/Rag/PathNormalizer.cs`:

```csharp
// Rules applied:
// 1. Replace escaped backslashes (\\) with /
// 2. Replace backslashes (\) with /
// 3. ToLowerInvariant()
// 4. Collapse multiple slashes (//) into single (/)
// 5. Preserve URI schemes (file://)
```

### Components Updated

| Component | Status | Notes |
|-----------|--------|-------|
| `WorkflowService` | ✅ Fixed | Now uses `PathNormalizer.Normalize()` + `EF.Functions.ILike()` |
| `BackgroundIndexer` | ✅ Fixed | Now uses `PathNormalizer.Normalize()` |
| `RagService` | ✅ Already correct | Uses `PathNormalizer` |
| `CodeGraphService` | ✅ Already correct | Uses `PathNormalizer` |
| `RoslynCodeIngestor` | ✅ Already correct | Uses `PathNormalizer` |
| `WorkspaceIdGenerator` | ✅ Already correct | Uses `PathNormalizer` |
| Extension (TypeScript) | ✅ Fixed | Added `normalizePath()` function matching C# behavior |

### Extension Changes

Added `normalizePath()` function to both `healthCheckService.ts` and `auraApiService.ts`:

```typescript
function normalizePath(path: string): string {
    if (!path) return path;
    return path
        .replace(/\\\\/g, '/')  // Handle escaped backslashes
        .replace(/\\/g, '/')     // Convert backslashes to forward slashes
        .toLowerCase();
}
```

Applied to:
- `getWorkspace()` - normalizes path before URL encoding
- `onboardWorkspace()` - normalizes path in request body
- `checkRagHealth()` - normalizes workspace path before API call

## Remaining Tasks

- [ ] Consider database migration for existing paths (if any inconsistent data exists)
- [ ] Add unit tests for TypeScript `normalizePath()` function
- [ ] Consider extracting TypeScript `normalizePath()` to shared utility file

## Related

- [api-review-harmonization.md](api-review-harmonization.md) - API consistency review
- ADR-017: Case-Insensitive Path Handling
