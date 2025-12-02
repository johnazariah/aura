# ADR-012: Tool-Using Agents with ReAct Loop

## Status

Accepted

## Date

2025-12-02

## Context

Aura needs agents that can perform complex, multi-step operations like code generation with validation, test writing, and refactoring. Simple prompt-in/text-out agents are insufficient for tasks that require:

- Reading and understanding existing code
- Making targeted modifications
- Validating changes compile
- Running tests to verify correctness

We evaluated three approaches:

1. **Hybrid (LLM + Validation Loop)** - LLM generates freely, Roslyn validates, iterate on errors
2. **Roslyn-First (Structured Intent)** - LLM outputs structured commands, Roslyn applies them
3. **Tool-Using Agent** - LLM has access to tools, decides which to use in multi-turn conversation

## Decision

**Adopt Tool-Using Agents with ReAct execution loop.**

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ReAct Execution Loop                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Agent receives task + available tools                   │
│  2. Agent THINKS: "What do I need to do?"                   │
│  3. Agent ACTS: Outputs tool call (e.g., read_file)         │
│  4. System executes tool, returns OBSERVATION               │
│  5. Loop continues until agent says "DONE"                  │
│  6. Final response returned to caller                       │
│                                                              │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐                 │
│  │  THINK  │───►│   ACT   │───►│ OBSERVE │───┐             │
│  └─────────┘    └─────────┘    └─────────┘   │             │
│       ▲                                       │             │
│       └───────────────────────────────────────┘             │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Why ReAct over Function Calling

- **Model-agnostic**: Works with any LLM, not just those with function-calling support
- **User control**: Local-first means users may run various Ollama models
- **Debuggable**: Thought/Action/Observation trace is human-readable
- **Flexible**: Agent adapts strategy based on observations

### Tool Framework Location

Tools are **Foundation-level** (`Aura.Foundation`):
- `ITool` interface and `IToolRegistry` in Foundation
- Any module can register tools
- Any agent can use tools

Module-specific tools (e.g., Roslyn) are registered by their module but use the Foundation framework.

## Consequences

### Positive

- Maximum flexibility - agent can adapt to unexpected situations
- Model-agnostic - works with any Ollama model
- Extensible - new tools can be added without changing agent code
- Transparent - full trace of agent reasoning visible to user

### Negative

- More LLM round-trips than single-turn function calling
- Requires well-designed tool set to be effective
- Agent may take suboptimal paths (mitigated by human-in-the-loop)

### Mitigations

- Human reviews each step in MVP (can add autonomous mode later)
- Tools designed to be composable and self-describing
- Full transparency shows user what agent is doing and why

## References

- [ReAct: Synergizing Reasoning and Acting in Language Models](https://arxiv.org/abs/2210.03629)
- ADR-011: Two-Tier Capability Model (agent selection)
