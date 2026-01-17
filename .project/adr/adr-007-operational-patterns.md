# ADR-007: Operational Patterns for Complex Multi-Step Tasks

**Status:** Accepted
**Date:** 2025-01-17
**Deciders:** @user, @copilot

## Context

During implementation of a "Workflow → Story" rename, we observed that:

1. The primitive `aura_refactor(operation: "rename")` works well for single symbols
2. Complex operations (renaming a domain concept + related types + files) require many coordinated steps
3. The LLM fell back to error-prone text replacement when the process became complex
4. Similar patterns exist for test generation, interface extraction, etc.

We considered several approaches:
- **Option A**: Monolithic compound operations (e.g., `comprehensive_rename`)
- **Option B**: More primitives + better composition
- **Option C**: Declarative recipe/playbook execution engine
- **Option D**: Better LLM instructions using existing primitives

## Decision

We chose **Option D + primitives as needed**: Create a `patterns/` folder containing step-by-step procedural documentation that guides LLM behavior for complex operations.

A pattern is:
- A **step-by-step procedure** for accomplishing a complex task
- Uses **existing MCP primitives** (no custom code required)
- **Deterministic** - same inputs → same steps
- **Composable** - patterns can reference other patterns

A pattern is NOT:
- A persona (that's an agent in `agents/`)
- A prompt template with variables (that's in `prompts/`)
- Primitive tool documentation (that's in `mcp-tools-instructions.md`)
- Custom code that needs deployment

## Consequences

### Positive
- No code changes required for new compound operations
- Patterns are shareable and version-controlled
- LLM follows documented steps rather than improvising
- Easy to add new patterns (just create a `.md` file)
- Users can create custom patterns for their workflows

### Negative
- LLM may still deviate from patterns if not properly loaded
- Patterns need to be discovered/referenced by the LLM
- No runtime enforcement (unlike a code-based compound operation)

### Mitigations
- Reference patterns in `mcp-tools-instructions.md` for discoverability
- Keep patterns focused and actionable
- Add anti-patterns section to prevent common mistakes

## Alternatives Considered

### Monolithic Compound Operations
Adding `aura_refactor(operation: "comprehensive_rename")` as a single API call.

**Rejected because:**
- Every new compound operation requires C# code + deployment
- Less flexible than LLM-driven composition
- Harder to customize per-project

### Declarative Recipe Engine
YAML-based recipes executed by a dedicated engine.

**Rejected because:**
- Requires building a new execution engine
- Adds complexity without clear benefit over LLM following docs
- May revisit if patterns become more complex

## Related

- [patterns/README.md](../../patterns/README.md) - Pattern documentation
- [patterns/comprehensive-rename.md](../../patterns/comprehensive-rename.md) - First pattern
- [patterns/generate-tests.md](../../patterns/generate-tests.md) - Second pattern
