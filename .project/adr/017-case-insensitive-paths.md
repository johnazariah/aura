# ADR-017: Case-Insensitive Path Handling

## Status
Accepted

## Date
2025-12-05

## Context

Windows file paths are case-insensitive, but string comparisons in .NET are case-sensitive by default. This caused several bugs:

1. **RAG index mismatch** - Files indexed as `C:\work\Brightsword` couldn't be found when querying with `C:\work\BrightSword`
2. **Workflow filtering** - VS Code extension opening `c:\work\brightsword` couldn't find workflows created for `C:\work\BrightSword`

These issues manifested as:
- RAG queries returning no results despite files being indexed
- Workflows not appearing in the VS Code extension tree view

## Decision

Normalize all paths consistently throughout the system:

### Path Normalization Rules

1. **Convert to lowercase** for comparison
2. **Use forward slashes** for consistency (or normalize backslashes)
3. **Trim trailing slashes**
4. **Use case-insensitive comparison** (e.g., `ILike` in PostgreSQL, `ToLowerInvariant()` in C#)

### Implementation Points

1. **RagService.cs** - Normalize `SourcePath` when storing chunks
2. **RagService.cs** - Use `EF.Functions.ILike` for case-insensitive path prefix matching
3. **WorkflowService.cs** - Use `ToLowerInvariant()` when filtering workflows by `repositoryPath`

### Normalization Helper

```csharp
private static string NormalizePath(string path) =>
    path.Replace('\\', '/').ToLowerInvariant();
```

## Consequences

### Positive
- RAG queries work regardless of path casing
- Workflows display correctly in VS Code extension
- Consistent behavior matching Windows filesystem semantics

### Negative
- Slight performance overhead for normalization
- Existing indexed data may need re-indexing after the fix
- Must remember to normalize at both storage and query time

## Notes

After applying this fix, clear the RAG index and re-index to ensure all paths are normalized consistently.
