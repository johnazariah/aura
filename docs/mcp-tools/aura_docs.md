# aura_docs - Documentation Search Tool

## Overview

The `aura_docs` tool provides semantic search capabilities over Aura's indexed documentation. It uses RAG (Retrieval-Augmented Generation) to find relevant documentation chunks based on natural language queries.

**Use this tool when:**
- You need information about Aura's features, configuration, or best practices
- You want to understand how to use specific Aura capabilities
- You need troubleshooting guidance
- You're looking for examples or usage patterns

## Tool Definition

### MCP Tool Schema

```json
{
  "name": "aura_docs",
  "description": "Search Aura documentation using semantic search. Returns relevant documentation chunks with similarity scores.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Natural language search query describing what documentation you're looking for"
      }
    },
    "required": ["query"]
  }
}
```

## Input Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Natural language search query (e.g., "How to configure OpenAI?" or "Best practices for workflow management") |

## Output Format

The tool returns a JSON object with the following structure:

```json
{
  "query": "original search query",
  "resultCount": 5,
  "results": [
    {
      "content": "Relevant documentation text...",
      "sourcePath": "path/to/source/document.md",
      "score": 0.85,
      "contentType": "Documentation",
      "metadata": {
        "title": "Document Title",
        "section": "Section Name"
      }
    }
  ]
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `query` | string | The original search query submitted |
| `resultCount` | number | Number of results returned |
| `results` | array | Array of documentation chunks matching the query |
| `results[].content` | string | The actual documentation text |
| `results[].sourcePath` | string | Path to the source document |
| `results[].score` | number | Similarity score (0.0-1.0), higher is better |
| `results[].contentType` | string | Content type (typically "Documentation" or "Markdown") |
| `results[].metadata` | object | Additional metadata about the source document |

## Search Behavior

The `aura_docs` tool uses the following configuration:

- **TopK**: Returns up to 10 results
- **Content Types**: Searches only Documentation and Markdown content types
- **MinScore**: Only returns results with similarity score ≥ 0.5
- **Search Algorithm**: Semantic vector search using embeddings

Results are ranked by similarity score, with the most relevant documentation appearing first.

## Examples

### Example 1: Configuration Help

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "aura_docs",
    "arguments": {
      "query": "How do I configure Aura to use OpenAI GPT-4?"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "query": "How do I configure Aura to use OpenAI GPT-4?",
    "resultCount": 3,
    "results": [
      {
        "content": "# LLM Configuration\n\nAura supports multiple LLM providers including OpenAI. To configure OpenAI GPT-4:\n\n1. Set your API key: `$env:OPENAI_API_KEY = 'sk-...'`\n2. Update appsettings.json:\n```json\n{\n  \"LLM\": {\n    \"Provider\": \"OpenAI\",\n    \"Model\": \"gpt-4-turbo-preview\"\n  }\n}\n```",
        "sourcePath": "docs/configuration/llm-providers.md",
        "score": 0.92,
        "contentType": "Documentation",
        "metadata": {
          "title": "LLM Configuration Guide",
          "section": "OpenAI Setup"
        }
      },
      {
        "content": "## Environment Variables\n\nRequired environment variables for OpenAI:\n- `OPENAI_API_KEY`: Your OpenAI API key\n- `OPENAI_ORG_ID`: (Optional) Organization ID",
        "sourcePath": "docs/configuration/environment.md",
        "score": 0.78,
        "contentType": "Documentation",
        "metadata": {
          "title": "Environment Configuration"
        }
      },
      {
        "content": "### Supported Models\n\nOpenAI models supported by Aura:\n- gpt-4-turbo-preview\n- gpt-4\n- gpt-3.5-turbo\n\nRecommended: Use gpt-4-turbo-preview for best results.",
        "sourcePath": "docs/reference/llm-models.md",
        "score": 0.71,
        "contentType": "Documentation",
        "metadata": {
          "title": "LLM Models Reference"
        }
      }
    ]
  }
}
```

### Example 2: Workflow Management

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "aura_docs",
    "arguments": {
      "query": "best practices for managing development workflows"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "query": "best practices for managing development workflows",
    "resultCount": 5,
    "results": [
      {
        "content": "# Workflow Best Practices\n\n1. **Use git worktrees** - Each workflow gets its own worktree for isolation\n2. **Keep workflows focused** - One feature or bug fix per workflow\n3. **Update workflow steps** - Use `aura_workflow` to track progress\n4. **Complete properly** - Follow the completion ceremony for documentation",
        "sourcePath": "docs/user-guide/workflows.md",
        "score": 0.88,
        "contentType": "Documentation",
        "metadata": {
          "title": "Workflow Management Guide",
          "section": "Best Practices"
        }
      }
    ]
  }
}
```

### Example 3: Tool Usage

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "aura_docs",
    "arguments": {
      "query": "When should I use aura_refactor versus manual code editing?"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "query": "When should I use aura_refactor versus manual code editing?",
    "resultCount": 4,
    "results": [
      {
        "content": "## aura_refactor vs Manual Editing\n\n**Use aura_refactor when:**\n- Renaming symbols across multiple files\n- Extracting methods or variables\n- Changing method signatures\n- You need blast radius analysis\n\n**Use manual editing when:**\n- Single-line fixes\n- Non-C# files (JSON, YAML, etc.)\n- aura_refactor doesn't support the operation",
        "sourcePath": "docs/user-guide/mcp-tools.md",
        "score": 0.94,
        "contentType": "Documentation",
        "metadata": {
          "title": "MCP Tools Reference",
          "section": "Tool Selection"
        }
      }
    ]
  }
}
```

### Example 4: Troubleshooting

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "aura_docs",
    "arguments": {
      "query": "API health check returns 503"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "query": "API health check returns 503",
    "resultCount": 2,
    "results": [
      {
        "content": "### API Returns 503\n\n**Possible causes:**\n1. Service not running - Check `Get-Service Aura`\n2. Port conflict - Verify port 5300 is available\n3. Database not accessible - Check connection string\n\n**Solutions:**\n```powershell\n# Restart service\nRestart-Service Aura\n\n# Check logs\nGet-Content C:\\ProgramData\\Aura\\logs\\aura-*.log -Tail 50\n```",
        "sourcePath": "docs/troubleshooting/api-issues.md",
        "score": 0.87,
        "contentType": "Documentation",
        "metadata": {
          "title": "API Troubleshooting Guide"
        }
      }
    ]
  }
}
```

## Integration with Other Tools

The `aura_docs` tool works seamlessly with other Aura MCP tools:

### Workflow Integration

```javascript
// 1. Search for relevant documentation
const docsResult = await aura_docs({
  query: "How to implement a new refactoring operation?"
});

// 2. Create a workflow based on the guidance found
const workflow = await aura_workflow({
  operation: "create",
  title: "Implement extract_interface refactoring",
  description: docsResult.results[0].content
});

// 3. Use other tools to implement the feature
await aura_generate({
  operation: "create_type",
  // ... implementation details
});
```

### Self-Service Agent Pattern

Agents can use `aura_docs` to self-serve information before asking the user:

```javascript
// Agent encounters an unfamiliar configuration option
const docs = await aura_docs({
  query: "RAG MinScore configuration option meaning"
});

// Use the documentation to understand and configure properly
// instead of asking the user for clarification
```

## Performance Characteristics

| Metric | Typical Value |
|--------|---------------|
| Average response time | 100-300ms |
| Max results | 10 |
| Min similarity score | 0.5 |
| Query length limit | 1000 characters |

## Error Handling

### Missing Query Parameter

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "aura_docs",
    "arguments": {}
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "error": {
    "code": -32602,
    "message": "Invalid params: query parameter is required"
  }
}
```

### No Results Found

When no documentation matches the query (all results below MinScore threshold):

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "query": "how to configure flux capacitor",
    "resultCount": 0,
    "results": []
  }
}
```

## Best Practices

### Query Formulation

✅ **Good queries:**
- "How to configure OpenAI API key?"
- "Best practices for workflow management"
- "Troubleshooting 503 errors from API"
- "What is the purpose of MinScore in RAG queries?"

❌ **Poor queries:**
- "OpenAI" (too vague)
- "error" (too generic)
- "asdfjkl" (nonsensical)

### Using Search Results

1. **Check the score** - Results with score > 0.8 are highly relevant
2. **Review multiple results** - The top 3-5 results often provide complementary information
3. **Use sourcePath** - Reference the source document for complete context
4. **Combine with other tools** - Use findings to inform actions with other MCP tools

### When NOT to Use

- **Searching code** - Use `aura_search` instead
- **Finding specific files** - Use file system tools
- **Listing available tools** - Use MCP protocol's `tools/list` method
- **Getting tool schemas** - Use MCP protocol's tool introspection

## Implementation Details

### Backend Implementation

The tool is implemented in `AuraDocsTool.cs`:

```csharp
public async Task<object> SearchDocumentationAsync(string query, CancellationToken ct)
{
    var options = new RagQueryOptions
    {
        TopK = 10,
        ContentTypes = new[] { RagContentType.Documentation, RagContentType.Markdown },
        MinScore = 0.5
    };

    var results = await _ragService.QueryAsync(query, options, ct);

    return new
    {
        query,
        resultCount = results.Count,
        results = results.Select(r => new
        {
            content = r.Text,
            sourcePath = r.SourcePath,
            score = r.Score,
            contentType = r.ContentType.ToString(),
            metadata = r.Metadata
        })
    };
}
```

### Indexed Content

The tool searches over:
- Markdown files in `docs/` directory
- README files throughout the repository
- `.project/` specification documents
- Feature documentation in `.project/features/`
- ADRs (Architecture Decision Records)

Content is indexed during Aura startup and kept in sync with file changes.

## See Also

- [MCP Tools Reference](../user-guide/mcp-tools.md) - Overview of all Aura MCP tools
- [RAG Configuration](../configuration/rag.md) - RAG service configuration options
- [aura_search](./aura_search.md) - Semantic code search tool
- [Getting Started](../getting-started/README.md) - Initial setup and configuration
