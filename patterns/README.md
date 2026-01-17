# Patterns

Operational patterns for multi-step tasks. These are procedural playbooks that guide the LLM through complex operations using the Aura MCP tools.

## What is a Pattern?

A pattern is:
- A **step-by-step procedure** for accomplishing a complex task
- Uses **existing MCP primitives** (no custom code required)
- **Deterministic** - same inputs → same steps
- **Composable** - patterns can reference other patterns

A pattern is NOT:
- A persona (that's an agent)
- A prompt template with variables (that's a prompt)
- Primitive tool documentation (that's in mcp-tools-instructions.md)

## Available Patterns

| Pattern | Purpose |
|---------|---------|
| [comprehensive-rename.md](comprehensive-rename.md) | Rename a domain concept across entire codebase |
| [generate-tests.md](generate-tests.md) | Generate comprehensive tests for a class/module |

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
