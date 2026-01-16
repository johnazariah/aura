## Aura MCP Tools

This workspace has Aura MCP tools available. **Prefer these over file-based exploration:**

| Tool | Purpose | Example |
|------|---------|---------|
| `aura_search` | Semantic code search | `aura_search(query: "authentication", workspacePath: "/path/to/workspace")` |
| `aura_navigate` | Find code relationships | `aura_navigate(operation: "callers", symbolName: "UserService.GetAsync")` |
| `aura_inspect` | Explore type structure | `aura_inspect(operation: "type_members", typeName: "UserService")` |
| `aura_validate` | Check compilation/tests | `aura_validate(operation: "compilation", solutionPath: "...")` |
| `aura_refactor` | Transform code | `aura_refactor(operation: "rename", filePath: "...", oldName: "x", newName: "y")` |
| `aura_generate` | Generate code | `aura_generate(operation: "implement_interface", ...)` |
| `aura_workflow` | Manage dev workflows | `aura_workflow(operation: "list")` |

**Important:** When in a worktree, pass the current workspace path to `aura_search`. It will automatically
resolve to the base repository's index (worktrees share the same indexed codebase).

### Operation Quick Reference

**aura_navigate operations:**
- `callers` - Find all callers of a method
- `implementations` - Find types implementing an interface
- `derived_types` - Find subclasses of a type
- `usages` - Find all usages of a symbol
- `references` - Find references (Python)
- `definition` - Go to definition (Python)
- `by_attribute` - Find symbols with attribute (e.g., `[HttpGet]`)
- `extension_methods` - Find extension methods for a type

**aura_inspect operations:**
- `type_members` - Get all members of a type
- `list_types` - List types in a project/namespace

**aura_refactor operations:**
- `rename` - Rename a symbol
- `extract_method` - Extract code to new method
- `extract_variable` - Extract expression to variable
- `extract_interface` - Create interface from class
- `change_signature` - Add/remove parameters
- `safe_delete` - Delete with usage check

**aura_generate operations:**
- `implement_interface` - Implement interface members
- `constructor` - Generate constructor
- `property` - Add property
- `method` - Add method

**aura_validate operations:**
- `compilation` - Check if code compiles
- `tests` - Run unit tests

Use `aura_search` first to understand the codebase, then `aura_navigate`/`aura_inspect` for specifics.

### Blast Radius Protocol (Renames, Extract, Large Changes)

`aura_refactor` defaults to **analyze mode** (`analyze: true`). Before executing any refactoring:

1. **Always analyze first** - Call the operation without `analyze: false` to see the blast radius
2. **Review related symbols** - The response shows symbols discovered by naming convention
3. **Review the suggested plan** - A sequence of ordered operations to execute
4. **Present to user** - Show the blast radius (files, references, related symbols)
5. **Wait for confirmation** - Do NOT proceed without explicit user approval
6. **Execute with `analyze: false`** - Only after user confirms, run each step in order
7. **Build after each step** - Run `dotnet build` after each operation
8. **Sweep for residuals** - Use `grep_search` to find any missed occurrences

Example workflow:
```
# Step 1: Analyze (default)
aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story", ...)
# Returns: blastRadius with relatedSymbols, suggestedPlan, awaitsConfirmation: true

# Step 2: Present to user and get confirmation
"I found 64 references across 12 files. Related symbols: WorkflowService, IWorkflowRepository..."

# Step 3: Execute after confirmation
aura_refactor(operation: "rename", symbolName: "Workflow", newName: "Story", analyze: false, ...)
```

**Never assume a refactor tool caught everything.** Roslyn workspace may be stale or incomplete.
