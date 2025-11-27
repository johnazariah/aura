# ADR-004: Markdown Agent Definitions

## Status

Accepted

## Date

2025-11-25

## Context

Agents are the core capability units in Aura. Each agent has:

- Metadata (name, description, model, provider)
- Capabilities (what it can do)
- Tools (what functions it can call)
- System prompt (how it behaves)

The question: How should agents be defined? Options include:

1. C# classes with attributes
2. JSON/YAML configuration files
3. Markdown files with structured sections
4. Database entries

## Decision

**Agents are defined in Markdown files with structured sections.**

### Format

```markdown
# Agent Name

## Metadata

- **Type**: Coder
- **Name**: Coding Agent
- **Version**: 1.0.0
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b
- **Temperature**: 0.7
- **Description**: Expert polyglot developer...

## Capabilities

- coding
- code-generation
- best-practices

## Tools Available

**validate_code(files: string[], language: string)**
- Validates code files using language-specific tooling

## System Prompt

You are an expert polyglot developer...

{{context.WorkspacePath}}
{{context.Prompt}}
```

### Key Features

1. **Human-readable** - Non-developers can understand and modify agents
2. **Hot-reloadable** - Drop a file in `agents/`, it becomes available
3. **Template variables** - `{{context.X}}` replaced at runtime
4. **Portable** - Copy agent files between installations
5. **Version-controllable** - Track agent changes in git

### Parser Implementation

The `MarkdownAgentLoader` extracts:

- Metadata section → `AgentDefinition` properties
- Capabilities section → `List<string>` of capability tags
- Tools section → `List<string>` of tool names
- System Prompt section → Template string

## Consequences

### Positive

- **Accessibility** - Anyone can create or modify agents
- **No compilation** - Changes take effect without rebuilding
- **Documentation-as-code** - Agent files are self-documenting
- **Git-friendly** - Meaningful diffs for agent changes
- **Shareable** - Community can share agent definitions

### Negative

- **Parse errors** - Malformed markdown can fail silently
- **No type safety** - Typos in metadata not caught at compile time
- **Limited validation** - Can reference non-existent tools/models
- **Performance** - File parsing on every reload

### Mitigations

- Logging for parse failures
- Validation at load time (model exists, tools registered)
- Caching of parsed definitions
- VS Code extension for agent editing with validation

## Alternatives Considered

### C# Classes with Attributes

```csharp
[Agent("coding-agent", Model = "qwen2.5-coder:7b")]
public class CodingAgent : IAgent { }
```

- **Pros**: Type safety, compile-time validation
- **Cons**: Requires compilation, not accessible to non-developers
- **Rejected**: Conflicts with "hot-reloadable" goal

### JSON/YAML Configuration

```json
{
  "id": "coding-agent",
  "model": "qwen2.5-coder:7b",
  "systemPrompt": "..."
}
```

- **Pros**: Standard formats, good tooling
- **Cons**: Less readable for long prompts, escaping issues
- **Rejected**: Markdown better for multi-paragraph prompts

### Database Storage

- **Pros**: Query capabilities, UI editing
- **Cons**: Not portable, requires running database
- **Rejected**: Agents should work without database

## References

- [agents/coding-agent.md](../../agents/coding-agent.md) - Example agent
- [MarkdownAgentLoader.cs](../../src/Aura.Foundation/Agents/MarkdownAgentLoader.cs) - Parser implementation
- Pattern originated in hve-hack, proven valuable
