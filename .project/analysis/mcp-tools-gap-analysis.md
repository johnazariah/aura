# MCP Tools Gap Analysis

**Date:** 2026-01-23
**Updated:** 2026-01-23
**Context:** User feedback from agentic workflow session

## Current Tool Inventory

| Tool | Purpose | Status |
|------|---------|--------|
| `aura_search` | Semantic code search | ✅ Working well |
| `aura_navigate` | Find callers, implementations, usages | ✅ Working well |
| `aura_inspect` | Examine type members, list types | ✅ Working |
| `aura_refactor` | Rename, extract, change signature | ✅ Working |
| `aura_generate` | Create types, add members, generate tests | ✅ **Enhanced** (modern C# support) |
| `aura_validate` | Check compilation, run tests | ✅ Not tested |
| `aura_workflow` | Story/workflow management | ✅ Working |
| `aura_architect` | Architectural analysis | 🔲 Placeholder |
| `aura_workspace` | Worktree detection, cache invalidation | ✅ Working |
| `aura_pattern` | Load operational patterns | ✅ Working |
| `aura_edit` | Surgical text editing (line-based) | ✅ Working |

## Issues Fixed This Session

1. **Records/Structs Support** - `aura_generate` now supports records, structs, not just classes
2. **Access Modifiers** - `accessModifier` parameter now respected (was hardcoded to public)
3. **Field Generation** - New `isField`, `isReadonly`, `isStatic` parameters for field generation
4. **Simple Text Editing** - New `aura_edit` tool for surgical line-based edits

## Modern C# Support (New)

Comprehensive modern C# (9-13) features added to `aura_generate`:

| Feature | Parameter | C# Version | Applies To |
|---------|-----------|------------|------------|
| Required properties | `isRequired: true` | C# 11 | property |
| Init-only setters | `hasInit: true` | C# 9 | property |
| Method modifiers | `methodModifier: "virtual\|override\|abstract\|sealed\|new"` | All | method |
| Primary constructors | `primaryConstructorParameters: [...]` | C# 12 | create_type (class) |
| Positional records | `primaryConstructorParameters: [...]` | C# 9 | create_type (record) |
| Generic types | `typeParameters: [{name, constraints}]` | All | create_type, method |
| Attributes | `attributes: [{name, arguments}]` | All | property, method |
| Extension methods | `isExtension: true` | All | method |
| XML documentation | `documentation: "..."` | All | property, method |

### Example: Generic Repository with Primary Constructor

```json
{
  "operation": "create_type",
  "typeName": "Repository",
  "typeKind": "class",
  "typeParameters": [{"name": "TEntity", "constraints": ["class", "IEntity"]}],
  "primaryConstructorParameters": [
    {"name": "context", "type": "DbContext"},
    {"name": "logger", "type": "ILogger<Repository<TEntity>>"}
  ],
  "documentationSummary": "Generic repository for entity operations."
}
```

### Example: Extension Method with Attributes

```json
{
  "operation": "method",
  "className": "StringExtensions",
  "methodName": "IsNullOrEmpty",
  "returnType": "bool",
  "isStatic": true,
  "isExtension": true,
  "parameters": [{"name": "value", "type": "string?"}],
  "documentation": "Checks if a string is null or empty.",
  "body": "return string.IsNullOrEmpty(value);"
}
```

## Remaining Gaps

### 1. ~~Simple Text Insertion~~ ✅ IMPLEMENTED

**Status:** Implemented as `aura_edit` tool (commit 2875250)

**Operations:**
- `insert_lines` - Insert after a line (0 = beginning)
- `replace_lines` - Replace a range of lines
- `delete_lines` - Remove a range of lines  
- `append` - Add content at end of file
- `prepend` - Add content at beginning of file

**Features:**
- 1-based line numbers
- LF normalization on all writes
- Preview mode available

### 2. Git Worktree Support (Priority: HIGH)

**Problem:** VS Code file operations (`list_dir`, `read_file`) reject paths outside the workspace root. Worktrees at `c:\work\aura-feature-xyz` can't be accessed when workspace is `c:\work\aura`.

**Impact:** Users using feature worktrees can't use MCP tools effectively.

**Recommendation Options:**
1. **Aura-side file operations** - Add `aura_read` and `aura_write` that work with any path
2. **Workspace detection** - Auto-detect related worktrees and add them to allowed paths
3. **Document limitation** - Tell users to open the worktree folder in VS Code

### 3. Line Ending Normalization (Priority: MEDIUM)

**Problem:** PowerShell here-strings produce CRLF, but project requires LF. Every file creation needs manual fixup.

**Recommendation:** 
- Add LF normalization to all `aura_generate` file writes (already done in C#)
- Consider adding an `aura_file` tool with `normalize_line_endings` operation

### 4. Better Error Messages (Priority: MEDIUM)

**Problem:** Tools sometimes silently do something different than requested.

**Examples:**
- Old: "Could not parse class declaration" (for records)
- Better: "Type 'ReActOptions' is a record. Use typeKind='record' or switch to class."

**Recommendation:** Review all error paths and add actionable guidance.

### 5. Validation Before Write (Priority: LOW)

**Problem:** `aura_generate` can create code that doesn't compile.

**Current:** `validateCompilation` parameter exists for tests.

**Recommendation:** Extend `validateCompilation` to all `aura_generate` operations.

## Proposed Tool Changes

### Option A: Add `aura_edit` Tool (Recommended)

A lightweight surgical editing tool for when AST manipulation is overkill:

```json
{
  "operation": "insert_line",      // Insert a new line
  "filePath": "...",
  "line": 42,                      // Line number (1-based)
  "position": "after",             // "before", "after", "replace"
  "content": "..."
}
```

```json
{
  "operation": "insert_text",      // Insert text at position
  "filePath": "...",
  "line": 42,
  "column": 10,
  "content": "..."
}
```

```json
{
  "operation": "replace_range",    // Replace a range
  "filePath": "...",
  "startLine": 42,
  "endLine": 45,
  "content": "..."
}
```

This complements VS Code's `replace_string_in_file` but:
- Works with line numbers (no pattern matching ambiguity)
- Can work outside workspace (if we add worktree support)
- Always normalizes to LF

### Option B: Extend Existing Tools

Add to `aura_refactor`:
- `operation: "insert_statement"` - Insert at AST location
- `operation: "modify_initializer"` - Change object initializer members

Downside: Makes `aura_refactor` even more complex.

## Usage Pattern Analysis

Based on user feedback (80% PowerShell, 10% VS Code, 10% Aura):

| Task | Ideal Tool | Actual Tool Used | Gap? |
|------|------------|------------------|------|
| Create new file | `aura_generate(create_type)` | PowerShell | LF issues, worktree |
| Add property | `aura_generate(property)` | PowerShell | Was broken, now fixed |
| Add line to method | `aura_edit` (proposed) | PowerShell | Tool doesn't exist |
| Rename symbol | `aura_refactor(rename)` | - | ✅ Would work |
| Find usages | `aura_navigate(usages)` | - | ✅ Would work |
| Run tests | `aura_validate(tests)` | PowerShell | User didn't try |

## Implementation Priority

1. ~~**Add `aura_edit` for surgical insertions**~~ ✅ Done
2. **Fix worktree support** - Biggest friction point
3. **Improve error messages** - Help users understand what went wrong
4. **LF normalization** - Minor but annoying (partially addressed by `aura_edit`)

## Next Steps

- [x] Create feature spec for `aura_edit`
- [x] Implement `aura_edit` tool
- [ ] Investigate VS Code workspace multi-root support for worktrees
- [ ] Audit all error messages for actionability
- [ ] Add validation flag to all generate operations
