# Unified Indexing Pipeline - Implementation Plan

**Spec:** [unified-indexing-pipeline.md](../spec/unified-indexing-pipeline.md)  
**Target:** Demo-ready in ~4 hours  
**Created:** 2025-12-12

## Task Breakdown

### 1. Foundation Changes (1.5 hours)

#### 1.1 Create ICodeIngestor Interface (15 min)
**File:** `Aura.Foundation/Rag/Ingestors/IContentIngestor.cs`

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

#### 1.2 Update BackgroundIndexer (30 min)
**File:** `Aura.Foundation/Rag/BackgroundIndexer.cs`

- Inject `ICodeGraphService` 
- In `ProcessDirectoryAsync`, check if ingestor is `ICodeIngestor`
- If yes: call `IngestCodeAsync`, save graph nodes/edges via `ICodeGraphService`
- Always save RAG chunks as before

#### 1.3 Add IStartupTask Interface (15 min)
**File:** `Aura.Foundation/Startup/IStartupTask.cs` (new)

```csharp
public interface IStartupTask
{
    int Order => 100;
    Task ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}
```

**File:** `Aura.Foundation/Startup/StartupTaskRunner.cs` (new)
- Static method to discover and run all `IStartupTask` in order

**File:** `Aura.Api/Program.cs`
- Call `StartupTaskRunner.RunAsync(app.Services)` after build, before run

#### 1.4 Update IngestorRegistry (15 min)
**File:** `Aura.Foundation/Rag/Ingestors/IngestorRegistry.cs`

- Ensure module-registered ingestors take priority
- Log which ingestor handles each extension for debugging

### 2. Developer Module Changes (1.5 hours)

#### 2.1 Create RoslynCodeIngestor (45 min)
**File:** `Aura.Module.Developer/Ingestors/RoslynCodeIngestor.cs`

- Implements `ICodeIngestor`
- Uses existing `IRoslynWorkspaceService` 
- Extracts from `CodeGraphIndexer` logic but outputs:
  - `IngestedChunk` list (for RAG embeddings)
  - `CodeNode` list (for graph)
  - `CodeEdge` list (for relationships)

Key difference from current:
- Current `CodeGraphIndexer` writes directly to DB
- New `RoslynCodeIngestor` returns data, caller writes

#### 2.2 Create RegisterCodeIngestorsTask (15 min)
**File:** `Aura.Module.Developer/Startup/RegisterCodeIngestorsTask.cs` (new)

```csharp
public class RegisterCodeIngestorsTask : IStartupTask
{
    public int Order => 100; // After Foundation
    
    public Task ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var registry = services.GetRequiredService<IIngestorRegistry>();
        var roslynService = services.GetRequiredService<IRoslynWorkspaceService>();
        var graphService = services.GetRequiredService<ICodeGraphService>();
        var logger = services.GetRequiredService<ILogger<RoslynCodeIngestor>>();
        
        registry.Register(new RoslynCodeIngestor(roslynService, graphService, logger));
        return Task.CompletedTask;
    }
}
```

**File:** `Aura.Module.Developer/DeveloperModule.cs`
- Register: `services.AddSingleton<IStartupTask, RegisterCodeIngestorsTask>();`

#### 2.3 Delete DeveloperSemanticIndexer (15 min)
- Delete `Services/DeveloperSemanticIndexer.cs`
- Remove DI registration
- Remove `ISemanticIndexer` interface if no longer needed

### 3. API Changes (30 min)

#### 3.1 Update/Remove Endpoints
**File:** `Aura.Api/Program.cs`

- Remove `/api/semantic/index` endpoint OR
- Make it redirect to `/api/index/background`

#### 3.2 Ensure Background Indexer Injects Graph Service
- Verify DI is correct for new dependencies

### 4. Testing (30 min)

#### 4.1 Unit Tests
- Test `RoslynCodeIngestor` produces both chunks and nodes
- Test `BackgroundIndexer` handles `ICodeIngestor`

#### 4.2 Integration Test
- Index BrightSword repo
- Verify RAG chunks exist for `.cs` files
- Verify code graph nodes exist

### 5. Cleanup (15 min)

- Remove unused code
- Update any stale comments
- Commit with clear message

## Dependency Order

```
1.1 ICodeIngestor interface
    ↓
1.2 BackgroundIndexer update ←── depends on 1.1
    ↓
1.3 Module startup hook
    ↓
2.1 RoslynCodeIngestor ←── depends on 1.1
    ↓
2.2 DeveloperModule.OnStartup ←── depends on 1.3, 2.1
    ↓
2.3 Delete DeveloperSemanticIndexer
    ↓
3.1 API cleanup
    ↓
4.x Testing
```

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Roslyn workspace caching issues | RoslynCodeIngestor is stateless, uses existing service |
| Large files cause timeout | BackgroundIndexer already async |
| Graph edges break | Edges created in same pass, data stays consistent |

## Rollback Plan

If something goes wrong:
1. Keep `DeveloperSemanticIndexer` but mark deprecated
2. Add feature flag to choose pipeline
3. Extension can fall back to old endpoint

## Commits

1. `feat(foundation): add ICodeIngestor interface and update BackgroundIndexer`
2. `feat(developer): add RoslynCodeIngestor for unified C# indexing`
3. `refactor(developer): remove DeveloperSemanticIndexer, use unified pipeline`
4. `refactor(api): remove /api/semantic/index endpoint`
