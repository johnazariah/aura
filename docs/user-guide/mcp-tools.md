# MCP Tools Reference

Aura exposes a comprehensive set of tools via the Model Context Protocol (MCP). These tools enable AI assistants like GitHub Copilot to interact with your codebase semantically.

## What is MCP?

MCP (Model Context Protocol) is a standard for exposing capabilities to AI assistants. When you configure Aura as an MCP server, GitHub Copilot can:

- Search your codebase semantically
- Navigate code relationships
- Generate code using project context
- Run refactoring operations
- Execute verification checks

## Setup

Add to your VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "aura": {
        "url": "http://localhost:5300/mcp"
      }
    }
  }
}
```

## Tool Categories

### Code Search (`aura_search`)

Semantic search across your indexed codebase. Returns relevant code chunks with similarity scores.

**Operations:**
- Search by concept, symbol name, or keyword
- Filter by content type (code, docs, config)
- Automatic worktree resolution

**Example:**
```
"Search for authentication logic"
→ Finds AuthService.cs, login methods, auth middleware
```

### Code Navigation (`aura_navigate`)

Find code relationships - callers, implementations, usages.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `callers` | Find all methods that call a function |
| `implementations` | Find implementations of an interface |
| `derived_types` | Find classes that inherit from a type |
| `usages` | Find all usages of a symbol |
| `references` | Find references to a symbol |
| `definition` | Jump to symbol definition |
| `by_attribute` | Find types/methods with a specific attribute |
| `extension_methods` | Find extension methods for a type |

**Example:**
```
"Find all callers of UserService.GetById"
→ Lists every method that calls GetById
```

### Code Inspection (`aura_inspect`)

Explore type structure and project contents.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `type_members` | List all members of a class/interface |
| `list_types` | List types in a project or namespace |

**Example:**
```
"Show me all members of the OrderService class"
→ Lists constructors, methods, properties
```

### Code Validation (`aura_validate`)

Check compilation and run tests.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `compilation` | Build the solution and check for errors |
| `tests` | Run unit tests with optional filter |

**Example:**
```
"Build the solution and run tests"
→ Shows build result and test pass/fail count
```

### Refactoring (`aura_refactor`)

Semantic code transformations with blast radius analysis.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `rename` | Rename a symbol across the codebase |
| `extract_method` | Extract code into a new method |
| `extract_variable` | Extract expression into a variable |
| `extract_interface` | Create interface from class members |
| `change_signature` | Add/remove parameters from a method |
| `safe_delete` | Delete a symbol if it has no usages |
| `move_type_to_file` | Move a type to its own file |

**Analyze Mode:**

By default, refactoring shows a **blast radius analysis** before making changes:

```
Rename: User → Customer
Blast radius: 64 references across 12 files
Related symbols: UserService, IUserRepository, UserDto
```

Then confirm to execute the actual refactoring.

### Code Generation (`aura_generate`)

Create new code elements with proper structure.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `create_type` | Create a new class/interface/record |
| `tests` | Generate comprehensive unit tests |
| `implement_interface` | Implement interface members |
| `constructor` | Generate constructor |
| `property` | Add a property to a class |
| `method` | Add a method to a class |

**Example:**
```
"Generate tests for OrderService"
→ Creates OrderServiceTests.cs with test cases
```

### Workflow Management (`aura_workflow`)

Manage development workflows (stories).

**Operations:**

| Operation | Description |
|-----------|-------------|
| `list` | List all active workflows |
| `get` | Get details of a specific workflow |
| `get_by_path` | Find workflow for a worktree path |
| `create` | Create a new workflow |
| `enrich` | Enrich workflow with issue details |
| `update_step` | Update a workflow step |
| `complete` | Complete with squash merge and PR |

### Operational Patterns (`aura_pattern`)

Load step-by-step playbooks for complex tasks.

**Operations:**

| Operation | Description |
|-----------|-------------|
| `list` | Show available patterns |
| `get` | Load a specific pattern |

**Example:**
```
"Load the comprehensive-rename pattern"
→ Returns step-by-step procedure for domain renames
```

### Documentation Search (`aura_docs`)

Search Aura's indexed documentation using semantic search.

**Purpose:**
- Self-service help for configuration, troubleshooting, best practices
- Find examples and usage patterns
- Understand Aura capabilities without asking the user

**Example:**
```
"How do I configure OpenAI GPT-4?"
→ Returns relevant docs about LLM configuration
```

**Details:** See [aura_docs reference](../mcp-tools/aura_docs.md) for complete documentation with JSON-RPC examples.

## Best Practices

### Use Semantic Tools First

Before reading files manually, use Aura's semantic tools:

| Instead of... | Use... |
|---------------|--------|
| Reading many files to understand a class | `aura_inspect(operation: "type_members")` |
| Grep searching for usages | `aura_navigate(operation: "usages")` |
| Manual find/replace for renames | `aura_refactor(operation: "rename")` |

### Path Resolution

Always use absolute paths anchored to the workspace root. When in a worktree, use the worktree path—Aura automatically resolves to the shared index.

### Refactoring Safety

1. Always analyze first (default behavior)
2. Review the blast radius
3. Get confirmation before executing
4. Build after each refactoring step
5. Sweep for residuals with grep

## Language Support

| Feature | C# | TypeScript | Python | Go | Rust |
|---------|----|-----------:|-------:|---:|-----:|
| `aura_search` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `aura_navigate` | ✅ (Roslyn) | ⚠️ (basic) | ⚠️ (basic) | ⚠️ (basic) | ⚠️ (basic) |
| `aura_inspect` | ✅ (Roslyn) | ⚠️ (basic) | ⚠️ (basic) | ⚠️ (basic) | ⚠️ (basic) |
| `aura_refactor` | ✅ (Roslyn) | ❌ | ⚠️ (Rope) | ❌ | ❌ |
| `aura_generate` | ✅ (Roslyn) | ❌ | ❌ | ❌ | ❌ |
| `aura_validate` | ✅ | ✅ | ✅ | ✅ | ✅ |

**Legend:** ✅ Full support | ⚠️ Basic/partial | ❌ Not supported

C# has the richest support via Roslyn. Other languages have semantic search and validation, with navigation based on Tree-sitter parsing.
