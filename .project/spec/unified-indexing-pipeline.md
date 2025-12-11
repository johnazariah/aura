# Unified Indexing Pipeline

**Status:** Approved  
**Priority:** High (blocking demo)  
**Created:** 2025-12-12

## Problem Statement

The current codebase has two competing indexing pipelines:

1. **BackgroundIndexer** (Foundation) - indexes all files to RAG, looks for ingesters
2. **DeveloperSemanticIndexer** (Developer Module) - indexes C# to code graph, markdown to RAG

This creates confusion, inconsistent behavior, and violates the layered architecture where modules should **extend** Foundation capabilities, not replace them.

### Symptoms
- Extension calls wrong endpoint (`/api/semantic/index` instead of `/api/index/background`)
- C# files go to code graph but NOT to RAG embeddings
- Other code files (TypeScript, Python) are only indexed if using BackgroundIndexer
- Two different timeout behaviors (synchronous vs async)

## Desired Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   BackgroundIndexer                              │
│   (single entry point for all indexing)                         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   IngestorRegistry                               │
│   file extension → IContentIngestor                              │
└─────────────────────────────────────────────────────────────────┘
         │                         │                    │
         ▼                         ▼                    ▼
┌─────────────────┐     ┌────────────────────┐   ┌──────────────┐
│ Foundation      │     │ Developer Module    │   │ Future Module│
│ Ingestors       │     │ Code Ingestors      │   │ Ingestors    │
├─────────────────┤     ├────────────────────┤   ├──────────────┤
│ MarkdownIngestor│     │ RoslynCodeIngestor │   │ PdfIngestor  │
│ PlainTextIngest │     │ TreeSitterIngestor │   │ DocxIngestor │
└─────────────────┘     └────────────────────┘   └──────────────┘
         │                         │
         ▼                         ▼
┌─────────────────┐     ┌────────────────────┐
│  RAG Chunks     │     │  RAG Chunks        │
│  (embeddings)   │     │  (embeddings)      │
│                 │     │  + Code Graph      │
└─────────────────┘     └────────────────────┘
```

## Design Principles

1. **Single Pipeline**: One indexing path (`BackgroundIndexer`) for all content
2. **Module Extension**: Modules register specialized ingesters at startup
3. **Ingestor Contract**: Ingestors produce chunks; some also produce graph nodes
4. **Same Pass**: Code graph populated during ingestion, not as separate phase

## Interface Changes

### Extend IContentIngestor

```csharp
public interface IContentIngestor
{
    // ... existing members ...
    
    /// <summary>
    /// Whether this ingestor also populates the code graph.
    /// </summary>
    bool PopulatesCodeGraph { get; }
}
```

Or, create a sub-interface for code-aware ingestors:

```csharp
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

### Module Registration

```csharp
// In DeveloperModule.ConfigureServices
public void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    // Register code ingestors with higher priority than Foundation's CodeIngestor
    services.AddSingleton<IContentIngestor, RoslynCodeIngestor>();
    // ... or use a startup hook to register with IIngestorRegistry
}
```

### Module Startup Hook

Add a new method to `IAuraModule`:

```csharp
public interface IAuraModule
{
    // ... existing members ...
    
    /// <summary>
    /// Called after all services are built, before the host starts.
    /// Use this to register ingestors and other runtime extensions.
    /// </summary>
    void OnStartup(IServiceProvider services);
}
```

## Implementation Plan

### Phase 1: Consolidate Pipeline (This PR)

1. **Create `ICodeIngestor` interface** in Foundation
   - Extends `IContentIngestor`
   - Adds `IngestCodeAsync` method returning chunks + graph nodes
   
2. **Update `BackgroundIndexer`** to handle `ICodeIngestor`
   - If ingestor is `ICodeIngestor`, call `IngestCodeAsync` and save graph nodes
   - Otherwise, call normal `IngestAsync`

3. **Create `RoslynCodeIngestor`** in Developer Module
   - Implements `ICodeIngestor`
   - Uses existing Roslyn workspace code
   - Produces both RAG chunks and code graph nodes in single pass

4. **Add `OnStartup` to `IAuraModule`** 
   - Called after DI container is built
   - Developer Module registers `RoslynCodeIngestor` with registry

5. **Delete `DeveloperSemanticIndexer`**
   - Remove `/api/semantic/index` endpoint (or make it alias)
   - Extension uses `/api/index/background` only

6. **Update Extension**
   - Already calls `startBackgroundIndex` - just needs reload

### Phase 2: Improve Foundation's CodeIngestor (Future)

- Keep regex-based `CodeIngestor` as fallback for non-C# languages
- Add TreeSitter support for better multi-language parsing
- Developer Module's `RoslynCodeIngestor` takes priority for `.cs` files

## Files to Modify

| File | Change |
|------|--------|
| `Aura.Foundation/Rag/Ingestors/IContentIngestor.cs` | Add `ICodeIngestor` interface |
| `Aura.Foundation/Rag/BackgroundIndexer.cs` | Handle `ICodeIngestor` |
| `Aura.Foundation/Modules/IAuraModule.cs` | Add `OnStartup` method |
| `Aura.Module.Developer/DeveloperModule.cs` | Implement `OnStartup`, register ingestor |
| `Aura.Module.Developer/Ingestors/RoslynCodeIngestor.cs` | New file |
| `Aura.Module.Developer/Services/DeveloperSemanticIndexer.cs` | Delete |
| `Aura.Api/Program.cs` | Remove/alias `/api/semantic/index` |

## Open Questions

1. **Dependency Direction**: `ICodeIngestor` needs `CodeNode`/`CodeEdge` which are in Foundation. Should the graph entities stay in Foundation? ✅ Yes - they're database entities.

2. **Roslyn Dependency**: `RoslynCodeIngestor` needs Roslyn. Should it live in Developer Module or Foundation?
   - ✅ **Decision:** Developer Module (Roslyn is heavy, not all deployments need it)

3. **Priority vs Replacement**: When Developer Module's `RoslynCodeIngestor` is registered, should it completely replace Foundation's `CodeIngestor` for `.cs` files?
   - ✅ **Decision:** Yes, module ingestors take priority (already how registry works)

4. **Module Startup Hook**: Should we add `OnStartup` to `IAuraModule` or use a separate pattern?
   - ✅ **Decision:** Use `IStartupTask` interface (see Decisions below)

## Decisions

### Decision 1: ICodeIngestor Interface

**Context:** Need code ingestors to produce both RAG chunks (for semantic search) and code graph nodes (for structural queries) in a single pass.

**Decision:** Create `ICodeIngestor` interface extending `IContentIngestor`:

```csharp
public interface ICodeIngestor : IContentIngestor
{
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

**Rationale:**
- Single pass efficiency (read file once, produce both outputs)
- Clean extension of existing interface
- BackgroundIndexer checks `is ICodeIngestor` and handles accordingly

### Decision 2: IStartupTask for Module Initialization

**Context:** Modules need to register ingestors after DI container is built but before app starts processing requests.

**Options Considered:**

| Option | Pros | Cons |
|--------|------|------|
| A. `OnStartup` on `IAuraModule` | Simple, one interface | Forces empty methods, mixes concerns |
| B. `IStartupTask` interface | Clean separation, ordering, optional | Another abstraction |
| C. `IHostedService` | Standard .NET | Wrong pattern (for background services) |

**Decision:** Option B - `IStartupTask` interface

```csharp
public interface IStartupTask
{
    /// <summary>Priority order (lower = runs first). Foundation = 0-99, Modules = 100+</summary>
    int Order => 100;
    
    /// <summary>Execute startup logic.</summary>
    Task ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}
```

**Rationale:**
- **Single Responsibility:** Modules do DI registration, startup tasks do runtime initialization
- **Ordering:** Foundation registers base ingestors (Order=0), modules override (Order=100+)
- **Optional:** Modules without startup needs register no tasks
- **Extensible:** Non-module code can add startup tasks
- **Async-ready:** Supports future async initialization needs

### Decision 3: Single Indexing Pipeline

**Context:** Currently two competing pipelines (BackgroundIndexer vs DeveloperSemanticIndexer) cause confusion and inconsistent behavior.

**Decision:** Consolidate to single pipeline:
- `BackgroundIndexer` is the only entry point
- Delete `DeveloperSemanticIndexer`
- Remove `/api/semantic/index` endpoint
- Extension uses `/api/index/background` exclusively

**Rationale:**
- One way to do things eliminates confusion
- Modules extend via ingestor registration, not parallel pipelines
- Background indexing handles large repos without timeout

## Success Criteria

- [ ] Single indexing endpoint: `/api/index/background`
- [ ] C# files produce both RAG chunks AND code graph nodes
- [ ] Other code files (TS, Py) produce RAG chunks via Foundation ingestor
- [ ] No timeout errors for large repos
- [ ] Progress tracking works
- [ ] Extension works without code changes
