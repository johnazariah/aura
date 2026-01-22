# ADR-022: Multi-Agent Orchestration Pattern

**Status**: Accepted
**Date**: 2026-01-20
**Deciders**: Aura Team

## Context

As agents tackle complex, multi-step tasks, they encounter several challenges:
1. **Context exhaustion** - Long reasoning chains fill the context window
2. **Scope creep** - Single agents try to handle tasks beyond their capacity
3. **Brittleness** - Single failures can derail entire executions

The industry is evolving toward "agentic" patterns with hierarchical task delegation. Our current ReAct executor treats each execution as monolithic.

## Decision

Implement hierarchical multi-agent orchestration through three pillars:

### 1. Sub-Agent Spawning

Add a `spawn_sub_agent` tool that allows agents to delegate subtasks:

```json
{
  "tool": "spawn_sub_agent",
  "input": {
    "agentId": "coding-agent",
    "task": "Implement the validation logic for email addresses",
    "context": "Working in UserService.cs, need RFC 5322 compliance"
  }
}
```

The child agent runs with fresh context and returns a structured result. The parent receives only the final answer, not the full reasoning chain.

### 2. Token Budget Tracking

Every ReAct execution gets a `TokenTracker` that monitors usage:

```csharp
var tracker = new TokenTracker(budget: 100_000);
tracker.Add(promptTokens + responseTokens);

if (tracker.IsAboveThreshold(0.7))
{
    // Inject budget warning into prompt
}
```

Agents can also explicitly check their budget via `check_token_budget` tool.

### 3. Intelligent Retry Loops

Tool failures and incomplete actions trigger automatic retry with context:

```
The previous action had this result:
Error: File already exists at path/to/file.cs

Consider adjusting your approach or using a different tool.
What is your next action?
```

Maximum retries and delay are configurable per execution.

## Consequences

### Positive
- Complex tasks broken into manageable pieces
- Each agent operates within healthy context limits
- Failures are recovered gracefully
- Agents learn to delegate appropriately

### Negative
- Increased total token usage (multiple contexts)
- More complex execution traces to debug
- Coordination overhead between agents

### Neutral
- Agents need to learn when to spawn vs continue
- Budget warnings guide but don't force behavior

## Implementation

| Component | Status |
|-----------|--------|
| TokenTracker | ✅ Complete |
| SpawnSubAgentTool | ✅ Complete |
| CheckTokenBudgetTool | ✅ Complete |
| Budget warnings injection | ✅ Complete |
| Retry loops | ✅ Complete |

See: [features/upcoming/agentic-execution-v2.md](../features/upcoming/agentic-execution-v2.md)