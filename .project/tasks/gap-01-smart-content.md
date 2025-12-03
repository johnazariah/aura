# Task: Smart Content (LLM-Generated Summaries)

## Overview
Generate concise LLM summaries for every indexed code element (file, class, method). Store both the summary and its embedding for semantic search by intent, not just code similarity.

## Parent Spec
`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 1

## Goals
1. Every `CodeNode` and `SemanticChunk` gets a `SmartSummary` field
2. Summaries are generated asynchronously during/after indexing
3. Summaries are embedded separately for intent-based search
4. Dual-vector search: match by code OR by intent

## Data Model Changes

### 1. Add SmartSummary to CodeNode

**File:** `src/Aura.Foundation/Data/Entities/CodeNode.cs`

```csharp
/// <summary>Gets the LLM-generated summary explaining what this code does.</summary>
public string? SmartSummary { get; init; }

/// <summary>Gets the embedding vector for the smart summary (intent-based search).</summary>
public Vector? SmartSummaryEmbedding { get; init; }

/// <summary>Gets when the smart summary was last generated.</summary>
public DateTimeOffset? SmartSummaryGeneratedAt { get; init; }
```

### 2. Add SmartSummary to SemanticChunk

**File:** `src/Aura.Foundation/Rag/ISemanticIndexer.cs`

```csharp
public record SemanticChunk
{
    // Existing fields...
    
    /// <summary>Gets the LLM-generated summary.</summary>
    [JsonPropertyName("smartSummary")]
    public string? SmartSummary { get; init; }
}
```

### 3. Add SmartSummary to RagChunk

**File:** `src/Aura.Foundation/Data/Entities/RagChunk.cs`

```csharp
/// <summary>Gets or sets the LLM-generated summary.</summary>
public string? SmartSummary { get; set; }

/// <summary>Gets or sets the embedding for the smart summary.</summary>
public Vector? SmartSummaryEmbedding { get; set; }
```

## Database Migration

```sql
ALTER TABLE code_nodes ADD COLUMN smart_summary TEXT;
ALTER TABLE code_nodes ADD COLUMN smart_summary_embedding vector(1024);
ALTER TABLE code_nodes ADD COLUMN smart_summary_generated_at TIMESTAMPTZ;

ALTER TABLE rag_chunks ADD COLUMN smart_summary TEXT;
ALTER TABLE rag_chunks ADD COLUMN smart_summary_embedding vector(1024);

-- Index for vector search on summaries
CREATE INDEX idx_code_nodes_smart_summary_embedding 
ON code_nodes USING ivfflat (smart_summary_embedding vector_cosine_ops);

CREATE INDEX idx_rag_chunks_smart_summary_embedding 
ON rag_chunks USING ivfflat (smart_summary_embedding vector_cosine_ops);
```

## New Service: ISmartContentService

**File:** `src/Aura.Foundation/Rag/ISmartContentService.cs`

```csharp
public interface ISmartContentService
{
    /// <summary>
    /// Generates smart summaries for code nodes that don't have them.
    /// </summary>
    Task<SmartContentResult> GenerateSummariesAsync(
        string? workspacePath = null,
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a smart summary for a single code element.
    /// </summary>
    Task<string> GenerateSummaryAsync(
        string code,
        string elementType,
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of nodes needing smart summary generation.
    /// </summary>
    Task<int> GetPendingCountAsync(string? workspacePath = null, CancellationToken ct = default);
}

public record SmartContentResult(
    int Processed,
    int Failed,
    TimeSpan Duration,
    List<string> Errors);
```

## Implementation: SmartContentService

**File:** `src/Aura.Foundation/Rag/SmartContentService.cs`

```csharp
public sealed class SmartContentService : ISmartContentService
{
    private readonly AuraDbContext _dbContext;
    private readonly ILlmProvider _llmProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<SmartContentService> _logger;
    private readonly SmartContentOptions _options;

    private const string SummaryPrompt = """
        Summarize what this {0} does in 1-2 sentences. Be concise and focus on purpose, not implementation details.
        
        Code:
        ```
        {1}
        ```
        
        Summary:
        """;

    public async Task<SmartContentResult> GenerateSummariesAsync(
        string? workspacePath,
        int batchSize,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var processed = 0;
        var failed = 0;
        var errors = new List<string>();

        // Get nodes without smart summaries
        var pendingNodes = await _dbContext.CodeNodes
            .Where(n => n.SmartSummary == null)
            .Where(n => workspacePath == null || n.WorkspacePath == workspacePath)
            .Where(n => n.NodeType == CodeNodeType.Method || 
                       n.NodeType == CodeNodeType.Class ||
                       n.NodeType == CodeNodeType.Interface)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var node in pendingNodes)
        {
            try
            {
                // Get code content from related RagChunk or file
                var code = await GetCodeContentAsync(node, ct);
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                // Generate summary
                var prompt = string.Format(SummaryPrompt, node.NodeType.ToString().ToLower(), code);
                var response = await _llmProvider.GenerateAsync(
                    _options.Model,
                    prompt,
                    temperature: 0.3,
                    ct);

                var summary = response.Content.Trim();

                // Generate embedding for summary
                var embedding = await _embeddingProvider.GenerateEmbeddingAsync(
                    _options.EmbeddingModel,
                    summary,
                    ct);

                // Update node
                node.SmartSummary = summary;
                node.SmartSummaryEmbedding = new Vector(embedding);
                node.SmartSummaryGeneratedAt = DateTimeOffset.UtcNow;

                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{node.FullName}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to generate summary for {NodeName}", node.FullName);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return new SmartContentResult(processed, failed, stopwatch.Elapsed, errors);
    }
}
```

## Background Service: SmartContentGenerator

**File:** `src/Aura.Foundation/Rag/SmartContentGenerator.cs`

```csharp
public sealed class SmartContentGenerator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmartContentGenerator> _logger;
    private readonly SmartContentOptions _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for initial indexing to complete
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISmartContentService>();

            var pending = await service.GetPendingCountAsync(ct: stoppingToken);
            if (pending > 0)
            {
                _logger.LogInformation("Generating smart summaries for {Count} pending nodes", pending);
                var result = await service.GenerateSummariesAsync(
                    batchSize: _options.BatchSize,
                    cancellationToken: stoppingToken);
                    
                _logger.LogInformation(
                    "Generated {Processed} summaries, {Failed} failed in {Duration:g}",
                    result.Processed, result.Failed, result.Duration);
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }
}
```

## Configuration

**File:** `src/Aura.Foundation/Rag/SmartContentOptions.cs`

```csharp
public sealed class SmartContentOptions
{
    public const string Section = "SmartContent";
    
    /// <summary>Model to use for summary generation (default: phi3:mini).</summary>
    public string Model { get; set; } = "phi3:mini";
    
    /// <summary>Model to use for embedding summaries.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    
    /// <summary>Batch size for processing.</summary>
    public int BatchSize { get; set; } = 50;
    
    /// <summary>Interval between processing batches.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>Whether smart content generation is enabled.</summary>
    public bool Enabled { get; set; } = true;
}
```

## Search Updates

### Dual-Vector Search in RagService

**File:** `src/Aura.Foundation/Rag/RagService.cs`

Add method:
```csharp
public async Task<IReadOnlyList<RagResult>> SearchByIntentAsync(
    string query,
    int maxResults = 10,
    float minSimilarity = 0.5f,
    CancellationToken ct = default)
{
    var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(
        _options.EmbeddingModel, query, ct);
    var vector = new Vector(queryEmbedding);

    // Search by smart summary embedding
    return await _dbContext.RagChunks
        .Where(c => c.SmartSummaryEmbedding != null)
        .OrderBy(c => c.SmartSummaryEmbedding!.CosineDistance(vector))
        .Take(maxResults)
        .Select(c => new RagResult(
            c.ContentId,
            c.Content,
            c.SmartSummary,
            1 - c.SmartSummaryEmbedding!.CosineDistance(vector)))
        .ToListAsync(ct);
}
```

## API Endpoints

**File:** `src/Aura.Api/Controllers/IndexController.cs`

```csharp
[HttpPost("smart-content/generate")]
public async Task<ActionResult<SmartContentResult>> GenerateSmartContent(
    [FromQuery] string? workspacePath,
    [FromQuery] int batchSize = 50,
    CancellationToken ct = default)
{
    var result = await _smartContentService.GenerateSummariesAsync(workspacePath, batchSize, ct);
    return Ok(result);
}

[HttpGet("smart-content/pending")]
public async Task<ActionResult<int>> GetPendingCount(
    [FromQuery] string? workspacePath,
    CancellationToken ct = default)
{
    var count = await _smartContentService.GetPendingCountAsync(workspacePath, ct);
    return Ok(count);
}
```

## Testing

### Unit Tests
- `SmartContentServiceTests.cs` - Mock LLM responses, verify storage
- `SmartContentGeneratorTests.cs` - Verify background processing

### Integration Tests
- Generate summaries for sample code files
- Verify dual-vector search returns intent-based results

## Rollout Plan

1. **Phase 1**: Add database columns and entities (migration)
2. **Phase 2**: Implement `SmartContentService` with manual trigger
3. **Phase 3**: Add background `SmartContentGenerator`
4. **Phase 4**: Update search to include intent-based results
5. **Phase 5**: Add API endpoints and VS Code integration

## Dependencies
- Ollama with `phi3:mini` or similar small/fast model
- Existing `ILlmProvider` and `IEmbeddingProvider`

## Estimated Effort
- **Low complexity**, **Medium effort** due to:
  - Async processing at scale
  - LLM rate limiting / batching
  - Progress tracking

## Success Criteria
- [ ] Every indexed method has a smart summary within 24 hours
- [ ] Intent search for "user authentication" finds auth-related code
- [ ] Background generator runs without blocking indexing
- [ ] Summary generation is idempotent (re-runs skip existing)
