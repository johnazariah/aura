# Pattern: Comprehensive Rename

Rename a domain concept across the entire codebase, including related symbols, variables, files, and documentation.

## When to Use

- User asks to rename a core domain concept (e.g., "Workflow" → "Story")
- The rename affects multiple related types (interfaces, services, DTOs, enums)
- File names should match the new type names

## Prerequisites

- Solution path is known
- The symbol to rename exists in the codebase
- User has confirmed the rename intent

## Execution Protocol

**For each step below:**
1. **Announce** which step you're executing (e.g., "Step 3 of 7: Renaming WorkflowService → StoryService")
2. **Explain** why this step is needed (e.g., "This service depends on the Workflow type renamed in step 2")
3. **Execute** the operation
4. **Report** the result before proceeding

Do not batch operations or skip explanations.

## Steps

### 1. Analyze Blast Radius

```
aura_refactor(
  operation: "rename",
  symbolName: "{OldName}",
  newName: "{NewName}",
  solutionPath: "{path}",
  analyze: true
)
```

This returns:
- Total references and files affected
- Related symbols discovered by naming convention
- Suggested execution plan
- Files that need renaming

### 2. Present Plan to User

Show the blast radius and ask for confirmation:
- List all symbols that will be renamed
- Ask if any should be preserved (symbols that contain the word but are different concepts)
- Get explicit approval before proceeding

### 3. Execute Renames in Order

For each symbol in the suggested plan:

```
aura_refactor(
  operation: "rename",
  symbolName: "{symbol}",
  newName: "{derivedNewName}",
  solutionPath: "{path}",
  filePath: "{path}",  // Required for disambiguation
  analyze: false
)
```

**Important**: Use `filePath` when the symbol name is ambiguous (e.g., multiple types named `Workflow`).

### 4. Build After Each Rename

```bash
dotnet build
```

If build fails, investigate before continuing.

### 5. Rename Files

For files that need renaming, use `move_type_to_file`:

```
aura_refactor(
  operation: "move_type_to_file",
  symbolName: "{NewTypeName}",
  filePath: "{oldFilePath}",
  solutionPath: "{path}"
)
```

This will:
- Rename the file if the type is the sole occupant
- Extract to a new file if multiple types exist

### 6. Sweep for Residuals

Use `grep_search` to find any remaining references:

```
grep_search(query: "{OldName}", isRegexp: false)
```

Check for:
- Copyright headers (cosmetic, safe to fix with text replacement)
- Comments referencing the old name
- String literals in tests

### 7. Handle Non-Code Files

If applicable, manually update:
- Documentation (`.md` files)
- API routes (may need versioning discussion)
- Database migrations (emit warning, don't auto-generate)

## Anti-patterns

❌ **Never use text replacement for symbol renames** - breaks builds, misses references

❌ **Never skip the analysis step** - you need to see the full blast radius

❌ **Never rename without disambiguation** - when multiple symbols match, specify `filePath` or `containingType`

❌ **Never rename unrelated symbols that happen to contain the word** - e.g., renaming `Order` shouldn't touch `OrderBy` or `SortOrder`

## Preserve Patterns

Symbols to consider preserving (different concepts that share the word):

| If renaming... | Consider preserving... |
|----------------|------------------------|
| `Order` | `OrderBy`, `SortOrder`, `IOrderedEnumerable` |
| `Task` | `System.Threading.Tasks.Task` |
| `Service` | `IHostedService`, `BackgroundService` |
| `Event` | `EventHandler`, `EventArgs` |

## Example

**User**: "Rename Order to Invoice in this codebase"

**Assistant**:
1. Calls `aura_refactor(analyze: true)` → gets blast radius
2. Shows: "This will rename 10 symbols across 15 files. `OrderBy` and `SortOrder` will be preserved (different concepts). Proceed?"
3. User confirms
4. Executes each rename in order, building after each
5. Renames files using `move_type_to_file`
6. Sweeps for residuals
7. Reports: "Rename complete. Check for any database schema updates needed."
