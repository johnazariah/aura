# Patterns

Operational patterns for multi-step tasks. These are procedural playbooks that guide the LLM through complex operations using the Aura MCP tools.

## What is a Pattern?

A pattern is:
- A **step-by-step procedure** for accomplishing a complex task
- Uses **existing MCP primitives** (no custom code required)
- **Deterministic** - same inputs → same steps
- **Composable** - patterns can reference other patterns
- **Polyglot base + language overlays** - language-agnostic workflow with optional language-specific guidance

A pattern is NOT:
- A persona (that's an agent)
- A prompt template with variables (that's a prompt)
- Primitive tool documentation (that's in mcp-tools-instructions.md)

## Available Patterns

### Polyglot (Base Patterns)

| Pattern | Description | Overlays |
|---------|-------------|----------|
| [generate-tests.md](generate-tests.md) | Generate comprehensive tests for a class/module | csharp, python |

### Language-Specific

| Pattern | Language | Description |
|---------|----------|-------------|
| [csharp/comprehensive-rename.md](csharp/comprehensive-rename.md) | C# | Rename domain concept across codebase (Roslyn) |

## Language Overlays

Language overlays provide language-specific guidance without duplicating the base pattern.

```
patterns/
├── generate-tests.md           # Polyglot base (~100 lines)
├── comprehensive-rename.md     # Polyglot base
├── csharp/
│   └── generate-tests.md       # C#-specific: Roslyn, xUnit, MockFileSystem
└── python/
    └── generate-tests.md       # Python-specific: pytest, mock
```

### Loading with Overlay

```
# Base only (~400 tokens)
aura_pattern(operation: "get", name: "generate-tests")

# Base + C# overlay (~1200 tokens)
aura_pattern(operation: "get", name: "generate-tests", language: "csharp")
```

### Token Efficiency

| Scenario | Tokens |
|----------|--------|
| Base pattern only | ~400 |
| Base + C# overlay | ~1200 |
| Base + Python overlay | ~600 |

Non-C# projects save ~800 tokens per pattern load.

## Pattern Structure

Each pattern file follows this structure:

```markdown
# Pattern: [Name]

## When to Use
[Trigger conditions]

## Prerequisites
[What must be true before starting]

## Steps
1. [First step]
2. [Second step]
...

## Anti-patterns
[What NOT to do]

## Example
[Full example conversation]
```

## Creating New Patterns

1. Create a new `.md` file in this folder
2. Follow the structure above
3. Test the pattern in a real conversation
4. Update this README with the new pattern
