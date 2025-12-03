# Task: MCP Server for Agent Access

## Overview
Expose `ICodeGraphService` as an MCP (Model Context Protocol) server so AI assistants can directly query the code graph.

## Parent Spec
`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 4

## Goals
1. Create `Aura.Mcp` project with MCP server implementation
2. Expose core graph queries as MCP tools
3. Support stdio transport for VS Code extension integration
4. Enable natural language queries via LLM + graph

## MCP Protocol Overview

MCP uses JSON-RPC 2.0 over stdio (or SSE/WebSocket). Key concepts:
- **Tools**: Functions the server exposes for the client to call
- **Resources**: Data the server can provide (like file contents)
- **Prompts**: Pre-built prompts the server offers

For Aura, we primarily need **Tools**.

## Project Structure

```
src/
  Aura.Mcp/
    Aura.Mcp.csproj
    Program.cs
    McpServer.cs
    Tools/
      SearchNodesTool.cs
      FindImplementationsTool.cs
      FindCallersTool.cs
      GetNodeContentTool.cs
      FindTestsTool.cs
      FindDocumentationTool.cs
    Transport/
      StdioTransport.cs
      JsonRpcHandler.cs
```

## Project File

**File:** `src/Aura.Mcp/Aura.Mcp.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Aura.Mcp</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0-*" />
    <PackageReference Include="System.Text.Json" Version="10.0.0-*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Aura.Foundation\Aura.Foundation.csproj" />
  </ItemGroup>

</Project>
```

## JSON-RPC Handler

**File:** `src/Aura.Mcp/Transport/JsonRpcHandler.cs`

```csharp
public sealed class JsonRpcHandler
{
    private readonly Dictionary<string, Func<JsonElement?, Task<object?>>> _methods = new();

    public void RegisterMethod(string name, Func<JsonElement?, Task<object?>> handler)
    {
        _methods[name] = handler;
    }

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
    {
        if (!_methods.TryGetValue(request.Method, out var handler))
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}",
                },
            };
        }

        try
        {
            var result = await handler(request.Params);
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result,
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32000,
                    Message = ex.Message,
                },
            };
        }
    }
}

public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public required string Method { get; init; }
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("result")]
    public object? Result { get; init; }
    
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("data")]
    public object? Data { get; init; }
}
```

## Stdio Transport

**File:** `src/Aura.Mcp/Transport/StdioTransport.cs`

```csharp
public sealed class StdioTransport : BackgroundService
{
    private readonly JsonRpcHandler _handler;
    private readonly ILogger<StdioTransport> _logger;

    public StdioTransport(JsonRpcHandler handler, ILogger<StdioTransport> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin);
        using var writer = new StreamWriter(stdout) { AutoFlush = true };

        _logger.LogInformation("MCP server started on stdio");

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);
            if (line == null) break;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                if (request != null)
                {
                    var response = await _handler.HandleAsync(request);
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON-RPC request");
                var error = new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32700,
                        Message = "Parse error",
                    },
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(error));
            }
        }
    }
}
```

## MCP Server

**File:** `src/Aura.Mcp/McpServer.cs`

```csharp
public sealed class McpServer
{
    private readonly JsonRpcHandler _handler;
    private readonly ICodeGraphService _graphService;
    private readonly IRagService _ragService;
    private readonly ISmartContentService? _smartContentService;

    public McpServer(
        JsonRpcHandler handler,
        ICodeGraphService graphService,
        IRagService ragService,
        ISmartContentService? smartContentService = null)
    {
        _handler = handler;
        _graphService = graphService;
        _ragService = ragService;
        _smartContentService = smartContentService;

        RegisterMcpMethods();
        RegisterTools();
    }

    private void RegisterMcpMethods()
    {
        // MCP protocol methods
        _handler.RegisterMethod("initialize", HandleInitialize);
        _handler.RegisterMethod("tools/list", HandleListTools);
        _handler.RegisterMethod("tools/call", HandleCallTool);
        _handler.RegisterMethod("resources/list", HandleListResources);
    }

    private Task<object?> HandleInitialize(JsonElement? _)
    {
        return Task.FromResult<object?>(new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new
            {
                name = "aura-mcp",
                version = "1.0.0",
            },
            capabilities = new
            {
                tools = new { },
                resources = new { },
            },
        });
    }

    private Task<object?> HandleListTools(JsonElement? _)
    {
        var tools = new[]
        {
            new
            {
                name = "search_nodes",
                description = "Search for code elements by name, pattern, or semantic similarity",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query" },
                        nodeType = new { type = "string", description = "Filter by node type (class, method, interface, etc.)" },
                        limit = new { type = "integer", description = "Maximum results", @default = 10 },
                        semantic = new { type = "boolean", description = "Use semantic search", @default = false },
                    },
                    required = new[] { "query" },
                },
            },
            new
            {
                name = "find_implementations",
                description = "Find all types that implement a given interface",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        interfaceName = new { type = "string", description = "Interface name" },
                    },
                    required = new[] { "interfaceName" },
                },
            },
            new
            {
                name = "find_callers",
                description = "Find all methods that call a given method",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Method name" },
                        containingType = new { type = "string", description = "Optional containing type" },
                    },
                    required = new[] { "methodName" },
                },
            },
            new
            {
                name = "find_dependencies",
                description = "Find what a method depends on (calls, uses)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Method name" },
                        containingType = new { type = "string", description = "Optional containing type" },
                    },
                    required = new[] { "methodName" },
                },
            },
            new
            {
                name = "get_node_content",
                description = "Get full content and smart summary for a code element",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        nodeName = new { type = "string", description = "Node name or full name" },
                    },
                    required = new[] { "nodeName" },
                },
            },
            new
            {
                name = "find_tests",
                description = "Find tests that cover a code element",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        symbolName = new { type = "string", description = "Symbol name" },
                    },
                    required = new[] { "symbolName" },
                },
            },
            new
            {
                name = "find_documentation",
                description = "Find documentation that references a code element",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        symbolName = new { type = "string", description = "Symbol name" },
                    },
                    required = new[] { "symbolName" },
                },
            },
        };

        return Task.FromResult<object?>(new { tools });
    }

    private async Task<object?> HandleCallTool(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue) throw new ArgumentException("Missing params");
        
        var name = paramsElement.Value.GetProperty("name").GetString();
        var arguments = paramsElement.Value.GetProperty("arguments");

        return name switch
        {
            "search_nodes" => await SearchNodesAsync(arguments),
            "find_implementations" => await FindImplementationsAsync(arguments),
            "find_callers" => await FindCallersAsync(arguments),
            "find_dependencies" => await FindDependenciesAsync(arguments),
            "get_node_content" => await GetNodeContentAsync(arguments),
            "find_tests" => await FindTestsAsync(arguments),
            "find_documentation" => await FindDocumentationAsync(arguments),
            _ => throw new ArgumentException($"Unknown tool: {name}"),
        };
    }

    private async Task<object> SearchNodesAsync(JsonElement args)
    {
        var query = args.GetProperty("query").GetString()!;
        var nodeType = args.TryGetProperty("nodeType", out var nt) ? nt.GetString() : null;
        var limit = args.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;
        var semantic = args.TryGetProperty("semantic", out var s) && s.GetBoolean();

        if (semantic)
        {
            var results = await _ragService.SearchAsync(query, limit);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = FormatSearchResults(results),
                    },
                },
            };
        }
        else
        {
            CodeNodeType? type = nodeType != null 
                ? Enum.Parse<CodeNodeType>(nodeType, ignoreCase: true) 
                : null;
            var nodes = await _graphService.FindNodesAsync(query, type);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = FormatNodes(nodes.Take(limit)),
                    },
                },
            };
        }
    }

    private async Task<object> FindImplementationsAsync(JsonElement args)
    {
        var interfaceName = args.GetProperty("interfaceName").GetString()!;
        var nodes = await _graphService.FindImplementationsAsync(interfaceName);
        return new
        {
            content = new[]
            {
                new { type = "text", text = FormatNodes(nodes) },
            },
        };
    }

    // ... similar implementations for other tools ...

    private static string FormatNodes(IEnumerable<CodeNode> nodes)
    {
        var sb = new StringBuilder();
        foreach (var node in nodes)
        {
            sb.AppendLine($"- **{node.Name}** ({node.NodeType})");
            if (node.FullName != null) sb.AppendLine($"  Full name: `{node.FullName}`");
            if (node.FilePath != null) sb.AppendLine($"  File: `{node.FilePath}`:{node.LineNumber}");
            if (node.SmartSummary != null) sb.AppendLine($"  Summary: {node.SmartSummary}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
```

## Program Entry Point

**File:** `src/Aura.Mcp/Program.cs`

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true);

// Database context
builder.Services.AddDbContext<AuraDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Aura");
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector());
});

// Core services
builder.Services.AddScoped<ICodeGraphService, CodeGraphService>();
builder.Services.AddScoped<IRagService, RagService>();

// MCP
builder.Services.AddSingleton<JsonRpcHandler>();
builder.Services.AddSingleton<McpServer>();
builder.Services.AddHostedService<StdioTransport>();

var host = builder.Build();

// Initialize MCP server (registers tools)
_ = host.Services.GetRequiredService<McpServer>();

await host.RunAsync();
```

## VS Code Extension Integration

The MCP server is invoked via stdio. VS Code's MCP support (or Claude Desktop) can be configured:

**mcp.json / settings.json**:
```json
{
  "mcpServers": {
    "aura": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/Aura.Mcp"],
      "env": {
        "ConnectionStrings__Aura": "Host=localhost;Database=aura;..."
      }
    }
  }
}
```

Or with a published executable:
```json
{
  "mcpServers": {
    "aura": {
      "command": "path/to/Aura.Mcp.exe"
    }
  }
}
```

## Testing

### Unit Tests
- `JsonRpcHandlerTests.cs` - Request parsing, method dispatch
- `McpServerTests.cs` - Tool registration, response formatting

### Integration Tests
- Start MCP server, send JSON-RPC requests via pipe
- Verify tool responses match graph queries

### Manual Testing
```bash
# Test with echo
echo '{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet run --project src/Aura.Mcp
```

## Rollout Plan

1. **Phase 1**: Create `Aura.Mcp` project with stdio transport
2. **Phase 2**: Implement JSON-RPC handler
3. **Phase 3**: Register core MCP methods (initialize, tools/list)
4. **Phase 4**: Implement tool handlers
5. **Phase 5**: Test with VS Code / Claude Desktop

## Dependencies
- `Aura.Foundation` for graph/RAG services
- No external MCP library (protocol is simple)

## Estimated Effort
- **Medium complexity**, **Medium effort**
- MCP protocol is simple, main work is tool formatting

## Success Criteria
- [ ] `initialize` returns valid capabilities
- [ ] `tools/list` returns all registered tools
- [ ] `tools/call` for `search_nodes` returns formatted results
- [ ] VS Code can connect and invoke tools
- [ ] Claude Desktop can query the code graph
