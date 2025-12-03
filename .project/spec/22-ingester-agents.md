# Ingester Agents

## Overview

Ingesters are agents that parse files and produce semantic chunks for indexing. They use the standard agent infrastructure with a special capability pattern: `ingest:{extension}`.

## Capability Pattern

Ingester agents declare which file types they can handle:

```markdown
## Capabilities

- ingest:cs
- ingest:csx
```

The BackgroundIndexer queries the agent registry to find the best ingester:

```csharp
var extension = Path.GetExtension(filePath).TrimStart('.'); // "cs"
var agent = registry.GetBestForCapability($"ingest:{extension}");
```

## Agent Contract

### Input

Ingester agents receive file content via `AgentContext`:

```csharp
var context = new AgentContext(
    Prompt: "Parse this file and extract semantic chunks",
    Properties: new Dictionary<string, object>
    {
        ["filePath"] = "/path/to/file.cs",
        ["content"] = "namespace Foo { public class Bar { } }",
        ["language"] = "csharp"
    });
```

### Output

Ingester agents return chunks as JSON in `AgentOutput.Artifacts`:

```csharp
return AgentOutput.WithArtifacts(
    content: "Extracted 3 chunks from File.cs",
    artifacts: new Dictionary<string, string>
    {
        ["chunks"] = JsonSerializer.Serialize(chunks)
    });
```

Chunk format:
```json
[
  {
    "text": "public class Bar { }",
    "filePath": "/path/to/file.cs",
    "chunkType": "class",
    "symbolName": "Bar",
    "parentSymbol": null,
    "fullyQualifiedName": "Foo.Bar",
    "startLine": 1,
    "endLine": 1,
    "language": "csharp",
    "metadata": {
      "accessibility": "public",
      "hasXmlDoc": "false"
    }
  }
]
```

## Priority-Based Fallback

Agents are selected by priority (lower = more specialized, selected first):

| Priority | Agent | Description |
|----------|-------|-------------|
| 10 | `csharp-ingester` | Roslyn-based, full semantic analysis |
| 10 | `python-ingester` | TreeSitter-based, AST parsing |
| 20 | `llm-code-ingester` | LLM parses code structure |
| 50 | `text-ingester` | Text/markdown chunking |
| 99 | `fallback-ingester` | Returns whole file as one chunk |

When no specialized ingester exists, the system falls back gracefully:

```
Request: ingest:lisp
→ No agent with "ingest:lisp" capability
→ Try "ingest:*" (generic code ingester)
→ Try "ingest:text" (text fallback)
→ Use fallback-ingester (apologetic)
```

## Markdown Ingester Example

```markdown
# Generic Code Ingester

Parses code files using LLM understanding of language structure.

## Metadata

- **Priority**: 50
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b
- **Temperature**: 0.1

## Capabilities

- ingest:*

## System Prompt

You are a code parser. Given source code, identify and extract semantic chunks.

For each chunk, output a JSON object with:
- text: The code content
- chunkType: "class", "function", "method", "interface", "type", etc.
- symbolName: The name of the symbol
- startLine/endLine: Line numbers

Output format: JSON array of chunks.

Focus on top-level declarations. Include docstrings/comments with their symbols.
```

## Hardcoded Ingester Example

```csharp
public class CSharpIngesterAgent : IAgent
{
    public string AgentId => "csharp-ingester";
    
    public AgentMetadata Metadata => new()
    {
        Name = "C# Roslyn Ingester",
        Description = "Parses C# files using Roslyn semantic analysis",
        Priority = 100,
        Capabilities = ["ingest:cs", "ingest:csx"],
        Tags = ["ingester", "roslyn", "csharp"],
    };

    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        var filePath = (string)context.Properties["filePath"];
        var content = (string)context.Properties["content"];
        
        // Use Roslyn to parse
        var chunks = await ParseWithRoslynAsync(content, filePath, ct);
        
        return AgentOutput.WithArtifacts(
            $"Extracted {chunks.Count} chunks",
            new Dictionary<string, string>
            {
                ["chunks"] = JsonSerializer.Serialize(chunks)
            });
    }
}
```

## BackgroundIndexer Integration

```csharp
private async Task<List<SemanticChunk>> IndexFileAsync(
    string filePath, 
    CancellationToken ct)
{
    var extension = Path.GetExtension(filePath).TrimStart('.');
    var content = await File.ReadAllTextAsync(filePath, ct);
    
    // Find best ingester agent
    var agent = _agentRegistry.GetBestForCapability($"ingest:{extension}")
             ?? _agentRegistry.GetBestForCapability("ingest:*")
             ?? _agentRegistry.GetBestForCapability("ingest:text")
             ?? _agentRegistry.GetAgent("fallback-ingester");
    
    if (agent is null)
    {
        _logger.LogWarning("No ingester found for .{Extension}", extension);
        return [CreateWholeFileChunk(filePath, content)];
    }
    
    var context = new AgentContext(
        "Parse this file",
        Properties: new Dictionary<string, object>
        {
            ["filePath"] = filePath,
            ["content"] = content,
            ["language"] = extension
        });
    
    var output = await agent.ExecuteAsync(context, ct);
    
    // Parse chunks from output
    if (output.Artifacts.TryGetValue("chunks", out var chunksJson))
    {
        return JsonSerializer.Deserialize<List<SemanticChunk>>(chunksJson) ?? [];
    }
    
    return [];
}
```

## Registration

### Markdown Ingesters
Placed in `agents/` directory, loaded automatically by `MarkdownAgentLoader`.

### Hardcoded Ingesters
Registered in module's `RegisterAgents` method:

```csharp
public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
{
    // Get dependencies from service provider
    var ingester = new CSharpIngesterAgent(_roslynService, _logger);
    registry.Register(ingester);
}
```

## Chunk Types

Standard chunk types for consistency:

| ChunkType | Description | Examples |
|-----------|-------------|----------|
| `namespace` | Namespace declaration | `namespace Foo.Bar` |
| `class` | Class definition | `public class Foo` |
| `interface` | Interface definition | `public interface IFoo` |
| `struct` | Struct definition | `public struct Point` |
| `record` | Record definition | `public record Person` |
| `enum` | Enum definition | `public enum Status` |
| `method` | Method/function | `public void DoThing()` |
| `property` | Property | `public string Name { get; }` |
| `field` | Field | `private int _count` |
| `function` | Top-level function | `def foo():` (Python) |
| `type` | Type alias | `type Foo = Bar` |
| `section` | Document section | Markdown header |
| `text` | Plain text chunk | Fallback |

## Dynamic Language Support

The power of agent-based ingesters is dynamic extensibility. Teaching the system to ingest a new language is as simple as dropping in an agent file.

### Adding LISP Support (Example)

User drops `lisp-ingester.md` into `agents/`:

```markdown
---
agentId: lisp-ingester
name: LISP Ingester
description: Extracts semantic chunks from LISP files
capabilities:
  - ingest:lisp
  - ingest:lsp
  - ingest:cl
  - ingest:scm
priority: 80
provider: ollama
model: qwen2.5-coder:32b
tags:
  - ingester
  - lisp
---

## System Prompt

You are a LISP code analyzer. Extract semantic chunks from LISP source.

Identify:
- defun (function definitions)
- defmacro (macro definitions)  
- defclass (CLOS class definitions)
- defvar/defparameter (global variables)
- defstruct (structure definitions)
```

**That's it.** No code changes, no recompilation. The system immediately knows how to ingest LISP files.

## Performance Considerations

### Native vs LLM Ingesters

| Parser Type | Files/sec | Cost | Accuracy |
|-------------|-----------|------|----------|
| Roslyn (C#) | ~500 | Free | Perfect |
| TreeSitter | ~1000 | Free | Perfect |
| LLM (local) | ~1 | Free | Good |
| LLM (cloud) | ~5 | $$ | Excellent |
| Text fallback | ~5000 | Free | Poor |

### Caching

Results can be cached based on file hash:
- Same file content → same chunks
- Invalidate on agent update

## Implementation Status

- [x] Update `BackgroundIndexer` to use agent-based ingestion
- [x] Create `fallback-ingester` agent (apologetic)
- [x] Create `text-ingester` agent (blank-line)
- [x] Create `CSharpIngesterAgent` (Roslyn) - port from hve-hack
- [x] Create `generic-code-ingester.md` (LLM-based)
- [ ] Port TreeSitter ingesters from hve-hack
- [x] Add `Capabilities` field to `AgentMetadata` (already existed)
- [x] Add `GetBestForCapability` to `IAgentRegistry` (already existed, added wildcard support)

## Files

| File | Purpose |
|------|---------|
| `agents/text-ingester.md` | Blank-line chunking fallback |
| `agents/generic-code-ingester.md` | LLM-based code parsing |
| `src/Aura.Foundation/Agents/FallbackIngesterAgent.cs` | Last resort (Priority 0) |
| `src/Aura.Foundation/Agents/TextIngesterAgent.cs` | Text/markdown chunking (Priority 30) |
| `src/Aura.Foundation/Rag/ISemanticIndexer.cs` | SemanticChunk + ChunkTypes |
| `src/Aura.Module.Developer/Agents/CSharpIngesterAgent.cs` | Roslyn ingester (Priority 100) |
| `src/Aura.Module.Developer/Agents/DeveloperAgentProvider.cs` | Registers C# ingester |

## See Also

- [Spec 23: Hardcoded Agents](./23-hardcoded-agents.md) - How to create native C# agents
