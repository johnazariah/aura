# Feature: Operational Patterns

**Status:** ✅ Complete
**Completed:** 2025-01-17

## Summary

Added a `patterns/` folder for documenting complex multi-step operations that guide LLM behavior when using Aura MCP tools.

## Problem

Complex operations like "rename a domain concept" or "generate comprehensive tests" require many coordinated steps:
1. Analyze blast radius
2. Execute multiple renames in order
3. Rename files
4. Build and verify
5. Sweep for residuals

When the LLM improvised these steps, it often fell back to text replacement, breaking builds.

## Solution

Created `patterns/` folder with step-by-step procedural documentation:

```
patterns/
├── README.md                    # What patterns are, how to create them
├── comprehensive-rename.md      # Rename domain concepts across codebase
└── generate-tests.md            # Generate unit tests for a class/module
```

Each pattern includes:
- **When to Use** - trigger conditions
- **Prerequisites** - what must be true before starting
- **Steps** - numbered procedure with tool calls
- **Anti-patterns** - what NOT to do
- **Example** - full conversation example

## Key Design Decisions

1. **No code required** - patterns are just markdown documentation
2. **Uses existing primitives** - no new MCP operations needed
3. **Explicit preserve patterns** - no heuristics, user specifies what to skip
4. **Extensible** - users can add their own patterns

## Files Created

| File | Purpose |
|------|---------|
| `patterns/README.md` | Documentation for the patterns concept |
| `patterns/comprehensive-rename.md` | Rename domain concepts |
| `patterns/generate-tests.md` | Generate unit tests |
| `.project/adr/adr-007-operational-patterns.md` | Architecture decision record |

## Related

- ADR-007: Operational Patterns for Complex Multi-Step Tasks
- `mcp-tools-instructions.md` - primitive tool documentation
