---
description: Guide for adding a new MCP tool to Aura's McpHandler.
---

# Add MCP Tool

You are adding a new MCP tool to Aura's MCP server. MCP tools are exposed to GitHub Copilot and are the primary way Copilot interacts with Aura's capabilities.

## Architecture Overview

MCP tools are implemented as methods on the `McpHandler` partial class in `src/Aura.Api/Mcp/`. The handler:
- Receives JSON-RPC requests via `src/Aura.Api/Endpoints/McpEndpoints.cs`
- Dispatches to tool methods registered in the `_tools` dictionary
- Returns JSON-RPC responses

Current tools (13): `aura_architect`, `aura_docs`, `aura_edit`, `aura_generate`, `aura_inspect`, `aura_navigate`, `aura_pattern`, `aura_refactor`, `aura_search`, `aura_tree`, `aura_validate`, `aura_workflow`, `aura_workspace`.

## Step 1: Decide — New Tool or New Operation?

Most new capabilities should be **operations on existing tools** rather than new tools. The project consolidated from 28 tools to 13 meta-tools.

**Add a new operation** if the capability fits an existing tool's domain:
- Code navigation → add operation to `aura_navigate`
- Code generation → add operation to `aura_generate`
- Refactoring → add operation to `aura_refactor`
- Validation → add operation to `aura_validate`

**Add a new tool** only if the capability is a genuinely new domain.

## Step 2: Create the Partial File (if new tool)

Create `src/Aura.Api/Mcp/McpHandler.{ToolName}.cs`:

```csharp
// <copyright file="McpHandler.{ToolName}.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;

public sealed partial class McpHandler
{
    private async Task<object> {ToolName}Async(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "operation_name" => await HandleOperationNameAsync(args.Value, ct),
            _ => throw new ArgumentException($"Unknown {tool_name} operation: {operation}")
        };
    }

    private async Task<object> HandleOperationNameAsync(JsonElement args, CancellationToken ct)
    {
        // Implementation here
        // Parse parameters from args
        // Call service methods
        // Return result object (will be serialized to JSON)
    }
}
```

## Step 3: Register the Tool

Add the tool to the `_tools` dictionary in `McpHandler.cs` constructor:

```csharp
// In McpHandler.cs constructor, add to _tools dictionary:
["aura_{tool_name}"] = {ToolName}Async,
```

## Step 4: Add Tool Descriptor

In `McpHandler.cs`, find the `GetToolDescriptors()` method (or the `tools/list` handler) and add a descriptor:

```csharp
new
{
    name = "aura_{tool_name}",
    description = "Brief description of what this tool does",
    inputSchema = new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["operation"] = new { type = "string", description = "Operation: operation_name", @enum = new[] { "operation_name" } },
            ["param1"] = new { type = "string", description = "Description of param1" },
        },
        required = new[] { "operation" }
    }
}
```

## Step 5: Add Service Dependencies (if needed)

If the tool needs a new service:

1. Define the interface in the appropriate module (`Aura.Foundation` or `Aura.Module.Developer`)
2. Implement the service
3. Register in DI (`Foundation/DependencyInjection.cs` or module's DI file)
4. Add constructor parameter to `McpHandler.cs`

## Step 6: Document in MCP Tools Instructions

Update `prompts/mcp-tools-instructions.md` to include the new tool. This file is loaded by Copilot to understand available tools.

## Step 7: Add Tests

Create tests in `tests/Aura.Foundation.Tests/` or `tests/Aura.Module.Developer.Tests/`:

```csharp
[Fact]
public async Task ToolName_OperationName_ReturnsExpectedResult()
{
    // Arrange
    var handler = CreateMcpHandler();
    var args = JsonDocument.Parse("""{"operation": "operation_name", "param1": "value"}""").RootElement;

    // Act
    var result = await handler.{ToolName}Async(args, CancellationToken.None);

    // Assert
    // Verify result structure
}
```

## Step 8: Build and Verify

```powershell
dotnet build
dotnet test
```

Then ask user to run `Update-LocalInstall.ps1` as Administrator and verify:

```powershell
# List tools to see the new one
curl -X POST http://localhost:5300/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

## Checklist

- [ ] Decided: new tool vs. new operation on existing tool
- [ ] Partial file created with async handler method
- [ ] Tool registered in `_tools` dictionary
- [ ] Tool descriptor added with inputSchema
- [ ] Service dependencies injected (if needed)
- [ ] `prompts/mcp-tools-instructions.md` updated
- [ ] Tests written and passing
- [ ] Build succeeds
- [ ] Tool appears in `tools/list` response
