# Feature: aura_tree - Hierarchical Code Exploration

**Status:** ✅ Complete
**Completed:** 2026-01-24
**Priority:** High
**Type:** Feature
**Estimated Effort:** 4 hours

## Problem Statement

Agents need to understand codebase structure before diving into specifics. Currently they must use `aura_search` or grep, which returns flat results. fs2's `tree` tool lets agents see hierarchy: files → classes → methods.

## Design

### MCP Tool: `aura_tree`

```json
{
  "name": "aura_tree",
  "description": "Explore codebase structure as a hierarchical tree. Use to understand project layout, find classes/functions, and navigate before reading source.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workspacePath": {
        "type": "string",
        "description": "Workspace root path"
      },
      "pattern": {
        "type": "string",
        "description": "Filter pattern - file path, class name, or function name. Use '.' for root.",
        "default": "."
      },
      "maxDepth": {
        "type": "integer",
        "description": "Maximum depth to traverse. 0 = unlimited, 1 = top-level only.",
        "default": 2
      },
      "detail": {
        "type": "string",
        "enum": ["min", "max"],
        "description": "min = name + type only. max = includes signatures, line numbers.",
        "default": "min"
      }
    },
    "required": ["workspacePath"]
  }
}
```

### Output Format

```json
{
  "meta": {
    "total_nodes": 42,
    "total_files": 8,
    "pattern": "OrderService",
    "depth": 2
  },
  "tree": [
    {
      "node_id": "file:src/Services/OrderService.cs",
      "name": "OrderService.cs",
      "type": "file",
      "path": "src/Services/OrderService.cs",
      "children": [
        {
          "node_id": "class:src/Services/OrderService.cs:OrderService",
          "name": "OrderService",
          "type": "class",
          "signature": "public class OrderService : IOrderService",
          "line": 15,
          "children": [
            {
              "node_id": "method:src/Services/OrderService.cs:OrderService.CreateOrder",
              "name": "CreateOrder",
              "type": "method",
              "signature": "public async Task<Order> CreateOrder(CreateOrderRequest request)",
              "line": 42
            }
          ]
        }
      ]
    }
  ]
}
```

### Node ID Format

`{type}:{path}:{symbol}` where:
- **type**: `file`, `namespace`, `class`, `interface`, `struct`, `enum`, `method`, `function`, `property`
- **path**: Relative file path (forward slashes)
- **symbol**: Fully qualified symbol name (optional for files)

Examples:
- `file:src/Services/OrderService.cs`
- `class:src/Services/OrderService.cs:OrderService`
- `method:src/Services/OrderService.cs:OrderService.CreateOrder`

### Implementation

#### 1. Extend RAG Metadata

Currently TreeSitter stores metadata in `RagChunk.MetadataJson`. Ensure it includes:

```json
{
  "symbolName": "OrderService",
  "chunkType": "class",
  "signature": "public class OrderService : IOrderService",
  "startLine": 15,
  "endLine": 89,
  "parentSymbol": null,
  "language": "csharp"
}
```

#### 2. Add to IRagService

```csharp
/// <summary>
/// Get all chunks with metadata for a workspace, grouped for tree building.
/// </summary>
Task<IReadOnlyList<ChunkMetadata>> GetChunksForTreeAsync(
    string workspacePath,
    string? pattern = null,
    CancellationToken ct = default);
```

#### 3. Tree Builder Service

```csharp
public interface ITreeBuilderService
{
    TreeResult BuildTree(
        IReadOnlyList<ChunkMetadata> chunks,
        string pattern,
        int maxDepth,
        TreeDetail detail);
}
```

Logic:
1. Group chunks by file path
2. Within each file, build parent-child relationships (class → method)
3. Apply pattern filter (fuzzy match on path or symbol)
4. Limit depth
5. Format output

#### 4. MCP Handler

```csharp
["aura_tree"] = TreeAsync,

private async Task<object> TreeAsync(JsonElement? args, CancellationToken ct)
{
    var workspacePath = args?.GetProperty("workspacePath").GetString() 
        ?? throw new ArgumentException("workspacePath required");
    var pattern = args?.TryGetProperty("pattern", out var p) == true 
        ? p.GetString() : ".";
    var maxDepth = args?.TryGetProperty("maxDepth", out var d) == true 
        ? d.GetInt32() : 2;
    var detail = args?.TryGetProperty("detail", out var det) == true 
        ? det.GetString() : "min";

    var chunks = await _ragService.GetChunksForTreeAsync(workspacePath, pattern, ct);
    var tree = _treeBuilder.BuildTree(chunks, pattern, maxDepth, 
        detail == "max" ? TreeDetail.Max : TreeDetail.Min);
    
    return tree;
}
```

## Companion Tool: `aura_get_node`

Once agents find a node via `aura_tree`, they need to retrieve full source:

```json
{
  "name": "aura_get_node",
  "description": "Retrieve complete source code for a node found via aura_tree.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workspacePath": { "type": "string" },
      "nodeId": { 
        "type": "string",
        "description": "Node ID from aura_tree (e.g., 'class:src/Services/OrderService.cs:OrderService')"
      }
    },
    "required": ["workspacePath", "nodeId"]
  }
}
```

Returns:
```json
{
  "node_id": "class:src/Services/OrderService.cs:OrderService",
  "name": "OrderService",
  "type": "class",
  "path": "src/Services/OrderService.cs",
  "line_start": 15,
  "line_end": 89,
  "content": "public class OrderService : IOrderService\n{\n    ...\n}",
  "metadata": {
    "signature": "public class OrderService : IOrderService",
    "docstring": "Service for managing orders.",
    "language": "csharp"
  }
}
```

## Files to Change

| File | Change |
|------|--------|
| `src/Aura.Foundation/Rag/IRagService.cs` | Add `GetChunksForTreeAsync` |
| `src/Aura.Foundation/Rag/RagService.cs` | Implement query |
| `src/Aura.Foundation/Rag/ChunkMetadata.cs` | New - typed metadata record |
| `src/Aura.Module.Developer/Services/ITreeBuilderService.cs` | New interface |
| `src/Aura.Module.Developer/Services/TreeBuilderService.cs` | New implementation |
| `src/Aura.Api/Mcp/McpHandler.cs` | Add `aura_tree`, `aura_get_node` |
| TreeSitter ingester | Ensure parent-child relationships stored |

## Acceptance Criteria

- [ ] `aura_tree` returns hierarchical view of codebase
- [ ] Pattern filtering works (file path, class name, function name)
- [ ] `maxDepth` limits traversal correctly
- [ ] `detail=min` returns compact output, `detail=max` includes signatures
- [ ] `aura_get_node` retrieves full source by node_id
- [ ] Works for C# (Roslyn) and TreeSitter-indexed languages
- [ ] Performance: < 100ms for typical workspace

## Agent Workflows

### Exploring a New Codebase
```
1. aura_tree(pattern=".", maxDepth=1)  → See top-level folders
2. aura_tree(pattern="src/Services")   → Drill into services
3. aura_get_node(nodeId="class:...")   → Read specific class
```

### Finding Related Code
```
1. aura_tree(pattern="Order")          → Find all Order-related symbols
2. aura_get_node for each result
```
