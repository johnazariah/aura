# Operational Patterns

Patterns are step-by-step playbooks for complex multi-step operations. They provide deterministic procedures that guide AI agents through tasks like comprehensive renames, test generation, and refactoring.

## What is a Pattern?

A pattern is:
- A **step-by-step procedure** for accomplishing a complex task
- Uses **existing MCP primitives** (no custom code required)
- **Deterministic** - same inputs produce same steps
- **Composable** - patterns can reference other patterns
- **Language-aware** - base patterns with language-specific overlays

A pattern is NOT:
- A persona (that's an agent)
- A prompt template with variables (that's a prompt)
- A one-shot instruction (that's a chat message)

## Available Patterns

### Polyglot (Language-Agnostic)

| Pattern | Description |
|---------|-------------|
| `generate-tests` | Generate comprehensive tests for a class or module |

### Language-Specific

| Pattern | Language | Description |
|---------|----------|-------------|
| `csharp/comprehensive-rename` | C# | Rename domain concept across codebase |
| `csharp/generate-tests` | C# | C#-specific test generation with xUnit, NSubstitute |
| `python/generate-tests` | Python | Python-specific tests with pytest |

## Using Patterns

### Loading a Pattern

Ask the agent to load a pattern:

```
"Load the comprehensive-rename pattern"
```

Or use the MCP tool directly:

```
aura_pattern(operation: "list")                              # See available patterns
aura_pattern(operation: "get", name: "comprehensive-rename") # Load specific pattern
```

### Language Overlays

Patterns support language-specific guidance without duplicating the base pattern:

```
aura_pattern(operation: "get", name: "generate-tests")                  # Base only
aura_pattern(operation: "get", name: "generate-tests", language: "csharp")  # Base + C# overlay
```

The overlay adds language-specific details:
- **C#**: Roslyn-based generation, xUnit patterns, `MockFileSystem` for I/O
- **Python**: pytest patterns, mock library usage

## Pattern Structure

Each pattern follows this structure:

```markdown
# Pattern: [Name]

## When to Use
[Trigger conditions - when should an agent use this pattern?]

## Prerequisites  
[What must be true before starting]

## Steps
1. [First step with tool calls]
2. [Second step with verification]
...

## Anti-patterns
[What NOT to do - common mistakes to avoid]

## Example
[Full example conversation showing the pattern in action]
```

## Comprehensive Rename Pattern

The most commonly used pattern for domain-level renames.

### When to Use

- Renaming a domain concept (not just a single symbol)
- The name appears in types, properties, methods, files, tests
- You want to ensure nothing is missed

### What It Does

1. **Analyze** - Find all related symbols by naming convention
2. **Plan** - Create ordered list of renames (interfaces first, then implementations)
3. **Execute** - Rename each symbol using `aura_refactor`
4. **Verify** - Build after each step
5. **Sweep** - Search for residuals that Roslyn might have missed

### Example

```
You: "Rename User to Customer throughout the codebase"

Agent: Loading comprehensive-rename pattern...

Step 1: Analyzing blast radius...
- IUser → ICustomer (3 references)
- User → Customer (45 references)
- UserService → CustomerService (12 references)
- UserRepository → CustomerRepository (8 references)
- UserDto → CustomerDto (15 references)
- Related files: 12 files to rename

Step 2: Would you like me to proceed with these renames?

You: "Yes"

Agent: Executing in order...
✅ IUser → ICustomer (build passed)
✅ User → Customer (build passed)
✅ UserService → CustomerService (build passed)
... (continues for all symbols)

Step 5: Sweeping for residuals...
Found 2 occurrences in comments that Roslyn missed. Fixing...

Done! All 83 references updated, build passes.
```

## Test Generation Pattern

Generates comprehensive tests for a class or module.

### When to Use

- Need full test coverage for a class
- Want tests for all public methods
- Need language-specific best practices

### Language Overlays

**C# Overlay adds:**
- xUnit test structure
- NSubstitute for mocking
- `MockFileSystem` for file I/O
- `NullLogger<T>` for logging
- Arrange-Act-Assert pattern

**Python Overlay adds:**
- pytest fixtures
- unittest.mock patterns
- Parametrized tests

## Binding Patterns to Stories

When creating a Story from a GitHub issue, you can bind a pattern:

1. Create Story from issue
2. In the workflow panel, click **"Attach Pattern"**
3. Select the pattern (e.g., "comprehensive-rename")
4. The pattern content is automatically included in agent context

This ensures the agent follows the pattern's steps exactly.

## Creating Custom Patterns

Patterns live in the `patterns/` directory:

```
patterns/
├── README.md
├── generate-tests.md           # Polyglot base
├── comprehensive-rename.md     # Polyglot base
├── csharp/
│   ├── generate-tests.md       # C# overlay
│   └── comprehensive-rename.md # C# overlay
└── python/
    └── generate-tests.md       # Python overlay
```

### Pattern Template

```markdown
# Pattern: my-pattern-name

## When to Use
[Clear trigger conditions]

## Prerequisites
- [Condition 1]
- [Condition 2]

## Steps

### Step 1: [Action]
[Description]
```tool
aura_*(operation: "...", ...)
```

### Step 2: [Verification]
[Description]
```tool
aura_validate(operation: "compilation", ...)
```

## Anti-patterns
- ❌ [What not to do]
- ❌ [Common mistake]

## Example
[Full conversation example]
```

### Testing Your Pattern

1. Create the pattern file in `patterns/`
2. Load it: `aura_pattern(operation: "get", name: "my-pattern-name")`
3. Test in a real conversation
4. Refine based on results
