# MCP Tools Enhancement

**Status:** Proposed  
**Priority:** High  
**Estimated Effort:** 3-5 days  
**Created:** 2026-01-15

## Overview

Enhance the Aura MCP tools based on real-world usage feedback. The current tools work well for simple queries but fall short on common code exploration patterns that developers need.

## Current State

### Available Tools

| Tool | Purpose | Works Well For |
|------|---------|----------------|
| `aura_search_code` | Semantic search | Conceptual queries |
| `aura_find_implementations` | Interface implementers | Solution-defined interfaces |
| `aura_find_callers` | Method call sites | Specific method names |
| `aura_get_type_members` | Type members | Exploring known types |
| `aura_find_derived_types` | Subclasses | Solution-defined base classes |
| `aura_find_usages` | Symbol references | Solution-defined symbols |
| `aura_list_classes` | Type discovery | Project exploration |
| `aura_validate_compilation` | Build check | After edits |
| `aura_run_tests` | Test execution | Validation |

### Identified Gaps

1. **No return type search** - Can't find "all methods returning `WebApplication`"
2. **No framework type support** - Can't find classes inheriting from `Controller`, `DbContext`
3. **Semantic search noise** - Returns markdown docs when searching for C# types
4. **No extension method discovery** - Can't find `IServiceCollection` extensions
5. **No attribute-based search** - Can't find `[HttpGet]` or `[Test]` methods
6. **RAG and Code Graph are disconnected** - Can't combine semantic + structural queries

---

## Proposed Enhancements

### Phase 1: Quick Wins (Low Effort, High Value)

#### 1.1 Add `contentType` Filter to `aura_search_code`

**Problem:** Searching for "AgentContext" returns markdown docs instead of the C# record.

**Solution:** Add a `contentType` parameter to filter results.

```csharp
// McpHandler.cs - updated tool definition
new McpToolDefinition
{
    Name = "aura_search_code",
    Description = "Semantic search across the indexed codebase. Use contentType to filter results.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "The search query" },
            limit = new { type = "integer", description = "Maximum results (default 10)" },
            contentType = new { 
                type = "string", 
                description = "Filter by content type: 'code', 'docs', 'config', or 'all' (default)",
                @enum = new[] { "code", "docs", "config", "all" }
            }
        },
        required = new[] { "query" }
    }
}
```

**Implementation:**
- RAG chunks already have `SourcePath` - derive content type from file extension
- Code: `.cs`, `.ts`, `.py`, `.rs`, `.go`, `.fs`, `.ps1`, `.js`, `.jsx`, `.tsx`
- Docs: `.md`, `.txt`, `.rst`
- Config: `.json`, `.yaml`, `.yml`, `.xml`, `.toml`

#### 1.2 Boost Exact Symbol Matches

**Problem:** Semantic search weights document structure over exact matches.

**Solution:** Before semantic search, check for exact symbol name matches in the code graph.

```csharp
// In HandleSearchCode
public async Task<object> HandleSearchCode(string query, int limit, string? contentType)
{
    var results = new List<SearchResult>();
    
    // First: Check code graph for exact type/method name match
    var exactMatch = await _codeGraphService.FindNodeByNameAsync(query);
    if (exactMatch is not null)
    {
        results.Add(new SearchResult
        {
            Type = "exact_match",
            Name = exactMatch.Name,
            FilePath = exactMatch.FilePath,
            Line = exactMatch.StartLine,
            Score = 1.0
        });
    }
    
    // Then: Semantic search with content type filter
    var semanticResults = await _ragService.QueryAsync(query, limit, contentType);
    results.AddRange(semanticResults);
    
    return results;
}
```

---

### Phase 2: New Tools (Medium Effort)

#### 2.1 `aura_find_by_attribute`

Find all symbols decorated with a specific attribute.

```csharp
new McpToolDefinition
{
    Name = "aura_find_by_attribute",
    Description = "Find all methods, classes, or properties decorated with a specific attribute.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            attributeName = new { type = "string", description = "Attribute name (e.g., 'HttpGet', 'Test', 'Obsolete')" },
            symbolType = new { 
                type = "string", 
                description = "Filter by symbol type: 'method', 'class', 'property', or 'all' (default)",
                @enum = new[] { "method", "class", "property", "all" }
            }
        },
        required = new[] { "attributeName" }
    }
}
```

**Use Cases:**
- Find all API endpoints: `aura_find_by_attribute("HttpGet")`
- Find all tests: `aura_find_by_attribute("Test")`
- Find deprecated code: `aura_find_by_attribute("Obsolete")`

**Implementation:** Requires indexing attributes in the code graph (currently not stored).

#### 2.2 `aura_find_extension_methods`

Find all extension methods for a given type.

```csharp
new McpToolDefinition
{
    Name = "aura_find_extension_methods",
    Description = "Find all extension methods that extend a given type.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            targetType = new { type = "string", description = "The type being extended (e.g., 'IServiceCollection', 'string')" }
        },
        required = new[] { "targetType" }
    }
}
```

**Use Cases:**
- Find DI registrations: `aura_find_extension_methods("IServiceCollection")`
- Find WebApplication setup: `aura_find_extension_methods("WebApplication")`
- Find string helpers: `aura_find_extension_methods("string")`

**Implementation:** 
- Extension methods are static methods where first parameter has `this` modifier
- Index the extended type name as a searchable field
- Roslyn: `methodSymbol.IsExtensionMethod` and `methodSymbol.Parameters[0].Type.Name`

#### 2.3 `aura_find_methods_by_signature`

Find methods by return type or parameter types.

```csharp
new McpToolDefinition
{
    Name = "aura_find_methods_by_signature",
    Description = "Find methods matching a signature pattern (return type, parameter types).",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            returnType = new { type = "string", description = "Return type to match (e.g., 'Task', 'WebApplication', 'void')" },
            parameterType = new { type = "string", description = "Parameter type to match (any parameter)" },
            containingType = new { type = "string", description = "Optional: filter to methods in this type" }
        }
    }
}
```

**Use Cases:**
- Find async methods: `aura_find_methods_by_signature(returnType: "Task")`
- Find factories: `aura_find_methods_by_signature(returnType: "WebApplication")`
- Find handlers: `aura_find_methods_by_signature(parameterType: "HttpContext")`

---

### Phase 3: Framework Type Support (Medium Effort)

#### 3.1 Enhance `aura_find_derived_types` for External Types

**Problem:** Can't find classes inheriting from `Controller`, `DbContext`, `Exception`.

**Current Limitation:** Only searches for base types defined in the solution.

**Solution:** Check if any type in the solution has a base type name matching the query, even if the base type is external.

```csharp
// Current: Only finds if BaseClass is in solution
// Proposed: Find all types where baseType.Name == "Controller" (regardless of where Controller is defined)

public async Task<IEnumerable<CodeGraphNode>> FindDerivedTypesAsync(string baseClassName)
{
    // Search by base type name, not by resolved symbol
    return await _db.CodeGraphNodes
        .Where(n => n.NodeType == "Class" || n.NodeType == "Record")
        .Where(n => n.BaseTypeName == baseClassName || 
                    n.ImplementedInterfaces.Contains(baseClassName))
        .ToListAsync();
}
```

**Requires:** Storing `BaseTypeName` as a string field in the code graph (not just resolved symbol references).

#### 3.2 Enhance `aura_find_usages` for External Types

**Problem:** Can't find usages of `HttpClient`, `ILogger<T>`, etc.

**Solution:** For types not in the solution, search for the type name as text in the indexed code.

```csharp
public async Task<IEnumerable<UsageResult>> FindUsagesAsync(string symbolName, string? solutionPath)
{
    // First: Try Roslyn symbol resolution (fast, accurate for solution types)
    var roslynResults = await TryFindUsagesViaRoslynAsync(symbolName, solutionPath);
    if (roslynResults.Any())
        return roslynResults;
    
    // Fallback: Text-based search in code files (for external types)
    return await FindUsagesViaTextSearchAsync(symbolName);
}
```

---

### Phase 4: Cross-Reference RAG + Code Graph (Higher Effort)

#### 4.1 Combined Semantic + Structural Query

**Vision:** "Find code related to 'authentication' that implements `IAuthHandler`"

This requires a new query pattern:

```csharp
new McpToolDefinition
{
    Name = "aura_semantic_code_query",
    Description = "Combined semantic + structural code query. Find code matching a concept AND a structural constraint.",
    InputSchema = new
    {
        type = "object",
        properties = new
        {
            concept = new { type = "string", description = "Semantic concept to search for" },
            implementsInterface = new { type = "string", description = "Optional: must implement this interface" },
            inheritsFrom = new { type = "string", description = "Optional: must inherit from this type" },
            hasAttribute = new { type = "string", description = "Optional: must have this attribute" }
        },
        required = new[] { "concept" }
    }
}
```

**Implementation:**
1. Run semantic search to find relevant files/chunks
2. For each result, check if it matches structural constraints via code graph
3. Return intersection

---

## Schema Changes Required

### Code Graph Node Enhancements

```csharp
public class CodeGraphNode
{
    // Existing fields...
    
    // NEW: For attribute search
    public List<string> Attributes { get; set; } = [];
    
    // NEW: For base type search (stores name, not resolved reference)
    public string? BaseTypeName { get; set; }
    
    // NEW: For extension method search
    public bool IsExtensionMethod { get; set; }
    public string? ExtendedTypeName { get; set; }
    
    // NEW: For signature search
    public string? ReturnTypeName { get; set; }
    public List<string> ParameterTypeNames { get; set; } = [];
}
```

### RAG Chunk Enhancements

```csharp
public class RagChunk
{
    // Existing fields...
    
    // NEW: For content type filtering
    public ContentCategory ContentCategory { get; set; } // Code, Docs, Config
    
    // NEW: For symbol boosting
    public List<string> ContainedSymbols { get; set; } = []; // Type/method names in chunk
}

public enum ContentCategory
{
    Code,
    Docs,
    Config,
    Other
}
```

---

## Implementation Plan

### Phase 1: Quick Wins (Day 1-2)

| Task | Effort | Files |
|------|--------|-------|
| Add `contentType` filter to `aura_search_code` | 2h | `McpHandler.cs`, RAG query |
| Derive content category from file extension | 1h | `RagService.cs` |
| Add exact symbol match boost | 3h | `McpHandler.cs`, `ICodeGraphService` |
| Update tool descriptions for clarity | 1h | `McpHandler.cs` |

### Phase 2: New Tools (Day 2-4)

| Task | Effort | Files |
|------|--------|-------|
| Index attributes in code graph | 4h | Roslyn indexer |
| Add `aura_find_by_attribute` tool | 2h | `McpHandler.cs` |
| Index extension method metadata | 3h | Roslyn indexer |
| Add `aura_find_extension_methods` tool | 2h | `McpHandler.cs` |
| Index return/parameter types | 3h | Roslyn indexer |
| Add `aura_find_methods_by_signature` tool | 2h | `McpHandler.cs` |

### Phase 3: Framework Types (Day 4-5)

| Task | Effort | Files |
|------|--------|-------|
| Store base type name as string | 2h | Schema, Roslyn indexer |
| Update `aura_find_derived_types` | 2h | `McpHandler.cs` |
| Add text fallback to `aura_find_usages` | 3h | `McpHandler.cs` |

---

## Success Criteria

| Scenario | Current | Target |
|----------|---------|--------|
| "Find all API endpoints" | ❌ Manual grep | ✅ `aura_find_by_attribute("HttpGet")` |
| "Find Task<T> returning methods" | ❌ Not possible | ✅ `aura_find_methods_by_signature(returnType: "Task")` |
| "Find IServiceCollection extensions" | ❌ Not possible | ✅ `aura_find_extension_methods("IServiceCollection")` |
| "Find code about 'caching'" | ⚠️ Returns docs | ✅ `aura_search_code(query: "caching", contentType: "code")` |
| "Find DbContext subclasses" | ❌ External type | ✅ `aura_find_derived_types("DbContext")` |
| "Find AgentContext type" | ⚠️ Semantic miss | ✅ Exact match boost returns record |

---

## Out of Scope

- Multi-language support for new tools (C# first, others later)
- Real-time incremental indexing (batch reindex for now)
- Natural language query translation ("find all endpoints" → attribute search)

---

## References

- [ADR-012: Tool-Using Agents](../adr/012-tool-using-agents.md)
- [Code Graph Indexing](../spec/code-graph-indexing.md)
- [MCP Protocol Spec](https://modelcontextprotocol.io/)
