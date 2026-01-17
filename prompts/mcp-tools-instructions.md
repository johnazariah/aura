## Aura MCP Tools

**Prefer these semantic tools over file-based exploration:**

| Tool | Purpose |
|------|---------|
| `aura_search` | Semantic code search |
| `aura_navigate` | Find callers, implementations, usages |
| `aura_inspect` | Explore type members and structure |
| `aura_validate` | Check compilation or run tests |
| `aura_refactor` | Rename, extract, change signatures |
| `aura_generate` | Create types, generate tests, implement interfaces |
| `aura_workflow` | Manage dev workflows |
| `aura_pattern` | Load step-by-step operational patterns |

### ⚠️ Workspace Path Resolution

**Before calling ANY `aura_*` tool:**

1. Check `<workspace_info>` for the actual workspace folders
2. Use ONLY paths from `<workspace_info>` — never from conversation history
3. Always use absolute paths anchored to the workspace root

**Mistakes to avoid:**
- ❌ Using paths from conversation history or error messages
- ❌ Using worktree paths when in main repo (or vice versa)
- ✅ Always check `<workspace_info>` fresh for each tool call

### Worktree Isolation

When in a git worktree:
- Stay within the worktree folder — never escape to parent/main repository
- The worktree IS your workspace; all paths must be within it
- `aura_search` automatically resolves to the shared index

---

## Operations Reference

**aura_navigate:**
`callers`, `implementations`, `derived_types`, `usages`, `references`, `definition`, `by_attribute`, `extension_methods`

**aura_inspect:**
`type_members`, `list_types`

**aura_refactor:**
`rename`, `extract_method`, `extract_variable`, `extract_interface`, `change_signature`, `safe_delete`, `move_type_to_file`

**aura_generate:**
`create_type`, `tests`, `implement_interface`, `constructor`, `property`, `method`

**aura_validate:**
`compilation`, `tests`

Use `aura_search` first to understand the codebase, then `aura_navigate`/`aura_inspect` for specifics.

---

## Operational Patterns

For complex multi-step operations, call `aura_pattern(operation: "list")` to discover available patterns, then load the relevant one:

```
aura_pattern(operation: "list")                              # Discover available patterns
aura_pattern(operation: "get", name: "comprehensive-rename") # Load specific pattern
```

Patterns are step-by-step procedures using existing primitives. When you encounter a complex operation (renaming domain concepts, generating comprehensive tests, etc.), check for an applicable pattern and follow it exactly.

---

## Refactoring

`aura_refactor` defaults to **analyze mode** (`analyze: true`), returning blast radius and suggested plan before making changes.

**For rename operations:** Load the `comprehensive-rename` pattern — it handles cascading renames, file renames, and residual sweeps.

**For other operations** (`extract_method`, `extract_variable`, `change_signature`, `safe_delete`):
1. Call with `analyze: true` to preview changes
2. Get user confirmation
3. Execute with `analyze: false`
4. Build to verify

**Disambiguation** (when multiple symbols match):
- `filePath` — for types
- `containingType` — for members

---

## Creating New Types

Use `aura_generate(operation: "create_type")` instead of manual file creation:

```
aura_generate(
  operation: "create_type",
  typeName: "InvoiceRepository",
  typeKind: "class",
  targetDirectory: "/path/to/project/Repositories",
  implements: ["IInvoiceRepository"]
)
```

Benefits: correct namespace inference, proper formatting, standard structure.

---

## Test Generation

Use `aura_generate(operation: "tests")` for comprehensive test generation:

```
aura_generate(
  operation: "tests",
  target: "OrderService"           # Class or method (Class.Method)
)
```

| Parameter | Description |
|-----------|-------------|
| `target` | Class, method, or namespace (required) |
| `count` | Explicit count (omit for comprehensive) |
| `focus` | `all`, `happy_path`, `edge_cases`, `error_handling` |
| `analyzeOnly` | Return analysis only, no generation |

---

## General Guidelines

- Explain each step before executing
- Wait for confirmation on destructive operations
- Build/validate after significant changes
