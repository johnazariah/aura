# AI Agent Integration Guide

**Last Updated**: 2026-01-27  
**Version**: 1.3.1

## Overview

Aura provides a powerful MCP (Model Context Protocol) integration that enables GitHub Copilot and other AI agents to perform semantic code operations. This guide covers the available tools, recommended workflows, and best practices for effective collaboration with Aura.

## Core Capabilities

Aura's agent integration offers three primary capabilities:

1. **Semantic Code Understanding** - RAG-powered search and navigation across your codebase
2. **Code Transformation** - Roslyn-based refactoring that understands language semantics
3. **Workflow Automation** - Pattern-driven development workflows with multi-step execution

## Available MCP Tools

Aura exposes 13 consolidated MCP tools, organized by intent:

| Tool | Intent | Operations | Read/Write |
|------|--------|-----------|------------|
| `aura_search` | Search codebase | Semantic search with similarity scoring | Read |
| `aura_navigate` | Find relationships | callers, implementations, usages, references | Read |
| `aura_inspect` | Examine structure | type_members, list_types | Read |
| `aura_refactor` | Transform code | rename, extract, signature changes | Write |
| `aura_generate` | Create code | types, methods, properties, tests | Write |
| `aura_validate` | Check correctness | compilation, tests | Read |
| `aura_workflow` | Manage stories | CRUD for development workflows | CRUD |
| `aura_pattern` | Load playbooks | Multi-step operational patterns | Read |
| `aura_workspace` | Manage state | Worktree detection, cache invalidation | R/W |
| `aura_edit` | Text editing | Line-based surgical edits | Write |
| `aura_tree` | Hierarchical view | Navigable codebase tree | Read |
| `aura_get_node` | Retrieve content | Full source for tree nodes | Read |
| `aura_docs` | Search docs | Semantic documentation search | Read |

## Recommended Workflows

### 1. **Explore → Navigate → Modify** (Code Understanding)

The most common workflow for code changes:

```
Step 1: Search for relevant code
  aura_search(query: "authentication logic", workspacePath: "C:\\work\\myrepo")

Step 2: Navigate to find usages
  aura_navigate(operation: "usages", symbolName: "AuthService")

Step 3: Inspect type structure
  aura_inspect(operation: "type_members", typeName: "AuthService")

Step 4: Make changes
  aura_refactor(operation: "rename", symbolName: "Authenticate", newName: "SignIn")
```

### 2. **Pattern → Generate → Validate** (Feature Development)

For implementing new features:

```
Step 1: Load a pattern
  aura_pattern(operation: "get", name: "implement-service-layer", language: "csharp")

Step 2: Generate code
  aura_generate(operation: "create_type", typeName: "UserService", implements: ["IUserService"])

Step 3: Validate
  aura_validate(operation: "compilation", solutionPath: "C:\\work\\myrepo\\App.sln")
```

### 3. **Workflow → Enrich → Execute** (Story-Driven Development)

For GitHub issue workflows:

```
Step 1: Create from issue
  aura_workflow(operation: "create", issueUrl: "https://github.com/org/repo/issues/123")

Step 2: Enrich with pattern
  aura_workflow(operation: "enrich", storyId: "{guid}", pattern: "implement-feature")

Step 3: Execute steps
  (Steps are executed through Aura's assisted workflow UI)

Step 4: Complete story
  aura_workflow(operation: "complete", storyId: "{guid}")
```

## When to Use Each Tool

### For Code Discovery

| Scenario | Tool | Operation |
|----------|------|-----------|
| Find classes/methods by concept | `aura_search` | (semantic search) |
| Find who calls a method | `aura_navigate` | `callers` |
| Find interface implementations | `aura_navigate` | `implementations` |
| Find where symbol is used | `aura_navigate` | `usages` |
| List all types in a project | `aura_inspect` | `list_types` |
| Examine class members | `aura_inspect` | `type_members` |
| Get hierarchical tree view | `aura_tree` | (full tree) |

### For Code Modification

| Scenario | Tool | Operation |
|----------|------|-----------|
| Rename symbol across codebase | `aura_refactor` | `rename` |
| Change method signature | `aura_refactor` | `change_signature` |
| Extract method from selection | `aura_refactor` | `extract_method` |
| Extract interface from class | `aura_refactor` | `extract_interface` |
| Create new class/interface | `aura_generate` | `create_type` |
| Add method to existing class | `aura_generate` | `method` |
| Add property to class | `aura_generate` | `property` |
| Generate unit tests | `aura_generate` | `tests` |
| Simple line edits | `aura_edit` | insert/replace/delete |

### For Validation

| Scenario | Tool | Operation |
|----------|------|-----------|
| Check if code compiles | `aura_validate` | `compilation` |
| Run unit tests | `aura_validate` | `tests` |

## Best Practices

### 1. **Always Start with Search**

Before making changes, use `aura_search` to understand the context:

```
Good: Search first to understand related code
  aura_search → aura_navigate → aura_refactor

Bad: Making changes without context
  aura_refactor (might miss related code)
```

### 2. **Use Analyze Mode for Refactoring**

`aura_refactor` defaults to **analyze mode** (`analyze: true`). This shows the blast radius before executing:

```
Step 1: Analyze (default)
  aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story")
  → Returns: 64 references across 12 files, related symbols: WorkflowService, IWorkflowRepository

Step 2: Review and confirm

Step 3: Execute
  aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story", analyze: false)
```

### 3. **Validate After Changes**

Always run compilation after semantic changes:

```
aura_refactor(operation: "rename", ..., analyze: false)
aura_validate(operation: "compilation", solutionPath: "...")
```

### 4. **Use Patterns for Complex Tasks**

For multi-step tasks, load a pattern first:

```
aura_pattern(operation: "list")  # See available patterns
aura_pattern(operation: "get", name: "comprehensive-rename", language: "csharp")
```

### 5. **Prefer Semantic Tools Over Text Manipulation**

| Task | ✅ Use | ❌ Avoid |
|------|--------|----------|
| Add method to C# class | `aura_generate` | `aura_edit` |
| Rename C# symbol | `aura_refactor` | Find/replace |
| Simple config change | `aura_edit` | `aura_generate` |

### 6. **Specify Workspace Path**

Always provide `workspacePath` or `solutionPath` to ensure correct context:

```
aura_search(query: "auth", workspacePath: "C:\\work\\myrepo")
aura_refactor(operation: "rename", solutionPath: "C:\\work\\myrepo\\App.sln", ...)
```

### 7. **Use Preview Mode When Uncertain**

Most tools support `preview: true` to see changes without applying:

```
aura_generate(operation: "method", preview: true, ...)
aura_refactor(operation: "rename", preview: true, ...)
```

## Multi-Language Support

Aura supports C#, Python, and TypeScript refactoring:

| Language | Supported Tools | Auto-Detection |
|----------|----------------|----------------|
| C# | All `aura_refactor`, `aura_generate` | via `solutionPath` |
| Python | `rename`, `extract_*`, `references`, `definition` | via `filePath` extension |
| TypeScript | `rename`, `extract_*` | via `filePath` extension |

**Auto-detection**: Tools automatically detect language from file extension. No explicit language parameter needed.

## Error Handling

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "Workspace not indexed" | RAG index missing | Use API: `POST /api/workspaces` |
| "Symbol not found" | Incorrect name or scope | Check spelling, provide `containingType` |
| "Compilation failed" | Code doesn't build | Fix errors, then retry |
| "Solution path required" | Missing C# context | Provide `solutionPath` parameter |

### Diagnostic Commands

```powershell
# Check workspace status
curl http://localhost:5300/api/workspaces/lookup?path=C%3A%5Cwork%5Cmyrepo

# Check API health
curl http://localhost:5300/health

# Re-index workspace
curl -X POST http://localhost:5300/api/workspaces/{id}/reindex
```

## Performance Tips

### 1. **Limit Search Results**

```
aura_search(query: "...", limit: 5)  # Default is 10
```

### 2. **Filter by Content Type**

```
aura_search(query: "...", contentType: "code")  # Exclude docs/config
```

### 3. **Use Tree with Max Depth**

```
aura_tree(workspacePath: "...", maxDepth: 2)  # Stop at types, skip members
```

### 4. **Batch Operations**

For multiple related changes, plan ahead:

```
Good: Single rename operation handles all references
  aura_refactor(operation: "rename", ...)

Bad: Multiple individual edits
  aura_edit × 50 files
```

## Integration Points

### With GitHub Copilot

Aura tools are exposed via MCP and automatically available in GitHub Copilot CLI. No manual setup required after installation.

### With VS Code Extension

The Aura VS Code extension provides:
- Workflow tree view (grouped by status)
- Step-by-step execution UI
- Agent chat panels
- RAG statistics

### With API Endpoints

Direct API access for custom integrations:

```powershell
# Create workflow
curl -X POST http://localhost:5300/api/developer/workflows `
  -H "Content-Type: application/json" `
  -d '{"title":"My Task", "repositoryPath":"C:\\work\\repo"}'

# Execute step
curl -X POST http://localhost:5300/api/developer/workflows/{wfId}/steps/{stepId}/execute
```

## Next Steps

- **Configuration**: See [configuration.md](configuration.md) for LLM and RAG settings
- **Tools Reference**: See [tools-reference.md](tools-reference.md) for complete API documentation
- **Troubleshooting**: See [troubleshooting.md](troubleshooting.md) for common issues
