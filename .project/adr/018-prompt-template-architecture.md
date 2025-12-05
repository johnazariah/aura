# ADR-018: Prompt Template Architecture

## Status
Accepted

## Date
2025-12-05

## Context

The system uses multiple types of prompts:

1. **Agent System Prompts** - Defined in agent `.md` files, describe "who the agent is"
2. **Task Prompts** - Defined in `.prompt` files, describe "what to do"
3. **RAG Context** - Retrieved from indexed codebase, provides "relevant information"

There was confusion about where each piece of information should live and how they combine.

## Decision

Establish clear separation of concerns:

### Agent Definition (.md files in /agents)

Defines the agent's identity and capabilities:

- System prompt (persona, expertise, behavior rules)
- Capabilities (what the agent can do)
- Provider/model configuration
- Tool access

### Prompt Templates (.prompt files in /prompts)

Defines task-specific instructions with Handlebars placeholders:

- Task structure and format
- Placeholders for dynamic data: `{{stepName}}`, `{{description}}`
- RAG queries for context retrieval (in frontmatter)
- Output format expectations

### RAG Context Injection

Handled automatically by `ConfigurableAgent.AppendRagContext()`:

- Appended to the system prompt as "## Relevant Context from Knowledge Base"
- No need to include in prompt templates
- Queries defined in prompt template frontmatter

### Message Flow

```
System Message:
  [Agent System Prompt from .md file]
  [RAG Context appended automatically]

User Message:
  [Rendered prompt template with {{placeholders}} filled]
```

## Consequences

### Positive

- Clear separation of concerns
- Agent behavior is consistent across different tasks
- Task instructions are reusable across agents
- RAG queries are prompt-specific and configurable

### Negative

- Need to understand the layered architecture
- RAG context appears in system prompt, not user prompt (may affect some LLM behaviors)
