# Creating Custom Agents

Agents in Aura are defined as Markdown files. This guide shows you how to create and customize agents.

## What is an Agent?

An agent is an AI persona with:
- A **system prompt** that defines its behavior
- **Capabilities** that determine what tasks it handles
- **Metadata** for configuration (priority, reflection, provider)

## Agent File Structure

Agents are Markdown files in the `agents/` directory:

```markdown
# Agent Name

A brief description of what this agent does.

## Metadata

- **Priority**: 70
- **Reflection**: true

## Capabilities

- capability-1
- capability-2

## Tags

- tag1
- tag2

## System Prompt

You are an expert at...

User's request: {{context.Prompt}}

```

### Sections Explained

| Section | Required | Description |
|---------|----------|-------------|
| **Title (H1)** | Yes | Agent name (used as identifier) |
| **Description** | No | Brief explanation under the title |
| **Metadata** | No | Configuration options |
| **Capabilities** | Yes | What tasks this agent handles |
| **Tags** | No | For filtering and discovery |
| **System Prompt** | Yes | The actual prompt sent to the LLM |

## Metadata Options

| Option | Type | Description |
|--------|------|-------------|
| `Priority` | Number | Lower = preferred when multiple agents match (default: 50) |
| `Reflection` | Boolean | Enable self-critique on responses |
| `Provider` | String | Specific LLM provider (e.g., `openai/gpt-4o`) |

### Priority

When multiple agents have the same capability, the one with the **lowest priority number** is used:

```markdown
## Metadata
- **Priority**: 30  # Will be preferred over Priority: 70
```

### Reflection

Enables self-critique. The agent reviews its own response and may correct it:

```markdown
## Metadata
- **Reflection**: true
```

This adds latency but improves quality for complex tasks.

### Provider Override

Force a specific LLM for this agent:

```markdown
## Metadata
- **Provider**: openai/gpt-4o
```

Format: `provider/model` (e.g., `ollama/qwen2.5-coder:7b`, `azure/gpt-4`)

## Capabilities

Capabilities are keywords that match agents to tasks:

```markdown
## Capabilities

- coding
- testing
- documentation
```

### Built-in Capabilities

| Capability | Description |
|------------|-------------|
| `coding` | Writing and modifying code |
| `testing` | Creating and running tests |
| `documentation` | Writing docs and comments |
| `code-review` | Reviewing code changes |
| `analysis` | Analyzing requirements |
| `chat` | General conversation |

## Template Variables

The system prompt supports Handlebars templates:

| Variable | Description |
|----------|-------------|
| `{{context.Prompt}}` | User's message |
| `{{context.WorkspacePath}}` | Current workspace path |
| `{{context.Selection}}` | Selected code (if any) |
| `{{context.File}}` | Current file path |
| `{{context.Language}}` | Programming language |

### Example

```markdown
## System Prompt

You are an expert {{context.Language}} developer.

Working in: {{context.WorkspacePath}}

{{#if context.Selection}}
Selected code:
```
{{context.Selection}}
```
{{/if}}

User's request: {{context.Prompt}}
```

## Language-Specific Agents

Create agents that only activate for specific languages:

```markdown
# Rust Agent

Expert Rust developer.

## Languages

- rust

## Capabilities

- coding
- testing

## System Prompt
...
```

The `Languages` section restricts the agent to those languages.

### Language Overlays

You can also create language-specific versions in subdirectories:

```
agents/
├── coding-agent.md           # Polyglot base
└── languages/
    ├── csharp.yaml           # C# guidance
    ├── python.yaml           # Python guidance
    └── rust.yaml             # Rust guidance
```

Language files are YAML and provide additional context to the base agent.

## Example Agents

### Simple Code Review Agent

```markdown
# Code Review Agent

Reviews code for quality, security, and best practices.

## Metadata

- **Priority**: 40
- **Reflection**: true

## Capabilities

- code-review

## System Prompt

You are an experienced code reviewer focused on:
- Code quality and readability
- Security vulnerabilities
- Performance issues
- Best practices

Review the following code and provide constructive feedback:

{{context.Selection}}

Be specific about issues and suggest improvements.
```

### Documentation Agent

```markdown
# Documentation Agent

Writes clear, comprehensive documentation.

## Metadata

- **Priority**: 50

## Capabilities

- documentation

## Tags

- docs
- readme
- comments

## System Prompt

You are a technical writer who creates clear, user-friendly documentation.

When writing docs:
1. Use simple, direct language
2. Include examples
3. Structure with clear headings
4. Consider the audience's expertise level

User's request: {{context.Prompt}}
```

## Hot Reload

Agents hot-reload automatically. When you save an agent file:

1. Aura detects the change
2. Reloads the agent definition
3. New conversations use the updated agent

No restart required.

## Testing Your Agent

1. Create or edit the agent file
2. Save it to `agents/`
3. In VS Code, open Chat and select your agent
4. Send a test message
5. Iterate on the prompt based on results

## Best Practices

### Be Specific

❌ "You are helpful"  
✅ "You are an expert Python developer who writes clean, type-hinted code following PEP 8"

### Include Examples

Show the agent what good output looks like in your system prompt.

### Use Reflection for Quality

Enable reflection for agents handling complex tasks:

```markdown
## Metadata
- **Reflection**: true
```

### Set Appropriate Priority

- Lower priority (10-30): Specialized agents you want to prefer
- Medium priority (40-60): General-purpose agents
- Higher priority (70-90): Fallback agents

### Test with Real Tasks

Create agents based on actual use cases, then refine based on real usage.
