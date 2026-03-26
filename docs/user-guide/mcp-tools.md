# MCP Tools

Aura exposes 10 MCP tools to GitHub Copilot. All tools are called via JSON-RPC over the `/mcp` endpoint.

## Setup

Add to your VS Code `settings.json`:

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

## Tool Summary

| Tool | Access | Description |
|------|--------|-------------|
| `aura_search` | Read | Semantic search across indexed codebases |
| `aura_navigate` | Read | Find callers, implementations, derived types, usages, references |
| `aura_inspect` | Read | Examine type members and class listings |
| `aura_tree` | Read | Hierarchical codebase exploration |
| `aura_refactor` | Write | Rename, extract, change signatures, safe delete |
| `aura_generate` | Write | Create types, implement interfaces, add members, generate tests |
| `aura_validate` | Read | Check compilation, run tests |
| `aura_index` | Read/Write | Trigger and manage content indexing |
| `aura_workspace` | Read/Write | Workspace registry CRUD |
| `aura_architect` | Read | Architecture analysis (coming soon) |

## aura_search

Semantic search across indexed code, docs, and config. Exact symbol matches are boosted above RAG results.

**Operations:** single search action (no `operation` parameter)

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | ✅ | Search query — concept, symbol name, or keyword |
| `workspacePath` | string | | Path to workspace or worktree |
| `workspaces` | string[] | | Workspace IDs or aliases; `["*"]` for all |
| `limit` | integer | | Max results (default 10) |
| `contentType` | enum | | `code`, `docs`, `config`, or `all` |

```json
{
  "name": "aura_search",
  "arguments": {
    "query": "dependency injection registration",
    "workspacePath": "C:/projects/my-app",
    "contentType": "code",
    "limit": 5
  }
}
```

## aura_navigate

Find code elements and their relationships. Auto-detects language from file extension, solution path, or project path.

**Operations:** `callers`, `implementations`, `derived_types`, `usages`, `by_attribute`, `extension_methods`, `by_return_type`, `references`, `definition`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Navigation operation |
| `symbolName` | string | | Symbol to navigate from |
| `containingType` | string | | Disambiguate overloaded symbols |
| `solutionPath` | string | | `.sln` path — required for C# |
| `filePath` | string | | File path — required for Python |
| `offset` | integer | | Character offset — required for Python |
| `projectPath` | string | | Project root — required for Python/TS |
| `attributeName` | string | | For `by_attribute` operation |
| `targetType` | string | | For `extension_methods` / `by_return_type` |
| `targetKind` | enum | | `method`, `class`, `property`, `all` |

```json
{
  "name": "aura_navigate",
  "arguments": {
    "operation": "implementations",
    "symbolName": "IUserService",
    "solutionPath": "C:/projects/my-app/MyApp.sln"
  }
}
```

## aura_inspect

Examine code structure — list types in a project or get members of a specific type.

**Operations:** `type_members`, `list_types`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Inspection operation |
| `typeName` | string | | Type to inspect (for `type_members`) |
| `solutionPath` | string | | `.sln` path for C# |
| `projectPath` | string | | Project root for TS/Python |
| `projectName` | string | | Filter by project name |
| `namespaceFilter` | string | | Partial namespace match |
| `nameFilter` | string | | Partial type name match |

```json
{
  "name": "aura_inspect",
  "arguments": {
    "operation": "type_members",
    "typeName": "UserService",
    "solutionPath": "C:/projects/my-app/MyApp.sln"
  }
}
```

## aura_tree

Browse the codebase hierarchy — files, types, and members — or retrieve full source for a specific node.

**Operations:** `explore`, `get_node`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `workspacePath` | string | ✅ | Workspace root |
| `operation` | enum | | `explore` or `get_node` (default: `explore`) |
| `pattern` | string | | Filter pattern (default: `.`) |
| `maxDepth` | integer | | 1 = files, 2 = +types, 3 = +members (default 2) |
| `detail` | enum | | `min` or `max` |
| `nodeId` | string | | Node ID from explore results (for `get_node`) |

```json
{
  "name": "aura_tree",
  "arguments": {
    "workspacePath": "C:/projects/my-app",
    "operation": "explore",
    "pattern": "Services",
    "maxDepth": 3
  }
}
```

## aura_refactor

Transform existing code with language-aware refactoring. Supports C# (Roslyn), Python (Rope), and TypeScript (ts-morph).

**Operations:** `rename`, `change_signature`, `extract_interface`, `extract_method`, `extract_variable`, `safe_delete`, `move_type_to_file`, `move_members_to_partial`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Refactoring operation |
| `symbolName` | string | | Symbol to refactor |
| `newName` | string | | New name (rename, extract) |
| `solutionPath` | string | | `.sln` path for C# |
| `filePath` | string | | File containing the code |
| `containingType` | string | | For disambiguation |
| `analyze` | boolean | | Blast radius analysis only (default: true) |
| `preview` | boolean | | Return changes without applying |
| `validate` | boolean | | Build after refactoring |

**Language support:**

| Operation | C# | Python | TypeScript |
|-----------|:---:|:---:|:---:|
| `rename` | ✅ | ✅ | ✅ |
| `extract_method` | | ✅ | ✅ |
| `extract_variable` | | ✅ | ✅ |
| `change_signature` | ✅ | | |
| `extract_interface` | ✅ | | |
| `safe_delete` | ✅ | | |
| `move_type_to_file` | ✅ | | |
| `move_members_to_partial` | ✅ | | |

```json
{
  "name": "aura_refactor",
  "arguments": {
    "operation": "rename",
    "symbolName": "GetUser",
    "newName": "GetUserById",
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "analyze": false
  }
}
```

## aura_generate

Generate new code — types, interfaces, constructors, properties, methods, and test suites.

**Operations:** `create_type`, `implement_interface`, `constructor`, `property`, `method`, `tests`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Generation operation |
| `solutionPath` | string | | `.sln` path |
| `className` | string | | Target class for member operations |
| `typeName` | string | | Name of type to create |
| `typeKind` | enum | | `class`, `interface`, `record`, `struct` |
| `target` | string | | Test generation target |
| `preview` | boolean | | Return changes without applying |

See [API Reference](../mcp-tools/api-reference.md) for the full parameter list.

```json
{
  "name": "aura_generate",
  "arguments": {
    "operation": "tests",
    "target": "UserService",
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "focus": "edge_cases",
    "maxTests": 10
  }
}
```

## aura_validate

Check compilation or run tests. Auto-detects language from the provided path.

**Operations:** `compilation`, `tests`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | `compilation` or `tests` |
| `solutionPath` | string | | `.sln` path for C# |
| `projectPath` | string | | Project root for TS/Python |
| `projectName` | string | | Filter to one project |
| `includeWarnings` | boolean | | Include warnings (default: false) |
| `filter` | string | | Test filter expression |
| `timeoutSeconds` | integer | | Timeout (default: 120) |

```json
{
  "name": "aura_validate",
  "arguments": {
    "operation": "tests",
    "solutionPath": "C:/projects/my-app/MyApp.sln",
    "filter": "FullyQualifiedName~UserServiceTests"
  }
}
```

## aura_index

Trigger and manage content indexing — index directories/files, check job status, view statistics.

**Operations:** `index_directory`, `index_file`, `status`, `stats`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Indexing operation |
| `path` | string | | Directory or file to index |
| `recursive` | boolean | | Recurse into subdirectories (default: true) |
| `filePattern` | string | | Glob filter, e.g. `*.pdf` |
| `jobId` | string | | Job ID for `status` operation |

```json
{
  "name": "aura_index",
  "arguments": {
    "operation": "index_directory",
    "path": "C:/projects/my-app",
    "recursive": true,
    "filePattern": "*.cs"
  }
}
```

## aura_workspace

Manage the workspace registry — add, remove, list, set default, detect worktrees, invalidate caches.

**Operations:** `list`, `add`, `remove`, `set_default`, `detect_worktree`, `invalidate_cache`, `status`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Workspace operation |
| `path` | string | | Workspace path (add, detect, invalidate, status) |
| `id` | string | | Workspace ID (remove, set_default) |
| `alias` | string | | Short alias (add) |
| `tags` | string[] | | Tags for categorization (add) |

```json
{
  "name": "aura_workspace",
  "arguments": {
    "operation": "add",
    "path": "C:/projects/my-app",
    "alias": "my-app",
    "tags": ["dotnet", "web"]
  }
}
```

## aura_architect

Analyze codebase architecture. **This tool is coming soon** — all operations currently return a placeholder message.

**Operations:** `dependencies`, `layer_check`, `public_api`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operation` | enum | ✅ | Architecture operation |
| `projectPath` | string | | Project or solution path |
| `targetLayer` | string | | Target layer for `layer_check` |

