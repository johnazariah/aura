# Path Normalization Code Review

**Status:** 📋 Planned  
**Priority:** High  
**Created:** 2025-01-09

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

## Scope

Review all instances of path handling in:
- `src/Aura.Api/Program.cs` - All endpoint path parameters
- `src/Aura.Foundation/Rag/` - Indexer path handling
- `src/Aura.Foundation/Git/GitService.cs` - Repository paths
- `src/Aura.Foundation/CodeGraph/` - Graph storage paths
- `src/Aura.Module.Developer/` - Workflow workspace paths

## Proposed Solution

1. **Create `PathNormalizer` utility class**
   ```csharp
   public static class PathNormalizer
   {
       public static string Normalize(string path)
       {
           var full = Path.GetFullPath(path);
           var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
           return OperatingSystem.IsWindows() ? trimmed.ToLowerInvariant() : trimmed;
       }
       
       public static bool AreEqual(string path1, string path2)
       {
           return string.Equals(
               Normalize(path1), 
               Normalize(path2),
               OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
       }
   }
   ```

2. **Use normalized paths consistently**
   - Normalize on input at API boundary
   - Store normalized paths in database
   - Use `PathNormalizer.AreEqual()` for comparisons

3. **Database migration** (if needed)
   - Update existing paths to normalized form
   - Add index on normalized path columns

## Tasks

- [ ] Audit all `Path.GetFullPath` calls
- [ ] Audit all path string comparisons
- [ ] Create `PathNormalizer` utility
- [ ] Update API endpoints to use normalizer
- [ ] Update RAG indexer
- [ ] Update code graph service
- [ ] Update git service
- [ ] Add unit tests for path normalization
- [ ] Consider database migration for existing data

## Related

- [api-review-harmonization.md](api-review-harmonization.md) - API consistency review
