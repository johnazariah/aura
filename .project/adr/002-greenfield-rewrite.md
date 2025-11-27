# ADR-002: Greenfield Rewrite from hve-hack

## Status

Accepted

## Date

2025-11-25

## Context

The original implementation (codename: hve-hack / Agent Orchestrator) grew to:

- 17 separate projects
- ~38,000 lines of C#
- Complex orchestration layers (IExecutionPlanner, WorkflowOrchestrator)
- Elaborate validation systems (AgentOutputValidator, CodeValidationTool)
- Multiple abstraction layers that were rarely extended

The system worked, but suffered from:

1. **High coupling** - Changes required touching 5+ projects
2. **Over-abstraction** - Interfaces for things that had one implementation
3. **Wrong focus** - Optimized for orchestration complexity, not user value
4. **Maintenance burden** - Bug fixes cascaded through layers

The question: Can we evolve hve-hack incrementally, or do we need a fresh start?

## Decision

**Start fresh. Port the proven patterns. Delete the rest.**

We will:

1. Create a new repository (aura) with clean architecture
2. Port only the code that proved valuable:
   - Markdown agent format and parser
   - Ollama HTTP client patterns
   - Provider abstraction model
   - Result<T,E> error handling
3. Delete everything else:
   - Execution planner
   - Workflow state machines
   - Agent output validation
   - Complex plugin discovery
4. Maintain a "greenfield mindset" - no backward compatibility concerns

### What Gets Ported

| Keep (Patterns) | Delete (Complexity) |
|-----------------|---------------------|
| Markdown agent format | 17-project structure |
| Ollama HTTP protocol | IExecutionPlanner |
| Result<T,E> pattern | WorkflowOrchestrator |
| Provider abstraction | AgentOutputValidator |
| Agent metadata model | Plugin discovery service |
| Git worktree concepts | Workflow state machines |

## Consequences

### Positive

- **Clean slate** - No legacy code weighing down decisions
- **Right-sized** - 4-6 projects instead of 17
- **Faster iteration** - Less code to maintain and understand
- **Correct abstractions** - Design for actual needs, not speculative
- **Fresh tests** - Test suite built for new architecture

### Negative

- **Lost investment** - ~38k lines of code "thrown away"
- **Re-implementation time** - Some features need rebuilding
- **Knowledge loss** - Edge cases discovered in hve-hack may be forgotten
- **Parallel maintenance** - hve-hack exists until aura is feature-complete

### Mitigations

- hve-hack remains available for reference
- Port code surgically when patterns prove valuable
- Document decisions that were hard-won in hve-hack
- Use ADRs to capture institutional knowledge

## Alternatives Considered

### Incremental Refactoring

- **Pros**: Preserve investment, lower risk
- **Cons**: Technical debt makes each refactor harder, never escape the 17-project prison
- **Rejected**: The architecture fundamentally doesn't match our new understanding

### Fork and Simplify

- **Pros**: Start with working code, remove what's not needed
- **Cons**: Deletion is harder than creation, legacy assumptions baked in
- **Rejected**: Easier to build right than to fix wrong

## References

- [ORIGIN-STORY.md](../ORIGIN-STORY.md) - Detailed narrative of the decision
- hve-hack repository at c:\work\hve-hack (reference only)
