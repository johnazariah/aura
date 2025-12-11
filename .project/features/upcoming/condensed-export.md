# Task: Condensed Export Format

## Overview

Export code graph to portable JSONL format for sharing, backup, or offline use.

## Parent Spec

`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 6

## Goals

1. Export graph to portable JSONL format
2. Import graph from JSONL
3. Enable sharing indexed repositories
4. Support offline graph queries

## Export Format

### JSONL Structure

Each line is a self-contained JSON object with a `type` discriminator:

```jsonl
{"type":"meta","version":"1.0","workspace":"aura","workspacePath":"/path/to/aura","exportedAt":"2025-12-03T10:00:00Z","stats":{"nodes":1234,"edges":5678}}
{"type":"node","id":"550e8400-e29b-41d4-a716-446655440000","nodeType":"Class","name":"WorkflowService","fullName":"Aura.Foundation.Services.WorkflowService","filePath":"src/Aura.Foundation/Services/WorkflowService.cs","lineNumber":15,"signature":null,"modifiers":"public sealed","smartSummary":"Orchestrates workflow execution with human-in-the-loop approval.","embedding":[0.1,0.2,...]}
{"type":"node","id":"550e8400-e29b-41d4-a716-446655440001","nodeType":"Method","name":"ExecuteAsync",...}
{"type":"edge","id":"...","edgeType":"Contains","sourceId":"550e8400-e29b-41d4-a716-446655440000","targetId":"550e8400-e29b-41d4-a716-446655440001"}
{"type":"edge","id":"...","edgeType":"Calls","sourceId":"...","targetId":"..."}
```

### Format Benefits

- **Streaming**: Can process line-by-line without loading entire file
- **Appendable**: Can add new records without rewriting
- **Human-readable**: Each line is valid JSON
- **Compressible**: Gzip/zstd works well on repeated patterns

## Data Models

### Export Models

**File:** `src/Aura.Foundation/Rag/Export/ExportModels.cs`

```csharp
namespace Aura.Foundation.Rag.Export;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ExportMeta), "meta")]
[JsonDerivedType(typeof(ExportNode), "node")]
[JsonDerivedType(typeof(ExportEdge), "edge")]
public abstract record ExportRecord;

public record ExportMeta : ExportRecord
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("workspace")]
    public required string Workspace { get; init; }

    [JsonPropertyName("workspacePath")]
    public required string WorkspacePath { get; init; }

    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("stats")]
    public required ExportStats Stats { get; init; }
}

public record ExportStats
{
    [JsonPropertyName("nodes")]
    public int Nodes { get; init; }

    [JsonPropertyName("edges")]
    public int Edges { get; init; }

    [JsonPropertyName("files")]
    public int Files { get; init; }

    [JsonPropertyName("languages")]
    public Dictionary<string, int> Languages { get; init; } = new();
}

public record ExportNode : ExportRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("nodeType")]
    public required string NodeType { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("lineNumber")]
    public int? LineNumber { get; init; }

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    [JsonPropertyName("modifiers")]
    public string? Modifiers { get; init; }

    [JsonPropertyName("smartSummary")]
    public string? SmartSummary { get; init; }

    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; init; }

    [JsonPropertyName("smartSummaryEmbedding")]
    public float[]? SmartSummaryEmbedding { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; init; }
}

public record ExportEdge : ExportRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("edgeType")]
    public required string EdgeType { get; init; }

    [JsonPropertyName("sourceId")]
    public Guid SourceId { get; init; }

    [JsonPropertyName("targetId")]
    public Guid TargetId { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; init; }
}
```

## Export Service

### IGraphExportService

**File:** `src/Aura.Foundation/Rag/Export/IGraphExportService.cs`

```csharp
public interface IGraphExportService
{
    /// <summary>
    /// Exports a workspace's graph to JSONL format.
    /// </summary>
    Task ExportAsync(
        string workspacePath,
        Stream outputStream,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports to a file.
    /// </summary>
    Task ExportToFileAsync(
        string workspacePath,
        string outputPath,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a graph from JSONL format.
    /// </summary>
    Task<ImportResult> ImportAsync(
        Stream inputStream,
        ImportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports from a file.
    /// </summary>
    Task<ImportResult> ImportFromFileAsync(
        string inputPath,
        ImportOptions? options = null,
        CancellationToken cancellationToken = default);
}

public record ExportOptions
{
    /// <summary>Include embeddings (can significantly increase file size).</summary>
    public bool IncludeEmbeddings { get; init; } = true;

    /// <summary>Include smart summaries.</summary>
    public bool IncludeSmartSummaries { get; init; } = true;

    /// <summary>Compress output with gzip.</summary>
    public bool Compress { get; init; } = false;

    /// <summary>Filter to specific node types.</summary>
    public HashSet<CodeNodeType>? NodeTypes { get; init; }
}

public record ImportOptions
{
    /// <summary>Workspace path to import into (overrides file metadata).</summary>
    public string? TargetWorkspace { get; init; }

    /// <summary>How to handle conflicts.</summary>
    public ImportConflictMode ConflictMode { get; init; } = ImportConflictMode.Skip;

    /// <summary>Whether to import embeddings.</summary>
    public bool ImportEmbeddings { get; init; } = true;
}

public enum ImportConflictMode
{
    Skip,       // Skip existing nodes
    Replace,    // Replace existing nodes
    Error,      // Throw on conflict
}

public record ImportResult
{
    public int NodesImported { get; init; }
    public int EdgesImported { get; init; }
    public int NodesSkipped { get; init; }
    public int EdgesSkipped { get; init; }
    public TimeSpan Duration { get; init; }
    public List<string> Warnings { get; init; } = new();
}
```

## Export Implementation

**File:** `src/Aura.Foundation/Rag/Export/GraphExportService.cs`

```csharp
public sealed class GraphExportService : IGraphExportService
{
    private readonly AuraDbContext _dbContext;
    private readonly ILogger<GraphExportService> _logger;

    public async Task ExportAsync(
        string workspacePath,
        Stream outputStream,
        ExportOptions? options,
        CancellationToken ct)
    {
        options ??= new ExportOptions();
        
        await using var writer = options.Compress
            ? new StreamWriter(new GZipStream(outputStream, CompressionLevel.Optimal))
            : new StreamWriter(outputStream);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // Count totals
        var nodeCount = await _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath)
            .CountAsync(ct);
        var edgeCount = await _dbContext.CodeEdges
            .Where(e => _dbContext.CodeNodes.Any(n => 
                n.Id == e.SourceId && n.WorkspacePath == workspacePath))
            .CountAsync(ct);

        // Write metadata
        var meta = new ExportMeta
        {
            Workspace = Path.GetFileName(workspacePath),
            WorkspacePath = workspacePath,
            Stats = new ExportStats
            {
                Nodes = nodeCount,
                Edges = edgeCount,
            },
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize<ExportRecord>(meta, jsonOptions));

        // Stream nodes
        var nodeQuery = _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath)
            .AsNoTracking()
            .AsAsyncEnumerable();

        if (options.NodeTypes?.Any() == true)
        {
            nodeQuery = _dbContext.CodeNodes
                .Where(n => n.WorkspacePath == workspacePath)
                .Where(n => options.NodeTypes.Contains(n.NodeType))
                .AsNoTracking()
                .AsAsyncEnumerable();
        }

        await foreach (var node in nodeQuery.WithCancellation(ct))
        {
            var exportNode = new ExportNode
            {
                Id = node.Id,
                NodeType = node.NodeType.ToString(),
                Name = node.Name,
                FullName = node.FullName,
                FilePath = node.FilePath,
                LineNumber = node.LineNumber,
                Signature = node.Signature,
                Modifiers = node.Modifiers,
                SmartSummary = options.IncludeSmartSummaries ? node.SmartSummary : null,
                Embedding = options.IncludeEmbeddings ? node.Embedding?.ToArray() : null,
                SmartSummaryEmbedding = options.IncludeEmbeddings 
                    ? node.SmartSummaryEmbedding?.ToArray() : null,
                Properties = !string.IsNullOrEmpty(node.PropertiesJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(node.PropertiesJson)
                    : null,
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize<ExportRecord>(exportNode, jsonOptions));
        }

        // Stream edges
        var nodeIds = await _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var edges = _dbContext.CodeEdges
            .Where(e => nodeIds.Contains(e.SourceId))
            .AsNoTracking()
            .AsAsyncEnumerable();

        await foreach (var edge in edges.WithCancellation(ct))
        {
            var exportEdge = new ExportEdge
            {
                Id = edge.Id,
                EdgeType = edge.EdgeType.ToString(),
                SourceId = edge.SourceId,
                TargetId = edge.TargetId,
                Properties = !string.IsNullOrEmpty(edge.PropertiesJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(edge.PropertiesJson)
                    : null,
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize<ExportRecord>(exportEdge, jsonOptions));
        }

        await writer.FlushAsync(ct);
        _logger.LogInformation(
            "Exported {NodeCount} nodes and {EdgeCount} edges for workspace {Workspace}",
            nodeCount, edgeCount, workspacePath);
    }

    public async Task ExportToFileAsync(
        string workspacePath,
        string outputPath,
        ExportOptions? options,
        CancellationToken ct)
    {
        await using var fileStream = File.Create(outputPath);
        await ExportAsync(workspacePath, fileStream, options, ct);
    }
}
```

## Import Implementation

```csharp
public async Task<ImportResult> ImportAsync(
    Stream inputStream,
    ImportOptions? options,
    CancellationToken ct)
{
    options ??= new ImportOptions();
    var stopwatch = Stopwatch.StartNew();
    var result = new ImportResult();
    var warnings = new List<string>();

    using var reader = new StreamReader(inputStream);
    var jsonOptions = new JsonSerializerOptions();
    
    ExportMeta? meta = null;
    var nodeIdMap = new Dictionary<Guid, Guid>(); // Old ID -> New ID
    var nodesToAdd = new List<CodeNode>();
    var edgesToAdd = new List<CodeEdge>();

    while (await reader.ReadLineAsync(ct) is { } line)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        var record = JsonSerializer.Deserialize<ExportRecord>(line, jsonOptions);
        
        switch (record)
        {
            case ExportMeta m:
                meta = m;
                break;

            case ExportNode n:
                var workspacePath = options.TargetWorkspace ?? meta?.WorkspacePath ?? "";
                
                // Check for existing
                var existingNode = await _dbContext.CodeNodes
                    .FirstOrDefaultAsync(x => 
                        x.WorkspacePath == workspacePath && 
                        x.FullName == n.FullName, ct);

                if (existingNode != null)
                {
                    if (options.ConflictMode == ImportConflictMode.Skip)
                    {
                        nodeIdMap[n.Id] = existingNode.Id;
                        result = result with { NodesSkipped = result.NodesSkipped + 1 };
                        continue;
                    }
                    else if (options.ConflictMode == ImportConflictMode.Error)
                    {
                        throw new InvalidOperationException(
                            $"Node already exists: {n.FullName}");
                    }
                    // Replace: remove existing
                    _dbContext.CodeNodes.Remove(existingNode);
                }

                var newNode = new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = Enum.Parse<CodeNodeType>(n.NodeType),
                    Name = n.Name,
                    FullName = n.FullName,
                    FilePath = n.FilePath,
                    LineNumber = n.LineNumber,
                    Signature = n.Signature,
                    Modifiers = n.Modifiers,
                    WorkspacePath = workspacePath,
                    SmartSummary = n.SmartSummary,
                    Embedding = options.ImportEmbeddings && n.Embedding != null 
                        ? new Pgvector.Vector(n.Embedding) : null,
                    SmartSummaryEmbedding = options.ImportEmbeddings && n.SmartSummaryEmbedding != null
                        ? new Pgvector.Vector(n.SmartSummaryEmbedding) : null,
                    PropertiesJson = n.Properties != null 
                        ? JsonSerializer.Serialize(n.Properties) : null,
                };

                nodeIdMap[n.Id] = newNode.Id;
                nodesToAdd.Add(newNode);
                break;

            case ExportEdge e:
                // Remap source and target IDs
                if (!nodeIdMap.TryGetValue(e.SourceId, out var newSourceId) ||
                    !nodeIdMap.TryGetValue(e.TargetId, out var newTargetId))
                {
                    warnings.Add($"Edge references unknown node: {e.SourceId} -> {e.TargetId}");
                    result = result with { EdgesSkipped = result.EdgesSkipped + 1 };
                    continue;
                }

                edgesToAdd.Add(new CodeEdge
                {
                    Id = Guid.NewGuid(),
                    EdgeType = Enum.Parse<CodeEdgeType>(e.EdgeType),
                    SourceId = newSourceId,
                    TargetId = newTargetId,
                    PropertiesJson = e.Properties != null 
                        ? JsonSerializer.Serialize(e.Properties) : null,
                });
                break;
        }

        // Batch insert every 1000 records
        if (nodesToAdd.Count >= 1000)
        {
            _dbContext.CodeNodes.AddRange(nodesToAdd);
            await _dbContext.SaveChangesAsync(ct);
            result = result with { NodesImported = result.NodesImported + nodesToAdd.Count };
            nodesToAdd.Clear();
        }

        if (edgesToAdd.Count >= 1000)
        {
            _dbContext.CodeEdges.AddRange(edgesToAdd);
            await _dbContext.SaveChangesAsync(ct);
            result = result with { EdgesImported = result.EdgesImported + edgesToAdd.Count };
            edgesToAdd.Clear();
        }
    }

    // Insert remaining
    if (nodesToAdd.Any())
    {
        _dbContext.CodeNodes.AddRange(nodesToAdd);
        result = result with { NodesImported = result.NodesImported + nodesToAdd.Count };
    }

    if (edgesToAdd.Any())
    {
        _dbContext.CodeEdges.AddRange(edgesToAdd);
        result = result with { EdgesImported = result.EdgesImported + edgesToAdd.Count };
    }

    await _dbContext.SaveChangesAsync(ct);

    return result with
    {
        Duration = stopwatch.Elapsed,
        Warnings = warnings,
    };
}
```

## API Endpoints

**File:** `src/Aura.Api/Controllers/ExportController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IGraphExportService _exportService;

    [HttpGet("{workspacePath}")]
    [Produces("application/x-ndjson")]
    public async Task Export(
        string workspacePath,
        [FromQuery] bool includeEmbeddings = true,
        [FromQuery] bool compress = false,
        CancellationToken ct = default)
    {
        var decodedPath = Uri.UnescapeDataString(workspacePath);
        
        Response.ContentType = compress 
            ? "application/gzip" 
            : "application/x-ndjson";
        Response.Headers.ContentDisposition = 
            $"attachment; filename=\"{Path.GetFileName(decodedPath)}.jsonl\"";

        await _exportService.ExportAsync(
            decodedPath,
            Response.Body,
            new ExportOptions
            {
                IncludeEmbeddings = includeEmbeddings,
                Compress = compress,
            },
            ct);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> Import(
        IFormFile file,
        [FromQuery] string? targetWorkspace,
        CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        var result = await _exportService.ImportAsync(
            stream,
            new ImportOptions { TargetWorkspace = targetWorkspace },
            ct);

        return Ok(result);
    }
}
```

## CLI Commands

Add to a future CLI tool:

```bash
# Export
aura export /path/to/workspace -o graph.jsonl
aura export /path/to/workspace -o graph.jsonl.gz --compress

# Import
aura import graph.jsonl
aura import graph.jsonl --target-workspace /path/to/other
```

## Testing

### Unit Tests

- `GraphExportServiceTests.cs` - Export/import roundtrip
- Verify ID remapping on import
- Test conflict modes

### Integration Tests

- Export workspace, import to new workspace
- Verify node and edge counts match
- Test streaming for large graphs

## Rollout Plan

1. **Phase 1**: Define export models
2. **Phase 2**: Implement export service
3. **Phase 3**: Implement import service
4. **Phase 4**: Add API endpoints
5. **Phase 5**: Add CLI commands (future)

## Dependencies

- Existing `AuraDbContext`
- `System.IO.Compression` for gzip

## Estimated Effort

- **Low complexity**, **Low effort**
- Standard serialization patterns

## Success Criteria

- [ ] Export creates valid JSONL file
- [ ] Import creates nodes/edges in database
- [ ] Round-trip preserves all data (except IDs)
- [ ] Large graphs (100k+ nodes) export/import in reasonable time
- [ ] Compressed export reduces file size by 5-10x
