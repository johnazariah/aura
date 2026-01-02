# File-Aware RAG Queries for Step Execution

**Status:** ✅ Complete
**Completed:** 2025-12-05
**Created**: 2025-12-05

## Problem

When executing workflow steps like "Review current README.md", the agent doesn't receive the actual README.md content because:

1. RAG queries are semantic/topical (e.g., "README documentation getting started")
2. These queries return thematically similar content from many files, not the specific file mentioned in the step

## Solution

Parse step names and descriptions to extract explicit file references, then:

1. Add those file names as RAG queries (semantic match on file name)
2. Add a `SourcePathContains` filter to ensure results include the specific file

## Implementation

### 1. Add `SourcePathContains` to `RagQueryOptions` (RagOptions.cs)

```csharp
public IReadOnlyList<string>? SourcePathContains { get; init; }
```

### 2. Apply filter in `RagService.QueryAsync`

Filter results where `SourcePath` contains any of the specified strings (case-insensitive).

### 3. Add `ExtractFileReferences()` to `WorkflowService.cs`

Extract file names from step name/description using regex:

- Pattern: `\b[\w\-\.]+\.(md|cs|json|yaml|yml|xml|proj|props|targets|csproj|sln|ts|js|py)\b`

### 4. Modify `BuildRagQueriesForStep()`

- Extract file references from step name and description
- Add file names as high-priority queries
- Return file references for use in `SourcePathContains` filter

### 5. Update `CodebaseContextService`

Pass file filters when building RAG context.

## Files to Modify

- `src/Aura.Foundation/Rag/RagOptions.cs`
- `src/Aura.Foundation/Rag/RagService.cs`
- `src/Aura.Module.Developer/Services/WorkflowService.cs`
- `src/Aura.Module.Developer/Services/CodebaseContextService.cs`

## Testing

Re-run Step 1 of "Update README.md v2" workflow and verify:

- Agent receives actual README.md content
- Output references specific content from README.md
