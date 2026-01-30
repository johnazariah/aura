# MCP Tools Gap Analysis

**Date:** 2026-01-23
**Updated:** 2026-01-30
**Context:** Comprehensive review of Aura MCP tool catalog

## Current Tool Inventory (13 Tools)

| Tool | Purpose | Operations | Status |
|------|---------|------------|--------|
| `aura_search` | Semantic code search | query, workspaces, contentType | ✅ Working |
| `aura_navigate` | Code relationships | callers, implementations, derived_types, usages, by_attribute, extension_methods, by_return_type, references, definition | ✅ Working |
| `aura_inspect` | Code structure | type_members, list_types | ✅ Working |
| `aura_refactor` | Code transformation | rename, change_signature, extract_interface, extract_method, extract_variable, safe_delete, move_type_to_file, move_members_to_partial | ✅ Working |
| `aura_generate` | Code generation | implement_interface, constructor, property, method, create_type, tests | ✅ Working |
| `aura_validate` | Build & test | compilation, tests | ✅ Working |
| `aura_workflow` | Story management | list, get, get_by_path, create, enrich, update_step, complete | ✅ Working |
| `aura_architect` | Architecture analysis | dependencies, layer_check, public_api | 🔲 Placeholder |
| `aura_workspace` | Workspace management | list, add, remove, set_default, detect_worktree, invalidate_cache, status | ✅ Working |
| `aura_pattern` | Operational patterns | list, get | ✅ Working |
| `aura_edit` | Surgical text editing | insert_lines, replace_lines, delete_lines, append, prepend | ✅ Working |
| `aura_tree` | Hierarchical exploration | explore, get_node | ✅ Working |
| `aura_docs` | Documentation | search, list, get | ✅ Working |

## Tool Categories

### Read-Only Tools (7)
- `aura_search` - Semantic search
- `aura_navigate` - Find relationships
- `aura_inspect` - Examine structure
- `aura_tree` - Hierarchical view + get source (explore, get_node)
- `aura_docs` - Documentation (search, list, get)
- `aura_pattern` - Load patterns
- `aura_architect` - Architecture analysis (placeholder)

### Write Tools (4)
- `aura_refactor` - Transform code
- `aura_generate` - Create code
- `aura_edit` - Line-based edits
- `aura_validate` - Run build/tests

### Management Tools (2)
- `aura_workflow` - Story CRUD + execution
- `aura_workspace` - Registry + worktree ops (list, add, remove, set_default, detect_worktree, invalidate_cache, status)

## Multi-Language Support

| Tool | C# | Python | TypeScript |
|------|-----|--------|------------|
| `aura_search` | ✅ | ✅ | ✅ |
| `aura_navigate` | ✅ | ✅ (references, definition) | 🔲 |
| `aura_refactor` | ✅ | ✅ (rename, extract) | ✅ (rename, extract) |
| `aura_generate` | ✅ | 🔲 | 🔲 |
| `aura_tree` | ✅ | ✅ | ✅ |
| `aura_inspect` | ✅ | 🔲 | 🔲 |

## Recent Enhancements (Session 2026-01-23 - 2026-01-30)

### Modern C# Support in `aura_generate`

| Feature | Parameter | C# Version |
|---------|-----------|------------|
| Required properties | `isRequired: true` | C# 11 |
| Init-only setters | `hasInit: true` | C# 9 |
| Method modifiers | `methodModifier: "virtual\|override\|abstract\|sealed\|new"` | All |
| Primary constructors | `primaryConstructorParameters: [...]` | C# 12 |
| Positional records | `primaryConstructorParameters: [...]` | C# 9 |
| Generic types | `typeParameters: [{name, constraints}]` | All |
| Attributes | `attributes: [{name, arguments}]` | All |
| Extension methods | `isExtension: true` | All |
| XML documentation | `documentation: "..."` | All |
| Field generation | `isField: true`, `isReadonly: true` | All |

### `aura_edit` Tool (New)

Surgical text editing for when AST manipulation is overkill:

| Operation | Parameters |
|-----------|------------|
| `insert_lines` | `line` (insert after), `content` |
| `replace_lines` | `startLine`, `endLine`, `content` |
| `delete_lines` | `startLine`, `endLine` |
| `append` | `content` |
| `prepend` | `content` |

Features:
- 1-based line numbers
- LF normalization on all writes
- Preview mode (`preview: true`)

### Hierarchical Exploration

`aura_tree` now has two operations:

| Operation | Purpose |
|-----------|---------|
| `explore` | Get tree view with configurable depth (1=files, 2=+types, 3=+members) |
| `get_node` | Retrieve full source for a node by ID |

### Workspace Management

`aura_workspace` unifies all workspace operations:

| Operation | Description |
|-----------|-------------|
| `list` | List all registered workspaces |
| `add` | Add workspace with optional alias/tags |
| `remove` | Remove workspace by ID |
| `set_default` | Set default workspace for queries |
| `detect_worktree` | Check if path is in a git worktree |
| `invalidate_cache` | Clear Roslyn cache for workspace |
| `status` | Get workspace indexing status |

Cross-workspace search: `aura_search` with `workspaces: ["*"]`

### Bundled Documentation

| Operation | Purpose |
|-----------|---------|
| `search` | Semantic search across bundled docs |
| `list` | List available doc topics with optional filtering |
| `get` | Get specific doc content by ID |

## Completed Gaps ✅

1. **Records/Structs Support** - `aura_generate` now supports records, structs
2. **Access Modifiers** - `accessModifier` parameter now respected
3. **Field Generation** - New `isField`, `isReadonly`, `isStatic` parameters
4. **Simple Text Editing** - `aura_edit` tool for surgical edits
5. **Hierarchical Exploration** - `aura_tree` with explore + get_node operations
6. **Workspace Management** - `aura_workspace` unifies registry + worktree ops
7. **Documentation** - `aura_docs` with search, list, get

## Remaining Gaps

### 1. `aura_architect` Implementation (Priority: LOW)

Currently placeholder. Planned operations:
- `dependencies` - Analyze project dependencies
- `layer_check` - Validate layered architecture
- `public_api` - List public API surface

**Recommendation:** Defer to post-1.0. Not blocking any workflows.

### 2. Python/TypeScript `aura_generate` (Priority: MEDIUM)

C# has full generation support; other languages limited to refactoring.

**Recommendation:** Add basic `create_type` for Python/TypeScript when demand exists.

### 3. `aura_inspect` for Python/TypeScript (Priority: LOW)

Only C# has `type_members` and `list_types`.

**Recommendation:** Use `aura_tree` as workaround; full support post-1.0.

### 4. Better Error Messages (Priority: MEDIUM)

Some tools return generic errors instead of actionable guidance.

**Recommendation:** Audit error paths and add context-specific help.

### 5. Validation on All Generate Operations (Priority: LOW)

`validateCompilation` exists for tests but not other operations.

**Recommendation:** Extend to all `aura_generate` operations.

## Usage Patterns

Based on observed usage:

| Task | Recommended Tool |
|------|------------------|
| Find code semantically | `aura_search` |
| Find callers/usages | `aura_navigate` |
| Explore type structure | `aura_inspect` or `aura_tree(explore)` |
| Get source code | `aura_tree(get_node)` |
| Rename symbol | `aura_refactor(rename)` |
| Add property/method | `aura_generate` |
| Create new class | `aura_generate(create_type)` |
| Generate tests | `aura_generate(tests)` |
| Simple line edit | `aura_edit` |
| Verify changes | `aura_validate(compilation)` |
| Run tests | `aura_validate(tests)` |
| Load pattern | `aura_pattern(get)` |
| Read documentation | `aura_docs` or `aura_docs_get` |

## Next Steps

- [x] Create `aura_edit` tool
- [x] Add hierarchical exploration (`aura_tree` with explore + get_node)
- [x] Add workspace management (`aura_workspace` with registry + worktree ops)
- [x] Add documentation tools (`aura_docs` with search, list, get)
- [ ] Implement `aura_architect` operations
- [ ] Add Python/TypeScript to `aura_generate`
- [ ] Audit error messages for actionability
