# API Reference

Complete MCP tool schemas and REST endpoint reference.

## MCP Protocol

- **Protocol version:** `2024-11-05`
- **Server name:** `Aura`
- **Endpoint:** `POST /mcp` (JSON-RPC over SSE)

### JSON-RPC Request Format

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "tool_name",
    "arguments": { ... }
  }
}
```

### Discovery

```json
{ "jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {} }
```

---

## Tool Schemas

### aura_search

```json
{
  "name": "aura_search",
  "description": "Semantic search across the indexed codebase. Returns relevant code chunks with file paths and similarity scores. Exact symbol matches are boosted.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": { "type": "string", "description": "The search query (concept, symbol name, or keyword)" },
      "workspacePath": { "type": "string", "description": "Path to the current workspace or worktree" },
      "workspaces": { "type": "array", "items": { "type": "string" }, "description": "Workspace IDs or aliases to search. Use ['*'] for all." },
      "limit": { "type": "integer", "description": "Maximum results (default 10)" },
      "contentType": { "type": "string", "enum": ["code", "docs", "config", "all"], "description": "Filter by content type" }
    },
    "required": ["query"]
  }
}
```

### aura_navigate

```json
{
  "name": "aura_navigate",
  "description": "Find code elements and their relationships: callers, implementations, derived types, usages, references.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["callers", "implementations", "derived_types", "usages", "by_attribute", "extension_methods", "by_return_type", "references", "definition"] },
      "symbolName": { "type": "string", "description": "Symbol name to navigate from" },
      "containingType": { "type": "string", "description": "Type containing the symbol (for disambiguation)" },
      "solutionPath": { "type": "string", "description": "Path to .sln file — required for C#" },
      "filePath": { "type": "string", "description": "Path to file — required for Python" },
      "offset": { "type": "integer", "description": "Character offset — required for Python" },
      "projectPath": { "type": "string", "description": "Project root — required for Python/TS" },
      "attributeName": { "type": "string", "description": "Attribute name for by_attribute" },
      "targetType": { "type": "string", "description": "Target type for extension_methods or by_return_type" },
      "targetKind": { "type": "string", "enum": ["method", "class", "property", "all"], "description": "Filter by symbol kind" }
    },
    "required": ["operation"]
  }
}
```

### aura_inspect

```json
{
  "name": "aura_inspect",
  "description": "Examine code structure: type members, class listings, project exploration.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["type_members", "list_types"] },
      "typeName": { "type": "string", "description": "Type name for type_members" },
      "solutionPath": { "type": "string", "description": "Path to .sln — enables Roslyn" },
      "projectPath": { "type": "string", "description": "Project root for TS/Python" },
      "projectName": { "type": "string", "description": "Project name for list_types" },
      "namespaceFilter": { "type": "string", "description": "Partial namespace match" },
      "nameFilter": { "type": "string", "description": "Partial type name match" }
    },
    "required": ["operation"]
  }
}
```

### aura_tree

```json
{
  "name": "aura_tree",
  "description": "Explore codebase hierarchically: list files/types/members, or get full source for a node.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workspacePath": { "type": "string", "description": "Path to the workspace root" },
      "operation": { "type": "string", "enum": ["explore", "get_node"], "description": "Default: explore" },
      "pattern": { "type": "string", "description": "Filter pattern (default: '.')" },
      "maxDepth": { "type": "integer", "description": "1=files, 2=+types, 3=+members (default 2)" },
      "detail": { "type": "string", "enum": ["min", "max"] },
      "nodeId": { "type": "string", "description": "Node ID for get_node" }
    },
    "required": ["workspacePath"]
  }
}
```

### aura_refactor

```json
{
  "name": "aura_refactor",
  "description": "Transform existing code: rename symbols, change signatures, extract methods/variables/interfaces, safe delete, move type to file.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["rename", "change_signature", "extract_interface", "extract_method", "extract_variable", "safe_delete", "move_type_to_file", "move_members_to_partial"] },
      "symbolName": { "type": "string" },
      "newName": { "type": "string" },
      "containingType": { "type": "string" },
      "solutionPath": { "type": "string" },
      "filePath": { "type": "string" },
      "projectPath": { "type": "string" },
      "offset": { "type": "integer" },
      "startOffset": { "type": "integer" },
      "endOffset": { "type": "integer" },
      "className": { "type": "string" },
      "memberNames": { "type": "array", "items": { "type": "string" } },
      "members": { "type": "array", "items": { "type": "string" } },
      "targetFileName": { "type": "string" },
      "targetDirectory": { "type": "string" },
      "addParameters": { "type": "array", "items": { "type": "object", "properties": { "name": { "type": "string" }, "type": { "type": "string" }, "defaultValue": { "type": "string" } } } },
      "removeParameters": { "type": "array", "items": { "type": "string" } },
      "analyze": { "type": "boolean", "description": "Blast radius analysis only (default: true)" },
      "preview": { "type": "boolean", "description": "Return changes without applying (default: false)" },
      "validate": { "type": "boolean", "description": "Build after refactoring (default: false)" }
    },
    "required": ["operation"]
  }
}
```

### aura_generate

```json
{
  "name": "aura_generate",
  "description": "Generate new code: create types, implement interfaces, generate constructors, add properties/methods, generate tests.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["implement_interface", "constructor", "property", "method", "create_type", "tests"] },
      "solutionPath": { "type": "string" },
      "className": { "type": "string" },
      "typeName": { "type": "string" },
      "typeKind": { "type": "string", "enum": ["class", "interface", "record", "struct"] },
      "targetDirectory": { "type": "string" },
      "baseClass": { "type": "string" },
      "implements": { "type": "array", "items": { "type": "string" } },
      "isSealed": { "type": "boolean" },
      "isAbstract": { "type": "boolean" },
      "isStatic": { "type": "boolean" },
      "documentationSummary": { "type": "string" },
      "primaryConstructorParameters": { "type": "array", "items": { "type": "object", "properties": { "name": { "type": "string" }, "type": { "type": "string" }, "defaultValue": { "type": "string" } } } },
      "typeParameters": { "type": "array", "items": { "type": "object", "properties": { "name": { "type": "string" }, "constraints": { "type": "array", "items": { "type": "string" } } } } },
      "interfaceName": { "type": "string" },
      "explicitImplementation": { "type": "boolean" },
      "members": { "type": "array", "items": { "type": "string" } },
      "propertyName": { "type": "string" },
      "propertyType": { "type": "string" },
      "accessModifier": { "type": "string" },
      "hasGetter": { "type": "boolean" },
      "hasSetter": { "type": "boolean" },
      "hasInit": { "type": "boolean" },
      "isRequired": { "type": "boolean" },
      "initialValue": { "type": "string" },
      "isField": { "type": "boolean" },
      "isReadonly": { "type": "boolean" },
      "methodName": { "type": "string" },
      "returnType": { "type": "string" },
      "parameters": { "type": "array", "items": { "type": "object", "properties": { "name": { "type": "string" }, "type": { "type": "string" }, "defaultValue": { "type": "string" } } } },
      "methodModifier": { "type": "string", "enum": ["virtual", "override", "abstract", "sealed", "new"] },
      "isAsync": { "type": "boolean" },
      "isExtension": { "type": "boolean" },
      "body": { "type": "string" },
      "attributes": { "type": "array", "items": { "type": "object", "properties": { "name": { "type": "string" }, "arguments": { "type": "array", "items": { "type": "string" } } } } },
      "documentation": { "type": "string" },
      "testAttribute": { "type": "string" },
      "target": { "type": "string", "description": "Test target: class, Class.Method, or namespace" },
      "count": { "type": "integer" },
      "maxTests": { "type": "integer", "description": "Default: 20" },
      "focus": { "type": "string", "enum": ["all", "happy_path", "edge_cases", "error_handling"] },
      "testFramework": { "type": "string" },
      "outputDirectory": { "type": "string" },
      "analyzeOnly": { "type": "boolean" },
      "validateCompilation": { "type": "boolean" },
      "preview": { "type": "boolean" }
    },
    "required": ["operation"]
  }
}
```

### aura_validate

```json
{
  "name": "aura_validate",
  "description": "Validate code: check compilation, run tests.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["compilation", "tests"] },
      "solutionPath": { "type": "string" },
      "projectName": { "type": "string" },
      "projectPath": { "type": "string" },
      "includeWarnings": { "type": "boolean", "description": "Default: false" },
      "filter": { "type": "string", "description": "Test filter expression" },
      "timeoutSeconds": { "type": "integer", "description": "Default: 120" }
    },
    "required": ["operation"]
  }
}
```

### aura_index

```json
{
  "name": "aura_index",
  "description": "Trigger and manage content indexing. Index directories or files, check job status, get index statistics.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["index_directory", "index_file", "status", "stats"] },
      "path": { "type": "string", "description": "Directory or file to index" },
      "recursive": { "type": "boolean", "description": "Recurse subdirectories (default: true)" },
      "filePattern": { "type": "string", "description": "Glob filter, e.g. '*.pdf'" },
      "jobId": { "type": "string", "description": "Job ID for status operation" }
    },
    "required": ["operation"]
  }
}
```

### aura_workspace

```json
{
  "name": "aura_workspace",
  "description": "Manage workspaces: registry CRUD, worktree detection, cache invalidation.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["list", "add", "remove", "set_default", "detect_worktree", "invalidate_cache", "status"] },
      "path": { "type": "string", "description": "Workspace path" },
      "id": { "type": "string", "description": "Workspace ID" },
      "alias": { "type": "string", "description": "Short alias" },
      "tags": { "type": "array", "items": { "type": "string" }, "description": "Tags for categorization" }
    },
    "required": ["operation"]
  }
}
```

### aura_architect

```json
{
  "name": "aura_architect",
  "description": "Analyze codebase architecture: dependencies, layer violations, public API surface. [Coming Soon]",
  "inputSchema": {
    "type": "object",
    "properties": {
      "operation": { "type": "string", "enum": ["dependencies", "layer_check", "public_api"] },
      "projectPath": { "type": "string" },
      "targetLayer": { "type": "string" }
    },
    "required": ["operation"]
  }
}
```

---

## REST API Reference

### Health

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Service status, start time, deploy tag |
| GET | `/health/db` | Database connectivity |
| GET | `/health/rag` | RAG subsystem health + stats |
| GET | `/health/mcp` | MCP handler status + tool list |

### Workspaces

| Method | Path | Body / Params | Description |
|--------|------|---------------|-------------|
| GET | `/api/workspaces` | `?limit=N` | List all workspaces |
| GET | `/api/workspaces/{idOrPath}` | | Get by ID or URL-encoded path |
| POST | `/api/workspaces` | `{ "path", "name?", "startIndexing?", "options?": { "includePatterns", "excludePatterns" } }` | Create workspace |
| DELETE | `/api/workspaces/{id}` | | Delete workspace and all data |

### Workspace Index

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/workspaces/{id}/index` | Index freshness, chunk/graph counts |
| POST | `/api/workspaces/{id}/index` | Trigger re-index (returns 202) |
| DELETE | `/api/workspaces/{id}/index` | Clear RAG index data |
| GET | `/api/workspaces/{id}/index/jobs` | List indexing jobs |
| GET | `/api/workspaces/{id}/index/jobs/{jobId}` | Single job status |

### Workspace Graph

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/workspaces/{id}/graph` | Graph statistics |
| DELETE | `/api/workspaces/{id}/graph` | Clear graph |
| GET | `/api/workspaces/{id}/graph/implementations/{name}` | Find interface implementations |
| GET | `/api/workspaces/{id}/graph/callers/{name}?containingType=` | Find method callers |
| GET | `/api/workspaces/{id}/graph/members/{typeName}` | Get type members |
| GET | `/api/workspaces/{id}/graph/namespaces/{ns}` | Types in namespace |
| GET | `/api/workspaces/{id}/graph/symbols/{name}?nodeType=` | Find symbols by name |

### Workspace Search

| Method | Path | Body |
|--------|------|------|
| POST | `/api/workspaces/{id}/search` | `{ "query": "...", "topK?": 5, "minScore?": 0.3 }` |

