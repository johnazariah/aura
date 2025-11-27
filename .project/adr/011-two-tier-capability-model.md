# ADR-011: Two-Tier Capability Model

## Status

Accepted

## Date

2025-11-27

## Context

Agents need to declare what they can do so the system can route requests to the right agent. We considered:

1. **Fixed taxonomy** - Predefined list of capabilities, validated on load
2. **Open vocabulary** - Any string, user-defined
3. **Two-tier model** - Fixed capabilities for routing + open tags for display

The key insight: capabilities are an **internal routing mechanism**, not a user-facing concept. The caller (API, workflow, orchestration) needs to know what string to ask for. Users don't type capabilities—they chat, and the system infers.

But users (like mum with her bible study agent) want to create specialized agents without understanding our internal routing vocabulary.

## Decision

**Two-tier model: Capabilities (fixed) + Tags (open).**

### Capabilities (Fixed, for routing)

```csharp
public static class Capabilities
{
    public const string Chat = "chat";
    public const string Digestion = "digestion";
    public const string Analysis = "analysis";
    public const string Coding = "coding";
    public const string Fixing = "fixing";
    public const string Documentation = "documentation";
    public const string Review = "review";
}
```

| Capability | Description |
|------------|-------------|
| `chat` | General conversation (fallback) |
| `digestion` | Turn raw issue text into structured, researched context |
| `analysis` | Break down requirements into implementation plan |
| `coding` | Write/modify code (implementation, tests, refactoring) |
| `fixing` | Iterate on build/test errors until passing |
| `documentation` | Write/update READMEs, CHANGELOGs, API docs |
| `review` | Review code, suggest improvements |

Specialization within `coding` comes from:
- **Languages filter** - `csharp`, `python`, etc. (null = polyglot)
- **Priority** - Lower number = more specialized = selected first
- **System prompt** - What the agent focuses on

### Tags (Open, for display/filtering)

User-defined strings for organization and filtering:

```
bible-study, theology, greek, hebrew
finance, budgeting, receipts
research, papers, citations
```

- Not used for routing
- Displayed in UI for filtering
- No validation (user's choice)

### Agent Definition Example

```markdown
# Bible Study Agent

## Metadata

- **Priority**: 60
- **Provider**: ollama
- **Model**: llama3.2:3b

## Capabilities

- chat

## Tags

- bible-study
- theology
- scripture

## System Prompt

You are a helpful Bible study assistant...
```

This agent:
- Routes via `chat` capability (fixed vocabulary)
- Displays with `#bible-study #theology` tags (user's vocabulary)
- Has priority 60, so it beats generic Chat Agent (priority 80) when both match

## Consequences

### Positive

- **Routing is predictable** - Code uses known capability strings
- **Users have flexibility** - Tags can be anything
- **Validation without rejection** - Unknown capabilities warn but don't fail
- **UI can filter by tags** - Show agents by domain

### Negative

- **Two concepts to explain** - Capabilities vs Tags
- **Users might confuse them** - Put routing info in Tags (harmless, just won't route)

### Mitigation

Clear documentation and examples. The default agents model correct usage.

## References

- [spec/11-agent-discovery.md](../spec/11-agent-discovery.md) - Agent discovery specification
- [spec/01-agents.md](../spec/01-agents.md) - Agent architecture
