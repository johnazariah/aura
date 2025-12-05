# ADR-016: Configurable RAG Queries in Prompt Templates

## Status
Accepted

## Date
2025-12-05

## Context

When executing workflow steps, the system needs to query the RAG (Retrieval-Augmented Generation) index to provide relevant codebase context to agents. Initially, RAG queries were hardcoded in `WorkflowService.cs` with methods like `BuildRagQueriesForStep()` and `BuildRagQueriesForAnalysis()`.

This approach had several problems:
1. **Required recompilation** to change RAG queries
2. **Violated the principle** that prompt-related configuration should be hot-reloadable
3. **Inconsistent with** the existing pattern of using `.prompt` files for prompt configuration

## Decision

Extend the prompt template frontmatter to support a `ragQueries` field that defines the RAG queries to use when that prompt is executed.

### Frontmatter Format

```yaml
---
description: Executes a documentation workflow step
ragQueries:
  - "README documentation getting started installation"
  - "project description purpose features overview"
  - "architecture design structure packages components"
---
```

### Implementation

1. **PromptTemplate** - Added `RagQueries` property (`IReadOnlyList<string>`)
2. **PromptRegistry** - Extended frontmatter parsing to read `ragQueries:` as a YAML list
3. **IPromptRegistry** - Added `GetRagQueries(string name)` method
4. **WorkflowService** - Updated to prefer prompt-defined queries, falling back to hardcoded defaults

### Query Resolution Order

1. Check if the prompt template defines `ragQueries`
2. If defined and non-empty, use those queries
3. Otherwise, fall back to capability-specific defaults in code

## Consequences

### Positive
- RAG queries can be tuned without recompiling
- Each prompt can have specialized queries for its use case
- Consistent with the hot-reloadable prompt pattern
- Easy to experiment with different query strategies

### Negative
- Simple YAML list parsing (not full YAML parser) - complex structures not supported
- Fallback logic adds slight complexity
- Need to restart API to pick up prompt changes (no file watcher yet)

## Related ADRs
- ADR-004: Markdown Agent Definitions
- ADR-008: Local RAG Foundation
