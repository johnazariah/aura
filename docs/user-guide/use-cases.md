# Use Cases

Practical examples of using Aura's MCP tools through GitHub Copilot.

## Code Search

Find code by concept, not just by name:

> "Search for how authentication tokens are validated"

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "authentication token validation",
    "workspacePath": "C:/projects/my-app",
    "contentType": "code"
  }
}
```

Results include the matching code snippet, file path, and a similarity score. Exact symbol matches (e.g., `ValidateToken`) are boosted above semantic matches.

## PDF Research

Index research papers or technical PDFs, then search them alongside code:

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "index_directory",
    "path": "C:/research/papers",
    "filePattern": "*.pdf"
  }
}
```

Then search across everything:

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "transformer attention mechanism",
    "workspaces": ["*"],
    "contentType": "docs"
  }
}
```

## Config File Search

Find configuration patterns across JSON, YAML, and XML files:

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "connection string database timeout",
    "contentType": "config"
  }
}
```

The StructuredData ingestor chunks config files by top-level keys, so results are contextually meaningful.

## Multi-Language Projects

Aura handles polyglot repositories. A single workspace can contain C#, TypeScript, Python, Go, Rust, and more — each processed by the appropriate ingestor.

### Navigate across languages

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "user registration endpoint",
    "workspacePath": "C:/projects/fullstack-app"
  }
}
```

This returns results from both the C# backend and the TypeScript frontend.

### Inspect C# types

```json
{
  "name": "aura_inspect",
  "arguments": {
    "operation": "type_members",
    "typeName": "UserController",
    "solutionPath": "C:/projects/fullstack-app/Backend.sln"
  }
}
```

### Find Python callers

```json
{
  "name": "aura_navigate",
  "arguments": {
    "operation": "references",
    "filePath": "C:/projects/fullstack-app/scripts/deploy.py",
    "offset": 150,
    "projectPath": "C:/projects/fullstack-app/scripts"
  }
}
```

## Code Generation

Generate a new service class with proper namespace detection:

```json
{
  "name": "aura_generate",
  "arguments": {
    "operation": "create_type",
    "typeName": "OrderService",
    "typeKind": "class",
    "implements": ["IOrderService"],
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "targetDirectory": "C:/projects/my-app/src/Services"
  }
}
```

Generate tests for an existing class:

```json
{
  "name": "aura_generate",
  "arguments": {
    "operation": "tests",
    "target": "OrderService",
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "focus": "edge_cases"
  }
}
```

## Refactoring with Blast Radius Analysis

Rename a symbol and see what would change before applying:

```json
{
  "name": "aura_refactor",
  "arguments": {
    "operation": "rename",
    "symbolName": "ProcessOrder",
    "newName": "ProcessOrderAsync",
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "analyze": true
  }
}
```

The `analyze: true` flag returns which files and references would be affected without making changes. Set `analyze: false` to apply.

## Multi-Workspace Search

Search across all registered workspaces at once:

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "retry policy implementation",
    "workspaces": ["*"]
  }
}
```

Or search specific workspaces by alias:

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "retry policy",
    "workspaces": ["backend", "shared-libs"]
  }
}
```

