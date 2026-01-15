# MCP Server Integration

**Status:** ✅ Complete  
**Completed:** 2026-01-15  
**Created:** 2025-12-12

## Overview

Expose Aura's RAG and Code Graph context to GitHub Copilot and other AI assistants via Model Context Protocol (MCP). This enables Copilot to query Aura for deep codebase context during conversations.

## Strategic Context

Per [ADR-021](../../adr/021-session-infrastructure-pivot.md), Aura is pivoting to "development session infrastructure that extends GitHub Copilot." MCP is the critical integration point—it lets Copilot call Aura for context without Aura needing to run the agent.

## Key Discovery: VS Code Native MCP Support

VS Code now has built-in MCP support via `vscode.lm.registerMcpServerDefinitionProvider`. This means:

1. **No separate Aura.Mcp project needed** — extend existing API + extension
2. **Extension registers Aura as MCP provider** — Copilot discovers automatically
3. **HTTP transport works** — point to running Aura.Api server
4. **Dynamic registration** — no user configuration required

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    GitHub Copilot                           │
│                  (Agent Mode / Chat)                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ MCP Protocol (JSON-RPC)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Aura VS Code Extension                    │
│          registerMcpServerDefinitionProvider()              │
│                  ↓ points to ↓                              │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Aura.Api                               │
│                   POST /mcp/rpc                             │
├─────────────────────────────────────────────────────────────┤
│  Tools:                                                     │
│  • aura_search_code - RAG semantic search                   │
│  • aura_find_implementations - Code Graph query             │
│  • aura_find_callers - Code Graph query                     │
│  • aura_get_type_members - Code Graph query                 │
│  • aura_get_story_context - Active story info               │
│  • aura_list_stories - Stories in flight                    │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
    ┌──────────────────┐            ┌──────────────────┐
    │   RAG Service    │            │ Code Graph       │
    │   (pgvector)     │            │ Service          │
    └──────────────────┘            └──────────────────┘
```

## Implementation

### Phase 1: Extension MCP Registration

**File:** `extension/src/extension.ts`

```typescript
import * as vscode from 'vscode';

// In activate()
const mcpProvider = vscode.lm.registerMcpServerDefinitionProvider('aura.context', {
    onDidChangeMcpServerDefinitions: new vscode.EventEmitter<void>().event,
    
    provideMcpServerDefinitions: async () => {
        const apiUrl = vscode.workspace.getConfiguration('aura').get<string>('apiUrl') 
            || 'http://localhost:5300';
        
        return [
            new vscode.McpHttpServerDefinition({
                label: 'Aura Codebase Context',
                uri: `${apiUrl}/mcp`,
                version: '1.0.0'
            })
        ];
    },
    
    resolveMcpServerDefinition: async (server) => server
});

context.subscriptions.push(mcpProvider);
```

**File:** `extension/package.json` (add contribution)

```json
{
  "contributes": {
    "mcpServerDefinitionProviders": [
      {
        "id": "aura.context",
        "label": "Aura Codebase Context"
      }
    ]
  }
}
```

### Phase 2: API MCP Endpoint

**File:** `src/Aura.Api/Program.cs` (add endpoint group)

```csharp
// MCP JSON-RPC endpoint
app.MapPost("/mcp", async (HttpContext ctx, McpHandler handler) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var json = await reader.ReadToEndAsync();
    var response = await handler.HandleAsync(json);
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(response);
});
```

**File:** `src/Aura.Foundation/Mcp/McpHandler.cs`

```csharp
public class McpHandler
{
    private readonly IRagService _ragService;
    private readonly ICodeGraphService _graphService;
    private readonly IWorkflowRepository _workflowRepo;
    
    private readonly Dictionary<string, Func<JsonElement?, Task<object>>> _tools;
    
    public McpHandler(
        IRagService ragService, 
        ICodeGraphService graphService,
        IWorkflowRepository workflowRepo)
    {
        _ragService = ragService;
        _graphService = graphService;
        _workflowRepo = workflowRepo;
        
        _tools = new()
        {
            ["aura_search_code"] = SearchCodeAsync,
            ["aura_find_implementations"] = FindImplementationsAsync,
            ["aura_find_callers"] = FindCallersAsync,
            ["aura_get_type_members"] = GetTypeMembersAsync,
            ["aura_get_story_context"] = GetStoryContextAsync,
            ["aura_list_stories"] = ListStoriesAsync,
        };
    }
    
    public async Task<string> HandleAsync(string json)
    {
        var request = JsonSerializer.Deserialize<JsonRpcRequest>(json);
        
        var response = request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            _ => ErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
        };
        
        return JsonSerializer.Serialize(response);
    }
    
    private object HandleInitialize(JsonRpcRequest request) => new
    {
        jsonrpc = "2.0",
        id = request.Id,
        result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { tools = new { } },
            serverInfo = new { name = "Aura", version = "1.2.0" }
        }
    };
    
    private object HandleToolsList(JsonRpcRequest request) => new
    {
        jsonrpc = "2.0",
        id = request.Id,
        result = new
        {
            tools = new[]
            {
                new { name = "aura_search_code", description = "Semantic search across codebase", 
                      inputSchema = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" } } },
                new { name = "aura_find_implementations", description = "Find implementations of an interface or abstract class",
                      inputSchema = new { type = "object", properties = new { typeName = new { type = "string" } }, required = new[] { "typeName" } } },
                new { name = "aura_find_callers", description = "Find methods that call a given method",
                      inputSchema = new { type = "object", properties = new { methodName = new { type = "string" } }, required = new[] { "methodName" } } },
                new { name = "aura_get_type_members", description = "Get all members of a type",
                      inputSchema = new { type = "object", properties = new { typeName = new { type = "string" } }, required = new[] { "typeName" } } },
                new { name = "aura_list_stories", description = "List active development stories",
                      inputSchema = new { type = "object", properties = new { } } },
                new { name = "aura_get_story_context", description = "Get context for current story",
                      inputSchema = new { type = "object", properties = new { storyId = new { type = "string" } } } },
            }
        }
    };
    
    private async Task<object> HandleToolCallAsync(JsonRpcRequest request)
    {
        var toolName = request.Params?.GetProperty("name").GetString();
        var args = request.Params?.GetProperty("arguments");
        
        if (toolName == null || !_tools.TryGetValue(toolName, out var handler))
        {
            return ErrorResponse(request.Id, -32602, $"Unknown tool: {toolName}");
        }
        
        try
        {
            var result = await handler(args);
            return new { jsonrpc = "2.0", id = request.Id, result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result) } } } };
        }
        catch (Exception ex)
        {
            return ErrorResponse(request.Id, -32000, ex.Message);
        }
    }
    
    // Tool implementations...
    private async Task<object> SearchCodeAsync(JsonElement? args)
    {
        var query = args?.GetProperty("query").GetString() ?? "";
        var results = await _ragService.SearchAsync(query, limit: 10);
        return results.Select(r => new { r.Content, r.FilePath, r.Score });
    }
    
    private async Task<object> FindImplementationsAsync(JsonElement? args)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var results = await _graphService.FindImplementationsAsync(typeName);
        return results;
    }
    
    // ... other tool implementations
}
```

### Phase 3: Tool Implementations

Each tool maps to existing services:

| MCP Tool | Existing Service | Method |
|----------|------------------|--------|
| `aura_search_code` | `IRagService` | `SearchAsync` |
| `aura_find_implementations` | `ICodeGraphService` | `FindImplementationsAsync` |
| `aura_find_callers` | `ICodeGraphService` | `FindCallersAsync` |
| `aura_get_type_members` | `ICodeGraphService` | `GetTypeMembersAsync` |
| `aura_list_stories` | `IWorkflowRepository` | `GetActiveAsync` |
| `aura_get_story_context` | `IWorkflowRepository` | `GetByIdAsync` + enrichment |

## Files to Modify

| File | Change |
|------|--------|
| `extension/src/extension.ts` | Register MCP server definition provider |
| `extension/package.json` | Add `mcpServerDefinitionProviders` contribution |
| `src/Aura.Api/Program.cs` | Add `/mcp` endpoint |
| `src/Aura.Foundation/Mcp/McpHandler.cs` | New file: JSON-RPC handler |
| `src/Aura.Foundation/Mcp/JsonRpcTypes.cs` | New file: Request/response types |

## Testing

### Manual Testing

1. Start Aura API (`Start-Api`)
2. Reload extension
3. Open Copilot Chat
4. Ask: "Using Aura, find implementations of ILlmProvider"
5. Verify Copilot calls the MCP tool and shows results

### Unit Tests

```csharp
[Fact]
public async Task HandleToolsList_ReturnsAllTools()
{
    var handler = new McpHandler(...);
    var response = await handler.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
    var result = JsonDocument.Parse(response);
    result.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength().Should().Be(6);
}

[Fact]
public async Task SearchCode_ReturnsRagResults()
{
    var mockRag = new Mock<IRagService>();
    mockRag.Setup(r => r.SearchAsync("test", 10)).ReturnsAsync(new[] { new RagResult { Content = "...", FilePath = "test.cs" } });
    
    var handler = new McpHandler(mockRag.Object, ...);
    var response = await handler.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"aura_search_code","arguments":{"query":"test"}}}""");
    
    response.Should().Contain("test.cs");
}
```

### Integration Test with Claude Desktop

MCP also works with Claude Desktop. Test by adding to Claude's config:

```json
{
  "mcpServers": {
    "aura": {
      "command": "curl",
      "args": ["-X", "POST", "http://localhost:5300/mcp", "-d", "@-"]
    }
  }
}
```

## Success Criteria

- [x] Extension registers MCP provider on activation
- [x] `/mcp` endpoint responds to `initialize` request
- [x] `tools/list` returns all tools (28 as of 2026-01-15)
- [x] `tools/call` for `aura_search_code` returns RAG results
- [x] `tools/call` for `aura_find_implementations` returns graph results
- [x] Copilot can invoke tools during conversation
- [ ] Claude Desktop can invoke tools (bonus - not tested)

## Future Enhancements

1. **Story-aware context** — include current worktree/story in tool responses
2. **Streaming results** — for large search results
3. **Resources** — expose file contents as MCP resources
4. **Prompts** — pre-built prompts for common queries

## Dependencies

- VS Code 1.96+ (MCP support)
- GitHub Copilot with MCP tool calling enabled
- Running Aura.Api server

## Estimated Effort

| Task | Effort |
|------|--------|
| Extension MCP registration | 2 hours |
| API endpoint + handler | 4 hours |
| Tool implementations | 4 hours |
| Testing | 4 hours |
| Documentation | 2 hours |
| **Total** | **~2 days** |
