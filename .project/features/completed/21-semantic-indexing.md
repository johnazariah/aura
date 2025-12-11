# Semantic Code Indexing

**Status:** ✅ Complete (Superseded by Ingester Agents)  
**Last Updated:** 2025-12-12

> **⚠️ SUPERSEDED**: This spec described an `IFileIndexingStrategy` pattern that has been replaced by the agent-based ingestion approach. See:
> - [Spec 22: Ingester Agents](./22-ingester-agents.md) - Agent-based file ingestion
> - [Spec 23: Hardcoded Agents](./23-hardcoded-agents.md) - Native C# agents
>
> The implementation uses `IAgent` with `ingest:*` capabilities instead of `IFileIndexingStrategy`.

## Overview (Historical)

The semantic indexer provides intelligent code understanding by dispatching to language-specific parsing strategies. Instead of treating all code as plain text, it understands the structure of each language and creates meaningful chunks (classes, functions, methods) with proper metadata.

## Architecture

```
SemanticIndexer
├── CSharpIndexingStrategy (Roslyn)
├── PythonIndexingStrategy (TreeSitter)
├── TypeScriptIndexingStrategy (TreeSitter)
├── JavaScriptIndexingStrategy (TreeSitter)
└── TextIndexingStrategy (fallback - blank-line chunking)
```

## Multi-Tier Fallback Strategy

From hve-hack's `UniversalCodeIngestionAgent`:

1. **Tier 1: AST Parsing** (TreeSitter/Roslyn)
   - Full syntax tree understanding
   - Extract classes, methods, functions with proper boundaries
   - Include docstrings/comments as context

2. **Tier 2: Pattern Matching** (Regex)
   - Language-specific patterns for common constructs
   - e.g., `class\s+(\w+)`, `def\s+(\w+)\(`
   - Works when AST parsing isn't available

3. **Tier 3: Blank-Line Chunking** (Fallback)
   - Split on blank lines
   - Works for any file type
   - Better than nothing

## Language Support

| Language | Strategy | Library | Status |
|----------|----------|---------|--------|
| C# | Roslyn | Microsoft.CodeAnalysis | ✅ Have CodeGraphIndexer |
| Python | TreeSitter | TreeSitterSharp | 🔧 Port from hve-hack |
| TypeScript | TreeSitter | TreeSitterSharp | 🔧 Port from hve-hack |
| JavaScript | TreeSitter | TreeSitterSharp | 🔧 Port from hve-hack |
| Rust | TreeSitter | TreeSitterSharp | 📋 Future |
| Go | TreeSitter | TreeSitterSharp | 📋 Future |
| Markdown | Text | N/A | ✅ Use text chunker |
| JSON/YAML | Text | N/A | ✅ Use text chunker |

## Implementation

### Interface

```csharp
public interface ISemanticIndexer
{
    Task<SemanticIndexResult> IndexDirectoryAsync(
        string directoryPath,
        SemanticIndexOptions? options = null,
        IProgress<SemanticIndexProgress>? progress = null,  // For UI feedback
        CancellationToken cancellationToken = default);
}

public interface IFileIndexingStrategy
{
    IReadOnlyList<string> SupportedExtensions { get; }
    int Priority { get; }
    Task<IReadOnlyList<SemanticChunk>> IndexFileAsync(string filePath, CancellationToken ct);
}

public record SemanticIndexProgress(
    int FilesProcessed,
    int TotalFiles,
    string CurrentFile,
    string CurrentLanguage,
    int ChunksCreated);
```

### Chunk Structure

```csharp
public record SemanticChunk
{
    public required string Text { get; init; }
    public required string FilePath { get; init; }
    public required string ChunkType { get; init; }  // "class", "method", "function", "text"
    public string? SymbolName { get; init; }
    public string? ParentSymbol { get; init; }
    public string? FullyQualifiedName { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public string? Language { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
```

## UI Progress Feedback

### VS Code Extension

When indexing is in progress, the extension should show:

1. **Progress notification** with cancel button
2. **Status bar** showing current file/progress
3. **Workflow panel** showing indexing state

```typescript
// In workflowPanelProvider.ts
await vscode.window.withProgress(
    {
        location: vscode.ProgressLocation.Notification,
        title: 'Indexing codebase...',
        cancellable: true
    },
    async (progress, token) => {
        // Subscribe to progress events from API via SSE or WebSocket
        progress.report({ message: 'Parsing C# files...', increment: 10 });
        progress.report({ message: 'Parsing Python files...', increment: 20 });
    }
);
```

### API Progress Endpoint

Option A: **Server-Sent Events (SSE)**
```
GET /api/developer/workflows/{id}/index/progress
Content-Type: text/event-stream

data: {"filesProcessed": 10, "totalFiles": 100, "currentFile": "Program.cs"}
data: {"filesProcessed": 11, "totalFiles": 100, "currentFile": "Startup.cs"}
```

Option B: **Polling**
```
GET /api/developer/workflows/{id}/index/status
{
    "status": "indexing",
    "filesProcessed": 10,
    "totalFiles": 100,
    "currentFile": "Program.cs",
    "chunksCreated": 45
}
```

## Files to Port from hve-hack

1. `AgentOrchestrator.Agents/ITreeSitterService.cs`
2. `AgentOrchestrator.Agents/TreeSitterService.cs`
3. `AgentOrchestrator.Agents/UniversalCodeIngestionAgent.cs` (refactor to strategy pattern)
4. `AgentOrchestrator.Agents/PythonIngestionAgent.cs` (patterns)
5. `AgentOrchestrator.Agents/TypeScriptIngestionAgent.cs` (patterns)

## Integration Points

### Workflow Creation
```csharp
// WorkflowService.CreateAsync
if (!string.IsNullOrEmpty(repositoryPath))
{
    // Create worktree (fast)
    var worktreeResult = await _worktreeService.CreateAsync(...);
    
    // Start background indexing (non-blocking)
    _ = Task.Run(async () => {
        await _semanticIndexer.IndexDirectoryAsync(
            workflow.WorkspacePath,
            progress: new WorkflowIndexProgress(workflow.Id, _hubContext));
    });
}
```

### Workflow Analyze Phase
```csharp
// WorkflowService.AnalyzeAsync
// Wait for indexing if still in progress
await WaitForIndexingAsync(workflowId, ct);

// Use indexed content for analysis
var context = await _ragService.QueryAsync(workflow.Description, topK: 10);
```

## Dependencies

```xml
<!-- TreeSitter support -->
<PackageReference Include="TreeSitterSharp" Version="0.5.0" />

<!-- Already have for Roslyn -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="..." />
```

## Implementation Phases

### Phase 1: Infrastructure
- [ ] Create `ISemanticIndexer` interface
- [ ] Create `IFileIndexingStrategy` interface
- [ ] Create `SemanticIndexer` dispatcher

### Phase 2: C# Strategy
- [ ] Adapt existing `CodeGraphIndexer` to implement `IFileIndexingStrategy`
- [ ] Ensure it produces `SemanticChunk` output

### Phase 3: TreeSitter Strategies
- [ ] Port `ITreeSitterService` from hve-hack
- [ ] Port `TreeSitterService` implementation
- [ ] Create `PythonIndexingStrategy`
- [ ] Create `TypeScriptIndexingStrategy`

### Phase 4: Text Fallback
- [ ] Create `TextIndexingStrategy` using existing RAG chunker
- [ ] Handle markdown, JSON, YAML, config files

### Phase 5: UI Progress
- [ ] Add SSE endpoint for indexing progress
- [ ] Update workflow panel to show progress
- [ ] Add cancellation support

### Phase 6: Integration
- [ ] Wire up to workflow creation (background)
- [ ] Wire up to analyze phase (wait if needed)
- [ ] Add "Index Now" button to UI

## Status

**Status:** Not Started  
**Priority:** High (blocks effective workflow analysis)  
**Estimated Effort:** 1-2 weeks  
**Dependencies:** TreeSitterSharp NuGet package
