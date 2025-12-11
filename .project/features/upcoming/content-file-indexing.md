# Task: Content File Indexing

## Overview
Treat non-code files (Markdown, config, scripts) as first-class citizens in the code graph with their own node types and embeddings.

## Parent Spec
`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 2

## Goals
1. Add `Content` and `Section` node types for documents
2. Index markdown, YAML, JSON, shell scripts, etc.
3. Create edges when content files reference code symbols
4. Enable queries like "find all docs that mention WorkflowService"

## Data Model Changes

### 1. Extend CodeNodeType Enum

**File:** `src/Aura.Foundation/Data/Entities/CodeNode.cs`

```csharp
public enum CodeNodeType
{
    // Existing...
    Solution,
    Project,
    File,
    Namespace,
    Class,
    Interface,
    Record,
    Struct,
    Enum,
    Method,
    Property,
    Field,
    Event,
    Constructor,
    
    // New
    /// <summary>A content file (markdown, config, script, etc.)</summary>
    Content = 20,
    
    /// <summary>A section within a document (e.g., markdown header).</summary>
    Section = 21,
    
    /// <summary>A configuration block or key.</summary>
    ConfigBlock = 22,
}
```

### 2. Extend CodeEdgeType Enum

**File:** `src/Aura.Foundation/Data/Entities/CodeEdge.cs`

```csharp
public enum CodeEdgeType
{
    // Existing...
    Contains,
    Declares,
    Inherits,
    Implements,
    References,
    Calls,
    Uses,
    Overrides,
    
    // New
    /// <summary>Document/content references or mentions code element.</summary>
    Documents = 20,
    
    /// <summary>Content file is located in same directory as code.</summary>
    CoLocated = 21,
}
```

## Content Detection

### Content File Patterns

**File:** `src/Aura.Foundation/Rag/ContentFilePatterns.cs`

```csharp
public static class ContentFilePatterns
{
    public static readonly IReadOnlyDictionary<string, string> ExtensionToLanguage = new Dictionary<string, string>
    {
        // Documentation
        [".md"] = "markdown",
        [".mdx"] = "markdown",
        [".rst"] = "restructuredtext",
        [".adoc"] = "asciidoc",
        [".txt"] = "text",
        
        // Configuration
        [".json"] = "json",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "toml",
        [".ini"] = "ini",
        [".env"] = "dotenv",
        [".config"] = "xml",
        
        // Scripts
        [".sh"] = "shell",
        [".bash"] = "shell",
        [".ps1"] = "powershell",
        [".bat"] = "batch",
        [".cmd"] = "batch",
        
        // Build/CI
        [".dockerfile"] = "dockerfile",
        ["Dockerfile"] = "dockerfile",
        [".bicep"] = "bicep",
        [".tf"] = "terraform",
    };
    
    public static bool IsContentFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileName(filePath);
        return ExtensionToLanguage.ContainsKey(ext) || ExtensionToLanguage.ContainsKey(name);
    }
}
```

## Content Ingestor Updates

### 1. Enhanced MarkdownIngestor

**File:** `src/Aura.Foundation/Rag/Ingestors/MarkdownIngestor.cs`

```csharp
public sealed class MarkdownIngestor : IContentIngestor
{
    public bool CanIngest(string filePath) => 
        Path.GetExtension(filePath).ToLowerInvariant() is ".md" or ".mdx";

    public async Task<ContentIngestionResult> IngestAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var result = new ContentIngestionResult
        {
            FilePath = filePath,
            ContentType = "markdown",
        };

        // Parse into sections based on headers
        var sections = ParseMarkdownSections(content);
        
        foreach (var section in sections)
        {
            result.Chunks.Add(new ContentChunk
            {
                Text = section.Content,
                ChunkType = ChunkTypes.Section,
                SymbolName = section.Header,
                StartLine = section.StartLine,
                EndLine = section.EndLine,
                Metadata = new Dictionary<string, string>
                {
                    ["headerLevel"] = section.Level.ToString(),
                },
            });
        }

        // Extract code symbol references
        result.SymbolReferences = ExtractSymbolReferences(content);
        
        return result;
    }

    private List<string> ExtractSymbolReferences(string content)
    {
        var references = new List<string>();
        
        // Match backtick code references: `ClassName`, `MethodName`
        var backtickPattern = @"`([A-Z][a-zA-Z0-9_]+)`";
        foreach (Match match in Regex.Matches(content, backtickPattern))
        {
            references.Add(match.Groups[1].Value);
        }
        
        // Match PascalCase words that look like type names
        var pascalPattern = @"\b([A-Z][a-z]+(?:[A-Z][a-z]+)+)\b";
        foreach (Match match in Regex.Matches(content, pascalPattern))
        {
            var word = match.Groups[1].Value;
            if (!IsCommonWord(word))
            {
                references.Add(word);
            }
        }
        
        return references.Distinct().ToList();
    }
}
```

### 2. New ConfigIngestor

**File:** `src/Aura.Foundation/Rag/Ingestors/ConfigIngestor.cs`

```csharp
public sealed class ConfigIngestor : IContentIngestor
{
    private static readonly HashSet<string> Extensions = [".json", ".yaml", ".yml", ".toml"];

    public bool CanIngest(string filePath) =>
        Extensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());

    public async Task<ContentIngestionResult> IngestAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        var result = new ContentIngestionResult
        {
            FilePath = filePath,
            ContentType = ext.TrimStart('.'),
        };

        // For config files, treat the whole file as one chunk
        // but extract key paths for searchability
        result.Chunks.Add(new ContentChunk
        {
            Text = content,
            ChunkType = ChunkTypes.File,
            SymbolName = Path.GetFileName(filePath),
            StartLine = 1,
            EndLine = content.Count(c => c == '\n') + 1,
        });

        // Extract top-level keys as metadata
        if (ext is ".json")
        {
            result.KeyPaths = ExtractJsonKeys(content);
        }
        else if (ext is ".yaml" or ".yml")
        {
            result.KeyPaths = ExtractYamlKeys(content);
        }

        return result;
    }
}
```

### 3. New ScriptIngestor

**File:** `src/Aura.Foundation/Rag/Ingestors/ScriptIngestor.cs`

```csharp
public sealed class ScriptIngestor : IContentIngestor
{
    private static readonly HashSet<string> Extensions = [".sh", ".bash", ".ps1", ".bat", ".cmd"];

    public bool CanIngest(string filePath) =>
        Extensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());

    public async Task<ContentIngestionResult> IngestAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        var result = new ContentIngestionResult
        {
            FilePath = filePath,
            ContentType = ext == ".ps1" ? "powershell" : "shell",
        };

        // Extract functions/commands as separate chunks
        var functions = ext == ".ps1" 
            ? ExtractPowerShellFunctions(content)
            : ExtractShellFunctions(content);

        foreach (var func in functions)
        {
            result.Chunks.Add(new ContentChunk
            {
                Text = func.Body,
                ChunkType = ChunkTypes.Function,
                SymbolName = func.Name,
                StartLine = func.StartLine,
                EndLine = func.EndLine,
            });
        }

        // If no functions, treat whole file as one chunk
        if (result.Chunks.Count == 0)
        {
            result.Chunks.Add(new ContentChunk
            {
                Text = content,
                ChunkType = ChunkTypes.File,
                SymbolName = Path.GetFileName(filePath),
                StartLine = 1,
                EndLine = content.Count(c => c == '\n') + 1,
            });
        }

        return result;
    }
}
```

## Graph Integration

### ContentGraphBuilder

**File:** `src/Aura.Foundation/Rag/ContentGraphBuilder.cs`

```csharp
public sealed class ContentGraphBuilder
{
    private readonly AuraDbContext _dbContext;
    private readonly ICodeGraphService _graphService;

    public async Task BuildContentGraphAsync(
        ContentIngestionResult result,
        string workspacePath,
        CancellationToken ct = default)
    {
        // Create file node
        var fileNode = new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = CodeNodeType.Content,
            Name = Path.GetFileName(result.FilePath),
            FullName = result.FilePath,
            FilePath = result.FilePath,
            WorkspacePath = workspacePath,
            PropertiesJson = JsonSerializer.Serialize(new
            {
                contentType = result.ContentType,
                keyPaths = result.KeyPaths,
            }),
        };
        await _graphService.AddNodeAsync(fileNode, ct);

        // Create section nodes and contain edges
        foreach (var chunk in result.Chunks.Where(c => c.ChunkType == ChunkTypes.Section))
        {
            var sectionNode = new CodeNode
            {
                Id = Guid.NewGuid(),
                NodeType = CodeNodeType.Section,
                Name = chunk.SymbolName ?? "Untitled",
                FilePath = result.FilePath,
                LineNumber = chunk.StartLine,
                WorkspacePath = workspacePath,
            };
            await _graphService.AddNodeAsync(sectionNode, ct);
            
            await _graphService.AddEdgeAsync(new CodeEdge
            {
                Id = Guid.NewGuid(),
                EdgeType = CodeEdgeType.Contains,
                SourceId = fileNode.Id,
                TargetId = sectionNode.Id,
            }, ct);
        }

        // Create "Documents" edges to referenced code symbols
        foreach (var symbolRef in result.SymbolReferences)
        {
            var targets = await _graphService.FindNodesAsync(symbolRef, workspacePath: workspacePath, ct: ct);
            foreach (var target in targets)
            {
                await _graphService.AddEdgeAsync(new CodeEdge
                {
                    Id = Guid.NewGuid(),
                    EdgeType = CodeEdgeType.Documents,
                    SourceId = fileNode.Id,
                    TargetId = target.Id,
                }, ct);
            }
        }
    }
}
```

## Query Support

### Find Documentation for Code

**File:** `src/Aura.Foundation/Rag/ICodeGraphService.cs`

Add method:
```csharp
/// <summary>
/// Finds content files (docs, configs) that reference a code element.
/// </summary>
Task<IReadOnlyList<CodeNode>> FindDocumentationAsync(
    string symbolName,
    string? workspacePath = null,
    CancellationToken cancellationToken = default);
```

Implementation:
```csharp
public async Task<IReadOnlyList<CodeNode>> FindDocumentationAsync(
    string symbolName,
    string? workspacePath,
    CancellationToken ct)
{
    var codeNodes = await FindNodesAsync(symbolName, workspacePath: workspacePath, ct: ct);
    if (codeNodes.Count == 0) return [];

    var codeNodeIds = codeNodes.Select(n => n.Id).ToList();

    var query = from doc in _dbContext.CodeNodes
                join edge in _dbContext.CodeEdges on doc.Id equals edge.SourceId
                where edge.EdgeType == CodeEdgeType.Documents
                      && codeNodeIds.Contains(edge.TargetId)
                      && doc.NodeType == CodeNodeType.Content
                select doc;

    if (!string.IsNullOrEmpty(workspacePath))
    {
        query = query.Where(n => n.WorkspacePath == workspacePath);
    }

    return await query.Distinct().ToListAsync(ct);
}
```

## Semantic Indexer Integration

### Update SemanticIndexer to Include Content Files

**File:** `src/Aura.Foundation/Rag/SemanticIndexer.cs`

Modify `IndexDirectoryAsync` to:
1. Detect content files using `ContentFilePatterns`
2. Use appropriate ingestor
3. Create graph nodes via `ContentGraphBuilder`

```csharp
public async Task<SemanticIndexResult> IndexDirectoryAsync(
    string directoryPath,
    SemanticIndexOptions? options,
    CancellationToken ct)
{
    // ... existing code discovery logic ...

    // Also index content files
    var contentFiles = Directory.EnumerateFiles(directoryPath, "*.*", 
        options?.Recursive == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
        .Where(f => ContentFilePatterns.IsContentFile(f))
        .Where(f => !IsExcluded(f, options?.ExcludePatterns));

    foreach (var contentFile in contentFiles)
    {
        var ingestor = _ingestorRegistry.GetIngestor(contentFile);
        if (ingestor != null)
        {
            var result = await ingestor.IngestAsync(contentFile, ct);
            await _contentGraphBuilder.BuildContentGraphAsync(result, directoryPath, ct);
            filesIndexed++;
            filesByLanguage[result.ContentType] = filesByLanguage.GetValueOrDefault(result.ContentType) + 1;
        }
    }
}
```

## API Endpoints

**File:** `src/Aura.Api/Controllers/GraphController.cs`

```csharp
[HttpGet("documentation/{symbolName}")]
public async Task<ActionResult<IReadOnlyList<CodeNode>>> FindDocumentation(
    string symbolName,
    [FromQuery] string? workspacePath,
    CancellationToken ct)
{
    var docs = await _graphService.FindDocumentationAsync(symbolName, workspacePath, ct);
    return Ok(docs);
}

[HttpGet("content")]
public async Task<ActionResult<IReadOnlyList<CodeNode>>> ListContentFiles(
    [FromQuery] string? workspacePath,
    [FromQuery] string? contentType,
    CancellationToken ct)
{
    var query = _dbContext.CodeNodes
        .Where(n => n.NodeType == CodeNodeType.Content);
    
    if (!string.IsNullOrEmpty(workspacePath))
        query = query.Where(n => n.WorkspacePath == workspacePath);
    
    if (!string.IsNullOrEmpty(contentType))
        query = query.Where(n => n.PropertiesJson!.Contains($"\"contentType\":\"{contentType}\""));

    return Ok(await query.ToListAsync(ct));
}
```

## Testing

### Unit Tests
- `MarkdownIngestorTests.cs` - Section parsing, symbol extraction
- `ConfigIngestorTests.cs` - JSON/YAML key extraction
- `ContentGraphBuilderTests.cs` - Edge creation

### Integration Tests
- Index a directory with mixed code and content files
- Query for documentation referencing a class
- Verify section nodes are created for markdown headers

## Rollout Plan

1. **Phase 1**: Add enum values and migrations
2. **Phase 2**: Implement new ingestors
3. **Phase 3**: Implement `ContentGraphBuilder`
4. **Phase 4**: Update `SemanticIndexer` to include content
5. **Phase 5**: Add query methods and API endpoints

## Dependencies
- Existing `IContentIngestor` interface
- `ICodeGraphService` for node/edge creation

## Estimated Effort
- **Low complexity**, **Low effort**
- Mostly extending existing patterns

## Success Criteria
- [ ] README.md appears as `Content` node in graph
- [ ] "find docs mentioning WorkflowService" returns relevant markdown files
- [ ] Markdown sections are separate nodes linked by `Contains` edges
- [ ] Config files are indexed with key paths searchable
