# MCP Tools Enhancement

**Status:** ⏳ In Progress (Phases 1-6 Complete)  
**Priority:** High  
**Estimated Effort:** 3-5 days (2 days remaining)  
**Created:** 2026-01-15  
**Updated:** 2026-01-15

> **Progress:** Phases 1-6 implemented on 2026-01-15. See commit `6db5ea6` (Roslyn) and `0c3b48f` (Python).

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

## Phase 5: Roslyn Code Editing & Refactoring Tools

### Executive Summary

Add Roslyn-powered code editing tools to the Aura MCP server, enabling AI agents to perform safe, semantically-aware code modifications. Unlike text-based edits, these tools understand code structure and automatically propagate changes across the codebase.

### Problem Statement

Current AI-assisted editing relies on `replace_string_in_file` which:

- Requires exact string matching (whitespace-sensitive, fragile)
- Cannot propagate changes to callers/references
- Misses overloads, partial classes, and cross-file references
- Has no understanding of syntax — can create invalid code
- Forces agents to make multiple sequential edits with high failure rates

| Text-based Editing | Roslyn-based Editing |
|-------------------|---------------------|
| Matches exact strings | Understands syntax |
| Misses overloads | Finds all overloads |
| Can break code | Preserves correctness |
| Manual caller updates | Automatic propagation |
| Whitespace sensitive | Format-aware |

### Current Workflow Gap

| Step | Tool | Works? |
|------|------|--------|
| 1. Understand context | `aura_get_type_members`, `aura_find_callers` | ✅ Yes |
| 2. Find what to change | `aura_find_usages`, `aura_search_code` | ⚠️ Partial |
| 3. Make the edit | `replace_string_in_file` | ❌ Fragile |
| 4. Validate | `aura_validate_compilation`, `aura_run_tests` | ✅ Yes |

---

### Category 1: Symbol Renaming

#### `aura_rename_symbol`

Rename any symbol (type, method, property, field, variable, parameter) across the entire solution.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `symbolName` | string | ✅ | Current name of the symbol |
| `newName` | string | ✅ | New name for the symbol |
| `containingType` | string | ❌ | Disambiguate when multiple symbols have same name |
| `symbolKind` | enum | ❌ | Type, Method, Property, Field, Parameter, Variable |
| `solutionPath` | string | ❌ | Path to .sln file (default: auto-detect) |
| `includeComments` | bool | ❌ | Update references in XML docs and comments (default: true) |
| `includeStrings` | bool | ❌ | Update string literals containing the name (default: false) |
| `preview` | bool | ❌ | Return changes without applying (default: false) |

**Returns:**
```json
{
  "success": true,
  "filesModified": ["src/Services/UserService.cs", "src/Controllers/UserController.cs"],
  "changesApplied": 15,
  "details": [
    { "file": "src/Services/UserService.cs", "line": 42, "change": "GetUser → GetUserAsync" }
  ]
}
```

**Must handle:**
- Overloaded methods (rename all or specific overload)
- Partial classes across files
- Interface implementations (rename interface member → rename implementations)
- Cascade to derived classes
- `nameof()` expressions
- XML doc `<see cref="..."/>` references
- Constructor names (when renaming class)
- Property backing fields

---

### Category 2: Signature Changes

#### `aura_change_method_signature`

Modify method parameters, return type, or modifiers with automatic caller updates.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `methodName` | string | ✅ | Method to modify |
| `containingType` | string | ✅ | Type containing the method |
| `newReturnType` | string | ❌ | New return type (e.g., `Task<Result<Order>>`) |
| `addParameters` | array | ❌ | Parameters to add: `[{ name, type, defaultValue?, position? }]` |
| `removeParameters` | array | ❌ | Parameter names to remove |
| `reorderParameters` | array | ❌ | New parameter order by name |
| `addModifiers` | array | ❌ | `["async", "static", "virtual"]` |
| `removeModifiers` | array | ❌ | Modifiers to remove |
| `solutionPath` | string | ❌ | Path to .sln file |

**Example — Add CancellationToken:**
```json
{
  "methodName": "GetOrderAsync",
  "containingType": "OrderService",
  "addParameters": [
    { "name": "ct", "type": "CancellationToken", "defaultValue": "default" }
  ]
}
```

**Must handle:**
- Update all call sites with new arguments
- Default values for new parameters
- Interface member → update all implementations
- Virtual/override chains
- Expression-bodied members
- Lambda invocations

#### `aura_change_property_type`

Change a property's type and update usages.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `propertyName` | string | ✅ | Property to modify |
| `containingType` | string | ✅ | Type containing the property |
| `newType` | string | ✅ | New type (e.g., `int` → `int?`) |
| `addConversion` | bool | ❌ | Insert conversion at usages if possible |

---

### Category 3: Code Generation

#### `aura_implement_interface`

Generate method stubs for interface implementation.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to add implementation to |
| `interfaceName` | string | ✅ | Interface to implement |
| `explicit` | bool | ❌ | Use explicit implementation (default: false) |
| `throwNotImplemented` | bool | ❌ | Body throws NotImplementedException (default: true) |
| `addInterfaceToType` | bool | ❌ | Add `: IInterface` to type declaration (default: true) |

**Returns:**
```json
{
  "success": true,
  "membersGenerated": ["GetAsync", "CreateAsync", "DeleteAsync"],
  "filesModified": ["src/Services/OrderService.cs"]
}
```

#### `aura_generate_constructor`

Generate constructor with field/property initialization.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to generate constructor for |
| `includeFields` | bool | ❌ | Include private fields (default: true) |
| `includeProperties` | bool | ❌ | Include properties (default: false) |
| `memberNames` | array | ❌ | Specific members to include |
| `usePrimaryConstructor` | bool | ❌ | Use C# 12 primary constructor syntax |

**Example output:**
```csharp
public OrderService(IOrderRepository repository, ILogger<OrderService> logger)
{
    _repository = repository;
    _logger = logger;
}
```

#### `aura_generate_equality_members`

Generate `Equals`, `GetHashCode`, `==`, `!=` operators.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to generate for |
| `memberNames` | array | ❌ | Members to include in equality (default: all) |
| `implementIEquatable` | bool | ❌ | Add `IEquatable<T>` (default: true) |

#### `aura_add_property`

Add a property to a type with correct formatting.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to add property to |
| `propertyName` | string | ✅ | Name of the property |
| `propertyType` | string | ✅ | Type of the property |
| `accessors` | string | ❌ | `get`, `set`, `init`, `get;set`, `get;init` (default: `get;set`) |
| `accessibility` | string | ❌ | `public`, `internal`, `protected`, `private` |
| `defaultValue` | string | ❌ | Initial value |
| `xmlDoc` | string | ❌ | Summary documentation |
| `attributes` | array | ❌ | Attributes: `["Required", "JsonPropertyName(\"name\")"]` |

**Must handle:**
- Records (use positional or property syntax appropriately)
- Record structs
- Classes, structs, interfaces
- Partial types (choose correct file)

#### `aura_add_method`

Add a method to a type.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to add method to |
| `methodName` | string | ✅ | Name of the method |
| `returnType` | string | ✅ | Return type |
| `parameters` | array | ❌ | `[{ name, type, defaultValue? }]` |
| `body` | string | ❌ | Method body (without braces) |
| `accessibility` | string | ❌ | Default: `public` |
| `modifiers` | array | ❌ | `["async", "static", "virtual", "override"]` |
| `xmlDoc` | string | ❌ | Summary documentation |
| `attributes` | array | ❌ | Attributes to apply |

---

### Category 4: Extraction & Reorganization

#### `aura_extract_interface`

Create an interface from a class's public members.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Source type |
| `interfaceName` | string | ✅ | Name for new interface (e.g., `IOrderService`) |
| `targetFile` | string | ❌ | File path for interface (default: same file) |
| `memberNames` | array | ❌ | Specific members to include (default: all public) |
| `addToType` | bool | ❌ | Add `: IInterface` to source type (default: true) |

#### `aura_extract_method`

Extract code block into a new method.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filePath` | string | ✅ | File containing the code |
| `startLine` | int | ✅ | Start line of code to extract |
| `endLine` | int | ✅ | End line of code to extract |
| `methodName` | string | ✅ | Name for new method |
| `makeStatic` | bool | ❌ | Make method static if possible (default: auto-detect) |

**Must handle:**
- Detect required parameters from captured variables
- Detect return type from returned/assigned values
- Handle multiple return values (tuples or out parameters)

#### `aura_move_type`

Move a type to a different file and/or namespace.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to move |
| `targetFile` | string | ✅ | Destination file path |
| `targetNamespace` | string | ❌ | New namespace (default: infer from folder) |
| `updateReferences` | bool | ❌ | Add using statements where needed (default: true) |

#### `aura_move_member`

Move a member (method, property) to a different type.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `memberName` | string | ✅ | Member to move |
| `sourceType` | string | ✅ | Current containing type |
| `targetType` | string | ✅ | Destination type |
| `updateCallers` | bool | ❌ | Update call sites (default: true) |

---

### Category 5: Code Cleanup

#### `aura_remove_unused_usings`

Remove unused using directives.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filePath` | string | ❌ | Specific file (default: entire solution) |
| `projectName` | string | ❌ | Specific project |

#### `aura_sort_members`

Reorder type members by convention.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | ✅ | Type to sort |
| `order` | array | ❌ | Member kind order: `["Fields", "Constructors", "Properties", "Methods"]` |
| `sortAlphabetically` | bool | ❌ | Alphabetize within groups (default: false) |

#### `aura_apply_code_fix`

Apply a specific Roslyn analyzer code fix.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `diagnosticId` | string | ✅ | e.g., `CS8600`, `CA1822`, `IDE0051` |
| `filePath` | string | ❌ | Specific file (default: entire solution) |
| `preview` | bool | ❌ | Return changes without applying |

---

### Category 6: Safe Delete

#### `aura_safe_delete`

Remove a symbol only if it has no usages.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `symbolName` | string | ✅ | Symbol to delete |
| `containingType` | string | ❌ | For members |
| `symbolKind` | enum | ❌ | Type, Method, Property, Field, Parameter |
| `force` | bool | ❌ | Delete even with usages, also removing usages (default: false) |

**Returns:**
```json
{
  "success": false,
  "blocked": true,
  "usages": [
    { "file": "src/Program.cs", "line": 42, "context": "var svc = new UserService();" }
  ],
  "message": "Symbol has 3 usages. Use force: true to delete anyway."
}
```

---

### Cross-Cutting Requirements

#### Preview Mode

All mutating tools must support `preview: true` which returns the changes without applying them:

```json
{
  "preview": true,
  "changes": [
    {
      "file": "src/Services/UserService.cs",
      "hunks": [
        { "startLine": 42, "oldLines": ["public User GetUser(int id)"], "newLines": ["public User GetUserAsync(int id)"] }
      ]
    }
  ]
}
```

#### Undo Support

Consider returning an undo token that can revert the change:

```json
{
  "success": true,
  "undoToken": "ref:abc123",
  "message": "Call aura_undo with this token to revert"
}
```

#### Formatting

All generated code must:
- Follow `.editorconfig` if present
- Use consistent indentation with surrounding code
- Apply `dotnet format` rules

#### Error Handling

Return structured errors:

```json
{
  "success": false,
  "error": {
    "code": "SYMBOL_NOT_FOUND",
    "message": "No symbol named 'GetUser' found in type 'UserService'",
    "suggestions": ["GetUserAsync", "GetUserById"]
  }
}
```

#### Compilation Validation

All tools should optionally validate the solution still compiles after changes:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `validateCompilation` | bool | true | Run compilation check after edit |
| `rollbackOnError` | bool | true | Revert changes if compilation fails |

---

### Priority Ranking

| Priority | Tool | Rationale |
|----------|------|-----------|
| **P0** | `aura_rename_symbol` | Most common refactoring, high error rate with text edits |
| **P0** | `aura_change_method_signature` | Adding parameters is frequent and error-prone |
| **P1** | `aura_implement_interface` | Common task, pure generation |
| **P1** | `aura_generate_constructor` | DI constructors are boilerplate |
| **P1** | `aura_add_property` | Records especially need syntax-aware insertion |
| **P1** | `aura_add_method` | Safer than text insertion |
| **P2** | `aura_extract_interface` | Less common but high value |
| **P2** | `aura_extract_method` | Complex but valuable |
| **P2** | `aura_move_type` | Reorganization support |
| **P2** | `aura_safe_delete` | Prevents breaking changes |
| **P3** | `aura_change_property_type` | Useful but less frequent |
| **P3** | `aura_generate_equality_members` | Nice to have |
| **P3** | `aura_remove_unused_usings` | Nice to have |
| **P3** | `aura_sort_members` | Style preference |
| **P3** | `aura_apply_code_fix` | Leverages existing analyzers |
| **P3** | `aura_move_member` | Complex, less common |

---

### Implementation Notes

#### Roslyn APIs to Leverage

- `Microsoft.CodeAnalysis.Rename.Renamer`
- `Microsoft.CodeAnalysis.CodeFixes`
- `Microsoft.CodeAnalysis.CodeRefactorings`
- `Microsoft.CodeAnalysis.Editing.SyntaxEditor`
- `Microsoft.CodeAnalysis.Formatting.Formatter`

#### Implementation Pattern

```csharp
public async Task<RefactoringResult> RenameSymbolAsync(
    string symbolName, 
    string newName, 
    string? containingType,
    string solutionPath,
    CancellationToken ct)
{
    // 1. Open the solution
    using var workspace = MSBuildWorkspace.Create();
    var solution = await workspace.OpenSolutionAsync(solutionPath, ct);
    
    // 2. Find the symbol
    var symbol = await FindSymbolAsync(solution, symbolName, containingType, ct);
    if (symbol is null)
        return RefactoringResult.Fail($"Symbol '{symbolName}' not found");
    
    // 3. Perform the rename
    var newSolution = await Renamer.RenameSymbolAsync(
        solution, 
        symbol, 
        new SymbolRenameOptions(), 
        newName, 
        ct);
    
    // 4. Apply changes to disk
    var changedDocs = GetChangedDocuments(solution, newSolution);
    foreach (var doc in changedDocs)
    {
        var text = await doc.GetTextAsync(ct);
        await File.WriteAllTextAsync(doc.FilePath!, text.ToString(), ct);
    }
    
    // 5. Return summary
    return RefactoringResult.Success(
        $"Renamed '{symbolName}' to '{newName}' in {changedDocs.Count} files",
        changedDocs.Select(d => d.FilePath!).ToList());
}
```

#### Existing Patterns in Aura

The current `aura_validate_compilation` shows the pattern — load the solution, perform analysis, return results. These tools extend that to also apply changes.

#### Workspace Management

Consider caching the `MSBuildWorkspace` across tool invocations to avoid repeated solution loading.

---

### Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Edit success rate | ~70% (text-based) | 95%+ |
| Edits requiring manual fix | ~30% | <5% |
| Multi-file refactoring time | 5-10 tool calls | 1 tool call |
| Compilation errors introduced | Frequent | Rare |

---

## Multi-Language Support Strategy

Phase 5 focuses on Roslyn for .NET languages, but the architecture should support other languages over time.

### Technology Stack by Language

| Language | Semantic Engine | Refactoring Capability | Status |
|----------|-----------------|----------------------|--------|
| **C#** | Roslyn | Full (rename, signature, extract, generate) | Phase 5 |
| **F#** | Roslyn (FSharp.Compiler.Service) | Full | Phase 5 |
| **VB.NET** | Roslyn | Full | Phase 5 |
| **TypeScript/JavaScript** | `ts-morph` (TS Compiler API) | Full | Future |
| **Python** | `rope` or `jedi` | Rename, extract, references | Future |
| **Go** | `gopls` (LSP) | Rename, references | Future |
| **Rust** | `rust-analyzer` (LSP) | Rename, references | Future |
| **Other** | LSP `workspace/rename` | Rename only | Future |

### Role of Each Technology

```
┌─────────────────────────────────────────────────────────────────┐
│                        Aura MCP Tools                           │
├─────────────────────────────────────────────────────────────────┤
│  Discovery Layer (all languages)                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Tree-sitter: Parse → AST → Symbol locations            │   │
│  │  - Find symbols in file                                  │   │
│  │  - Query syntax patterns                                 │   │
│  │  - Fast, incremental parsing                             │   │
│  └─────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│  Semantic Layer (language-specific)                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │   Roslyn     │  │   ts-morph   │  │   LSP Servers        │  │
│  │   (.NET)     │  │   (TS/JS)    │  │   (Go, Rust, etc.)   │  │
│  ├──────────────┤  ├──────────────┤  ├──────────────────────┤  │
│  │ • Type info  │  │ • Type info  │  │ • References         │  │
│  │ • References │  │ • References │  │ • Rename             │  │
│  │ • Refactor   │  │ • Refactor   │  │ • (limited)          │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### What Tree-sitter Cannot Do

Tree-sitter is a parser, not a semantic analyzer. It provides:

| ✅ Tree-sitter Can | ❌ Tree-sitter Cannot |
|-------------------|----------------------|
| Find all method declarations | Know what type a variable has |
| Locate symbol by name in file | Find all references across files |
| Query syntax patterns | Determine if rename causes conflict |
| Fast incremental re-parse | Resolve imports/dependencies |

**Key insight:** Tree-sitter powers *discovery* (Phases 1-4), semantic engines power *editing* (Phase 5+).

### LSP as Universal Fallback

For languages without dedicated support, LSP provides baseline refactoring:

```typescript
// LSP requests we can use
interface LspRefactoringCapabilities {
  // Available in most language servers
  "textDocument/rename": true,           // Rename symbol
  "textDocument/references": true,       // Find all references
  "textDocument/definition": true,       // Go to definition
  
  // Available in some language servers
  "textDocument/codeAction": "varies",   // Quick fixes, extracts
  "workspace/applyEdit": true,           // Apply multi-file edits
}
```

**Implementation approach:**
1. Detect language from file extension
2. Check if we have a native semantic engine (Roslyn, ts-morph)
3. Fall back to LSP if available
4. Fall back to text-based (current behavior) as last resort

### Phased Rollout

| Phase | Languages | Engine |
|-------|-----------|--------|
| Phase 5 | C#, F#, VB.NET | Roslyn |
| Phase 6 | Python | rope/jedi |
| Phase 7 | TypeScript, JavaScript | ts-morph |
| Phase 8 | Go, Rust, Java | LSP integration |

### Tool Naming Convention

Tools should indicate language scope in descriptions:

```
aura_rename_symbol
  Description: "Rename a symbol and update all references. 
                Supports: C#, F#, VB.NET (Roslyn), Python (rope).
                Other languages: basic rename via LSP if available."
```

This allows the AI to understand capabilities and choose the right tool.

---

## Implementation Roadmap

This roadmap covers the path from current state through Phase 5 (Roslyn refactoring) and Phase 6 (Python).

### Current State (January 2026)

- ✅ 12 MCP tools available (search, find implementations, callers, etc.)
- ✅ Tree-sitter indexing for 13 languages
- ✅ RAG semantic search
- ✅ Code graph with relationships
- ❌ No content type filtering on search
- ❌ No framework type support (inheriting from `Controller`, etc.)
- ❌ No refactoring tools (rename, signature, etc.)

### Milestone 1: Discovery Improvements (Phases 1-4)

**Duration:** 1 week  
**Goal:** Make existing tools more useful before adding new ones

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | Add `contentType` filter to `aura_search_code` | Filter code/docs/config |
| 1 | Boost exact symbol name matches | Code graph lookup before RAG |
| 2 | Add `aura_find_by_attribute` tool | Find `[HttpGet]`, `[Test]`, etc. |
| 3 | Add `aura_find_extension_methods` tool | Find extensions for `IServiceCollection` |
| 4 | Extend `aura_find_derived_types` for framework types | Index NuGet package type hierarchies |
| 5 | Add `aura_find_by_return_type` tool | Find methods returning `WebApplication` |

**Acceptance Criteria:**
- [ ] `aura_search_code("AgentContext", contentType: "code")` returns C# record, not markdown
- [ ] `aura_find_by_attribute("[HttpGet]")` finds all HTTP GET endpoints
- [ ] `aura_find_extension_methods("IServiceCollection")` finds all `AddX()` methods
- [ ] `aura_find_derived_types("Controller")` finds all controllers

### Milestone 2: Roslyn Foundation (Phase 5.1)

**Duration:** 3 days  
**Goal:** Establish the Roslyn refactoring infrastructure

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | Create `RoslynRefactoringService` | MSBuildWorkspace management, solution loading |
| 1 | Symbol lookup by name | Find symbol across solution |
| 2 | `aura_rename_symbol` tool | Rename with all references updated |
| 2 | Preview mode support | Return changes without applying |
| 3 | Integration tests | Rename across projects, edge cases |

**Key Code:**
```csharp
// src/Aura.Module.Developer/Services/RoslynRefactoringService.cs
public class RoslynRefactoringService : IRoslynRefactoringService
{
    public async Task<RefactoringResult> RenameSymbolAsync(
        string symbolName,
        string newName,
        string? containingType,
        string solutionPath,
        bool preview,
        CancellationToken ct);
}
```

**Acceptance Criteria:**
- [ ] Rename a method and all 50+ call sites in one operation
- [ ] Preview mode shows affected files without writing
- [ ] Handles overloads correctly (only renames the specified one)

### Milestone 3: Signature Changes (Phase 5.2)

**Duration:** 3 days  
**Goal:** Safe parameter modifications

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | `aura_change_method_signature` tool | Add/remove/reorder parameters |
| 1 | Default value injection | Existing call sites get default |
| 2 | `aura_change_property_type` tool | Change property types safely |
| 2 | Cascading updates | Update derived classes/implementations |
| 3 | Integration tests | Complex inheritance scenarios |

**Acceptance Criteria:**
- [ ] Add `CancellationToken ct = default` to method, all callers updated
- [ ] Remove unused parameter, all call sites cleaned up
- [ ] Change property from `string` to `int`, compilation validates

### Milestone 4: Code Generation (Phase 5.3)

**Duration:** 4 days  
**Goal:** Generate boilerplate correctly

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | `aura_implement_interface` tool | Generate interface stubs |
| 1 | Explicit vs implicit implementation | Option to choose style |
| 2 | `aura_generate_constructor` tool | Initialize readonly fields |
| 2 | Primary constructor support | .NET 8+ primary ctors |
| 3 | `aura_add_property` tool | Add with backing field or auto |
| 3 | `aura_add_method` tool | Add method skeleton |
| 4 | `aura_generate_equality_members` | Equals, GetHashCode, operators |

**Acceptance Criteria:**
- [ ] Implement `IDisposable` adds pattern-correct `Dispose()` method
- [ ] Generate constructor from 8 readonly fields in one call
- [ ] Add property with validation in setter

### Milestone 5: Extraction & Reorganization (Phase 5.4)

**Duration:** 4 days  
**Goal:** Restructure code safely

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | `aura_extract_interface` tool | Create interface from class |
| 1 | Member selection | Choose which members to include |
| 2 | `aura_extract_method` tool | Extract code block to method |
| 2 | Variable capture | Determine parameters and return |
| 3 | `aura_move_type` tool | Move class to new file/namespace |
| 3 | Update using statements | Fix references across solution |
| 4 | `aura_move_member` tool | Move method/property between types |

**Acceptance Criteria:**
- [ ] Extract interface, original class now implements it
- [ ] Extract 20-line block to method, correct parameters inferred
- [ ] Move class to new namespace, all usings updated

### Milestone 6: Cleanup & Safe Delete (Phase 5.5)

**Duration:** 2 days  
**Goal:** Code cleanup tools

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | `aura_safe_delete` tool | Remove unused symbol with validation |
| 1 | Reference check | Error if still referenced |
| 2 | `aura_remove_unused_usings` tool | Clean up imports |
| 2 | `aura_sort_members` tool | Organize type members |
| 2 | `aura_apply_code_fix` tool | Apply compiler suggestions |

**Acceptance Criteria:**
- [ ] Safe delete fails with error listing 3 remaining references
- [ ] Remove unused usings from 50 files in one operation
- [ ] Apply "make readonly" suggestion to field

### Milestone 7: Python Support (Phase 6)

**Duration:** 5 days  
**Goal:** Refactoring for Python projects

| Day | Task | Deliverable |
|-----|------|-------------|
| 1 | Integrate `rope` library | Python refactoring engine |
| 1 | Create `PythonRefactoringService` | Service abstraction |
| 2 | `aura_rename_symbol` Python support | Rename with references |
| 3 | `aura_extract_method` Python support | Extract function |
| 4 | `aura_change_signature` Python support | Modify parameters |
| 5 | Integration tests | pytest-based validation |

**Acceptance Criteria:**
- [ ] Rename Python class across 10 files
- [ ] Extract Python function with correct parameter capture
- [ ] Tools report "Python" in capability description

---

### Timeline Summary

```
Week 1: Discovery Improvements (Phases 1-4)
        ├── Day 1-2: contentType filter, exact match boost, attributes
        ├── Day 3-4: extension methods, framework types
        └── Day 5: return type search

Week 2: Roslyn Foundation + Signatures (Phase 5.1-5.2)
        ├── Day 1-3: RoslynRefactoringService, rename_symbol
        └── Day 4-5: change_signature, change_property_type

Week 3: Code Generation + Extraction (Phase 5.3-5.4)
        ├── Day 1-2: implement_interface, generate_constructor, add_property/method
        ├── Day 3-4: extract_interface, extract_method
        └── Day 5: move_type, move_member

Week 4: Cleanup + Python (Phase 5.5 + Phase 6)
        ├── Day 1-2: safe_delete, remove_usings, apply_code_fix
        └── Day 3-5: Python rope integration
```

### Dependencies

| Component | Dependency | Notes |
|-----------|------------|-------|
| Roslyn workspace | `Microsoft.CodeAnalysis.Workspaces.MSBuild` | Already referenced |
| Symbol lookup | Existing code graph | Reuse for symbol location |
| Python refactoring | `rope` (Python package) | Subprocess or IPC |

### Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| MSBuildWorkspace slow on large solutions | High | Cache workspace, incremental updates |
| Roslyn API version conflicts | Medium | Pin to known-good version |
| Python subprocess unreliable | Medium | Fallback to text-based editing |
| Framework type resolution | Medium | NuGet package pre-indexing (Phase 4) |

---

## References

- [ADR-012: Tool-Using Agents](../adr/012-tool-using-agents.md)
- [Code Graph Indexing](../spec/code-graph-indexing.md)
- [MCP Protocol Spec](https://modelcontextprotocol.io/)
