# Feature: aura_docs - Bundled Documentation for Agents

**Status:** 📋 Ready for Development
**Priority:** Medium
**Type:** Feature
**Estimated Effort:** 3 hours

## Problem Statement

Agents often need guidance on how to use Aura's tools, configuration options, or best practices. Currently they must:
1. Ask the user
2. Search external documentation
3. Guess based on tool descriptions

fs2's `docs_list`/`docs_get` pattern lets agents self-serve documentation bundled with the tool.

## Design

### MCP Tools

#### `aura_docs_list`

```json
{
  "name": "aura_docs_list",
  "description": "Browse available documentation. Use to discover guides, references, and best practices.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "category": {
        "type": "string",
        "enum": ["guide", "reference", "troubleshooting", "all"],
        "default": "all"
      },
      "tags": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Filter by tags (OR logic - matches ANY tag)"
      }
    }
  }
}
```

**Response:**
```json
{
  "count": 4,
  "docs": [
    {
      "id": "agents",
      "title": "AI Agent Integration Guide",
      "summary": "How to use Aura MCP tools effectively - workflows, patterns, examples.",
      "category": "guide",
      "tags": ["agents", "mcp", "tools", "getting-started"]
    },
    {
      "id": "configuration",
      "title": "Configuration Reference",
      "summary": "All configuration options for LLM, RAG, and workspace settings.",
      "category": "reference",
      "tags": ["config", "llm", "rag", "setup"]
    }
  ]
}
```

#### `aura_docs_get`

```json
{
  "name": "aura_docs_get",
  "description": "Retrieve full document content by ID. Use after finding relevant docs via aura_docs_list.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "id": {
        "type": "string",
        "description": "Document ID from aura_docs_list"
      }
    },
    "required": ["id"]
  }
}
```

**Response:**
```json
{
  "id": "agents",
  "title": "AI Agent Integration Guide",
  "category": "guide",
  "tags": ["agents", "mcp", "tools", "getting-started"],
  "content": "# AI Agent Integration Guide\n\n## Overview\n\nAura provides MCP tools for...",
  "lastUpdated": "2026-01-24"
}
```

### Document Registry

```yaml
# src/Aura.Api/Docs/registry.yaml
documents:
  - id: agents
    title: "AI Agent Integration Guide"
    summary: "How to use Aura MCP tools effectively - workflows, patterns, examples."
    category: guide
    tags: [agents, mcp, tools, getting-started]
    path: agents.md

  - id: configuration
    title: "Configuration Reference"
    summary: "All configuration options for LLM, RAG, and workspace settings."
    category: reference
    tags: [config, llm, rag, setup]
    path: configuration.md

  - id: tools-reference
    title: "MCP Tools Reference"
    summary: "Complete reference for all aura_* MCP tools with examples."
    category: reference
    tags: [mcp, tools, api]
    path: tools-reference.md

  - id: troubleshooting
    title: "Troubleshooting Guide"
    summary: "Common issues and solutions for Aura setup and usage."
    category: troubleshooting
    tags: [debug, errors, help]
    path: troubleshooting.md
```

### Implementation

#### 1. Document Structure

```
src/Aura.Api/
├── Docs/
│   ├── registry.yaml
│   ├── agents.md
│   ├── configuration.md
│   ├── tools-reference.md
│   └── troubleshooting.md
```

#### 2. Embed as Resources

```xml
<!-- src/Aura.Api/Aura.Api.csproj -->
<ItemGroup>
  <EmbeddedResource Include="Docs\**\*" />
</ItemGroup>
```

#### 3. DocsService

```csharp
public interface IDocsService
{
    IReadOnlyList<DocumentEntry> ListDocuments(string? category = null, IReadOnlyList<string>? tags = null);
    DocumentContent? GetDocument(string id);
}

public record DocumentEntry(
    string Id,
    string Title,
    string Summary,
    string Category,
    IReadOnlyList<string> Tags);

public record DocumentContent(
    string Id,
    string Title,
    string Category,
    IReadOnlyList<string> Tags,
    string Content,
    string LastUpdated);
```

```csharp
public sealed class DocsService : IDocsService
{
    private readonly DocsRegistry _registry;
    private readonly Assembly _assembly;

    public DocsService()
    {
        _assembly = typeof(DocsService).Assembly;
        _registry = LoadRegistry();
    }

    private DocsRegistry LoadRegistry()
    {
        using var stream = _assembly.GetManifestResourceStream("Aura.Api.Docs.registry.yaml");
        using var reader = new StreamReader(stream!);
        var yaml = reader.ReadToEnd();
        return YamlDeserializer.Deserialize<DocsRegistry>(yaml);
    }

    public DocumentContent? GetDocument(string id)
    {
        var entry = _registry.Documents.FirstOrDefault(d => d.Id == id);
        if (entry is null) return null;

        var resourceName = $"Aura.Api.Docs.{entry.Path.Replace("/", ".")}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        return new DocumentContent(
            entry.Id, entry.Title, entry.Category, entry.Tags,
            content, DateTime.UtcNow.ToString("yyyy-MM-dd"));
    }
}
```

#### 4. MCP Handler

```csharp
["aura_docs_list"] = DocsListAsync,
["aura_docs_get"] = DocsGetAsync,

private Task<object> DocsListAsync(JsonElement? args, CancellationToken ct)
{
    var category = args?.TryGetProperty("category", out var c) == true ? c.GetString() : null;
    var tags = args?.TryGetProperty("tags", out var t) == true 
        ? t.EnumerateArray().Select(x => x.GetString()!).ToList() 
        : null;

    var docs = _docsService.ListDocuments(
        category == "all" ? null : category, 
        tags);

    return Task.FromResult<object>(new { count = docs.Count, docs });
}

private Task<object> DocsGetAsync(JsonElement? args, CancellationToken ct)
{
    var id = args?.GetProperty("id").GetString() 
        ?? throw new ArgumentException("id required");

    var doc = _docsService.GetDocument(id);
    if (doc is null)
        throw new KeyNotFoundException($"Document '{id}' not found");

    return Task.FromResult<object>(doc);
}
```

## Initial Documents

### 1. agents.md (~200 lines)
- Overview of Aura's agent capabilities
- Available MCP tools with brief descriptions
- Recommended workflows (explore → search → modify)
- When to use each tool
- Best practices

### 2. configuration.md (~300 lines)
- LLM provider configuration
- RAG settings
- Workspace configuration
- Environment variables
- Secrets management

### 3. tools-reference.md (~400 lines)
- Complete reference for each `aura_*` tool
- Input schemas
- Output formats
- Examples

### 4. troubleshooting.md (~150 lines)
- Common errors and solutions
- Diagnostic commands
- How to report issues

## Files to Create/Change

| File | Change |
|------|--------|
| `src/Aura.Api/Docs/registry.yaml` | New - document registry |
| `src/Aura.Api/Docs/*.md` | New - 4 documents |
| `src/Aura.Api/Aura.Api.csproj` | Add embedded resources |
| `src/Aura.Api/Services/DocsService.cs` | New service |
| `src/Aura.Api/Services/IDocsService.cs` | New interface |
| `src/Aura.Api/Mcp/McpHandler.cs` | Add tools |
| DI registration | Register DocsService |

## Acceptance Criteria

- [ ] `aura_docs_list` returns all documents
- [ ] Filtering by category works
- [ ] Filtering by tags works (OR logic)
- [ ] `aura_docs_get` returns full content
- [ ] Documents are embedded in assembly (no external files needed)
- [ ] At least 4 initial documents created
- [ ] MCP tool descriptions are agent-friendly

## Future Enhancements

- Section extraction (get specific heading from doc)
- Search within docs
- User-contributed docs (from workspace `.aura/docs/`)
- Version-specific documentation
