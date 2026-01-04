# Unified Indexing Backend

**Status:** ✅ Complete  
**Completed:** 2026-01-02  
**Created:** 2025-12-12  
**Consolidates:** unified-indexing-pipeline.md, unified-indexing-implementation.md

## Problem Statement

Aura currently has **two separate indexing pipelines**:

1. **BackgroundIndexer** (Foundation) → RAG chunks only
2. **DeveloperSemanticIndexer** (Developer Module) → Code graph nodes only

This creates problems:
- **Duplicate work**: Both pipelines read files, parse with Roslyn, walk syntax trees
- **Race conditions**: Two async pipelines can conflict on shared resources
- **UX confusion**: "Index Codebase" does RAG, but users also need "Index Code Graph" separately
- **Maintenance burden**: Two implementations to keep in sync

## Solution

Create a **single unified pipeline** where code ingestors produce both RAG chunks AND code graph nodes in a single pass.

```
┌─────────────────────────────────────────────────────────────────┐
│                    /api/index/background                        │
│                           ↓                                     │
│                   BackgroundIndexer                             │
│                           ↓                                     │
│              ┌────────────────────────┐                         │
│              │   IContentIngestor?    │                         │
│              └───────────┬────────────┘                         │
│                          │                                      │
│         ┌────────────────┼────────────────┐                     │
│         ↓                ↓                ↓                     │
│    ICodeIngestor    ICodeIngestor    IContentIngestor           │
│   (RoslynCode)    (TreeSitter)      (Text/Fallback)             │
│         ↓                ↓                ↓                     │
│    CodeIngestion     CodeIngestion    IngestedChunk             │
│       Result           Result              ↓                    │
│    ↓        ↓       ↓        ↓        RAG only                  │
│  Chunks + Nodes   Chunks + Nodes                                │
│    ↓        ↓       ↓        ↓                                  │
│   RAG    Graph     RAG    Graph                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Technical Design

### New Interface: ICodeIngestor

```csharp
/// <summary>
/// Extended content ingestor that produces both RAG chunks and code graph nodes.
/// Implementations should do a single parse pass to extract both.
/// </summary>
public interface ICodeIngestor : IContentIngestor
{
    /// <summary>
    /// Ingests code and produces both RAG chunks and code graph nodes.
    /// </summary>
    Task<CodeIngestionResult> IngestCodeAsync(
        string filePath,
        string content,
        string workspacePath,
        CancellationToken cancellationToken = default);
}

public record CodeIngestionResult(
    IReadOnlyList<IngestedChunk> Chunks,
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);
```

### BackgroundIndexer Changes

```csharp
// In ProcessFileAsync
if (ingestor is ICodeIngestor codeIngestor)
{
    var result = await codeIngestor.IngestCodeAsync(filePath, content, workspacePath, ct);
    
    // Store RAG chunks (existing flow)
    await SaveChunksAsync(result.Chunks, ct);
    
    // Store code graph (new)
    await _codeGraphService.SaveNodesAsync(result.Nodes, ct);
    await _codeGraphService.SaveEdgesAsync(result.Edges, ct);
}
else
{
    // Fall back to existing flow for non-code ingestors
    var chunks = await ingestor.IngestAsync(filePath, content, ct);
    await SaveChunksAsync(chunks, ct);
}
```

### Module Startup: IStartupTask

Modules need to register ingestors after DI container is built but before app starts.

```csharp
public interface IStartupTask
{
    /// <summary>Priority order (lower = runs first). Foundation = 0-99, Modules = 100+</summary>
    int Order => 100;
    
    /// <summary>Execute startup logic.</summary>
    Task ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}

// Developer Module implementation:
public class RegisterIngestorsTask : IStartupTask
{
    public int Order => 100;
    
    public Task ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var registry = services.GetRequiredService<IIngestorRegistry>();
        registry.Register(new RoslynCodeIngestor(...), priority: 10);
        return Task.CompletedTask;
    }
}
```

### RoslynCodeIngestor

New class in Developer Module that combines current:
- `CSharpIngesterAgent` (RAG chunk extraction)
- `DeveloperSemanticIndexer` (Code graph extraction)

```csharp
public class RoslynCodeIngestor : ICodeIngestor
{
    public bool CanProcess(string filePath, string? content)
        => filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    public int Priority => 10; // Highest priority for .cs files

    public async Task<CodeIngestionResult> IngestCodeAsync(
        string filePath, string content, string workspacePath, CancellationToken ct)
    {
        // Single Roslyn parse
        var tree = CSharpSyntaxTree.ParseText(content);
        var root = await tree.GetRootAsync(ct);
        
        var chunks = new List<IngestedChunk>();
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();
        
        // Walk tree once, extract both chunks and nodes
        var walker = new UnifiedSyntaxWalker(chunks, nodes, edges, filePath);
        walker.Visit(root);
        
        return new CodeIngestionResult(chunks, nodes, edges);
    }
}
```

## Implementation Tasks

### Task 1: ICodeIngestor Interface (30 min)

**File:** `src/Aura.Foundation/Rag/Ingestors/IContentIngestor.cs`

- Add `ICodeIngestor` interface extending `IContentIngestor`
- Add `CodeIngestionResult` record

### Task 2: IStartupTask Interface (30 min)

**File:** `src/Aura.Foundation/Modules/IStartupTask.cs` (new)

- Create interface with `Order` property and `ExecuteAsync` method
- Wire up in `Program.cs` to run all `IStartupTask` after DI build

### Task 3: BackgroundIndexer Enhancement (1 hour)

**File:** `src/Aura.Foundation/Rag/BackgroundIndexer.cs`

- Check if ingestor is `ICodeIngestor`
- Call `IngestCodeAsync` and handle both chunks + nodes
- Inject `ICodeGraphService` (already exists)

### Task 4: RoslynCodeIngestor (2 hours)

**File:** `src/Aura.Module.Developer/Ingestors/RoslynCodeIngestor.cs` (new)

- Implement `ICodeIngestor`
- Merge logic from `CSharpIngesterAgent` and `DeveloperSemanticIndexer`
- Single Roslyn parse producing both outputs

### Task 5: Developer Module Startup Task (30 min)

**File:** `src/Aura.Module.Developer/DeveloperModuleStartupTask.cs` (new)

- Implement `IStartupTask`
- Register `RoslynCodeIngestor` with registry at priority 10

### Task 6: Delete Old Code (30 min)

- Delete `DeveloperSemanticIndexer.cs`
- Remove `/api/semantic/index` endpoint from `Program.cs`
- Update any tests

## Files to Modify

| File | Change |
|------|--------|
| `Aura.Foundation/Rag/Ingestors/IContentIngestor.cs` | Add `ICodeIngestor` interface |
| `Aura.Foundation/Modules/IStartupTask.cs` | New file |
| `Aura.Foundation/Rag/BackgroundIndexer.cs` | Handle `ICodeIngestor` |
| `Aura.Api/Program.cs` | Run startup tasks, remove old endpoint |
| `Aura.Module.Developer/Ingestors/RoslynCodeIngestor.cs` | New file |
| `Aura.Module.Developer/DeveloperModuleStartupTask.cs` | New file |
| `Aura.Module.Developer/Services/DeveloperSemanticIndexer.cs` | Delete |

## Decisions Made

### Decision 1: ICodeIngestor Interface

**Decision:** Create `ICodeIngestor` interface extending `IContentIngestor`.

**Rationale:**
- Single pass efficiency (read file once, produce both outputs)
- Clean extension of existing interface
- BackgroundIndexer checks `is ICodeIngestor` and handles accordingly

### Decision 2: IStartupTask for Module Initialization

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| A. `OnStartup` on `IAuraModule` | Simple | Forces empty methods, mixes concerns |
| B. `IStartupTask` interface | Clean separation, ordering | Another abstraction |
| C. `IHostedService` | Standard .NET | Wrong pattern |

**Decision:** Option B - `IStartupTask` interface

**Rationale:**
- **Single Responsibility:** Modules do DI registration, startup tasks do runtime init
- **Ordering:** Foundation registers base ingestors (Order=0), modules override (Order=100+)
- **Optional:** Modules without startup needs register no tasks
- **Async-ready:** Supports future async initialization

### Decision 3: Single Indexing Pipeline

**Decision:** Consolidate to single pipeline via BackgroundIndexer.

**Rationale:**
- One way to do things eliminates confusion
- Modules extend via ingestor registration, not parallel pipelines
- Background indexing handles large repos without timeout

## Success Criteria

- [ ] Single indexing endpoint: `/api/index/background`
- [ ] C# files produce both RAG chunks AND code graph nodes in single pass
- [ ] Other code files (TS, Py) produce RAG chunks via Foundation ingestors
- [ ] No timeout errors for large repos (background processing)
- [ ] Progress tracking works (existing infrastructure)
- [ ] Extension works without code changes (uses same API)
- [ ] DeveloperSemanticIndexer deleted
- [ ] /api/semantic/index endpoint removed
