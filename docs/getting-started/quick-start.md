# Quick Start

This guide walks you through registering a workspace, triggering indexing, and searching — all through Copilot MCP tools.

## 1. Configure Copilot to Use Aura

Add Aura as an MCP server in your VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "aura": {
        "type": "sse",
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```

Restart VS Code. The Aura tools should appear when Copilot lists available MCP tools.

## 2. Register a Workspace

Ask Copilot to register your project:

> "Register my current project as an Aura workspace"

Or call the tool directly:

```json
{
  "name": "aura_workspace",
  "arguments": {
    "operation": "add",
    "path": "C:/projects/my-app",
    "alias": "my-app"
  }
}
```

## 3. Index the Workspace

Trigger indexing:

> "Index my workspace with Aura"

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "index_directory",
    "path": "C:/projects/my-app",
    "recursive": true
  }
}
```

Check indexing status:

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "stats",
    "path": "C:/projects/my-app"
  }
}
```

## 4. Search Your Code

Ask Copilot a question about your codebase:

> "Search my codebase for authentication logic"

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "authentication logic",
    "workspacePath": "C:/projects/my-app",
    "limit": 5
  }
}
```

Results include file paths, code snippets, and similarity scores. Exact symbol matches are boosted above semantic results.

## 5. Explore Code Structure

Browse types and members:

```json
{
  "name": "aura_tree",
  "arguments": {
    "workspacePath": "C:/projects/my-app",
    "operation": "explore",
    "maxDepth": 2
  }
}
```

Navigate relationships:

```json
{
  "name": "aura_navigate",
  "arguments": {
    "operation": "implementations",
    "symbolName": "IAuthService",
    "solutionPath": "C:/projects/my-app/MyApp.sln"
  }
}
```

## What's Next

- [MCP Tools](../user-guide/mcp-tools.md) — full reference for all 10 tools
- [Indexing](../user-guide/indexing.md) — supported file types and ingestors
- [Use Cases](../user-guide/use-cases.md) — real-world scenarios

