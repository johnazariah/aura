## Aura MCP Tools

### ⚠️ CRITICAL: Tool Selection

**Always prefer Aura semantic tools over text manipulation for C# code.**

| Task | ✅ Use Aura Tool | ❌ Don't Use |
|------|------------------|--------------|
| Add method | `aura_generate(operation: "method")` | `replace_string_in_file` |
| Add property | `aura_generate(operation: "property")` | `replace_string_in_file` |
| Rename symbol | `aura_refactor(operation: "rename")` | Find/replace |
| Create C# class | `aura_generate(operation: "create_type")` | `create_file` |
| Implement interface | `aura_generate(operation: "implement_interface")` | Manual stubs |
| Generate tests | `aura_generate(operation: "tests")` | Manual test writing |
| Find usages | `aura_navigate(operation: "usages")` | `grep_search` |
| Explore types | `aura_inspect` | Multiple `read_file` calls |

**Text manipulation (`replace_string_in_file`) is ONLY for:**
- Non-C# files (JSON, YAML, Markdown, TypeScript)
- Simple one-line fixes where Aura overhead isn't justified
- When Aura tools fail and fallback is needed

---

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

## Context-Aware Agent Tools

### Token Budget Management

Agents have access to token budget awareness tools for managing long-running tasks:

| Tool | Purpose |
|------|---------|
| `check_token_budget` | Query remaining context capacity and get recommendations |
| `spawn_sub_agent` | Delegate subtasks to fresh context when approaching limits |

### check_token_budget

Call when uncertain about remaining capacity:

```json
{
  "tool": "check_token_budget",
  "input": {}
}
```

Returns:
```json
{
  "available": true,
  "used": 75000,
  "remaining": 25000,
  "budget": 100000,
  "percentage": 75.0,
  "isAboveThreshold": true,
  "recommendation": "CAUTION: Approaching context limit (>70%). Plan to spawn sub-agents for complex remaining work."
}
```

### spawn_sub_agent

Delegate complex subtasks to a child agent with fresh context:

```json
{
  "tool": "spawn_sub_agent",
  "input": {
    "agentId": "coding-agent",
    "task": "Implement email validation per RFC 5322",
    "context": "Working in UserService.cs, method ValidateEmail"
  }
}
```

**Use when:**
- Token budget exceeds 70% threshold
- Task requires deep reasoning in a specific subdomain
- Current task has clear subtasks that can be delegated

**The parent receives:** Only the final answer (not the full reasoning chain)

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

### ⚠️ Known Limitations

| Issue | Fix With |
|-------|----------|
| Wrong folder placement | Move file manually |
| Unqualified imports (`IFileSystem`) | Add full namespace with `replace_string_in_file` |
| `Substitute.For<ILogger>()` | Replace with `NullLogger<T>.Instance` |
| `// TODO:` placeholder assertions | Write real assertions |

### Hybrid Approach: `aura_generate method` with `body`

For maximum control, generate individual test methods with full implementation:

```
aura_generate(
  operation: "method",
  className: "MyServiceTests",
  methodName: "MyMethod_WhenValid_ReturnsTrue",
  returnType: "void",
  body: "// Arrange\nvar sut = new MyService();\n\n// Act\nvar result = sut.DoThing();\n\n// Assert\nresult.Should().BeTrue();"
)
```

**Aura handles:** File location, insertion point, formatting, test attribute
**You provide:** Complete test logic with real assertions

---

## General Guidelines

- Explain each step before executing
- Wait for confirmation on destructive operations
- Build/validate after significant changes
