# Tool Execution for Agents

**Status:** ✅ Complete
**Completed:** 2026-01-03
**Created**: 2025-12-05
**Updated**: 2026-01-03
**Author**: AI-assisted specification

## Implementation Summary

Tool execution for agents is now fully implemented with native LLM function calling support:

1. **LLM Function Calling** - All providers (Azure OpenAI, OpenAI, Ollama) now support `ChatWithFunctionsAsync` with native tool/function calling
2. **ConfigurableAgent Tool Loop** - Agents automatically resolve tools from their definition and execute them in a loop until completion
3. **Human-in-the-Loop** - `IToolConfirmationService` allows tools requiring confirmation to be approved/rejected
4. **Auto-Approve Mode** - `AutoApproveToolConfirmationService` auto-approves safe tools and rejects dangerous ones

### Key Files Created/Modified

- `src/Aura.Foundation/Llm/FunctionCalling.cs` - FunctionDefinition, FunctionCall, LlmFunctionResponse records
- `src/Aura.Foundation/Llm/ILlmProvider.cs` - Added ChatWithFunctionsAsync method
- `src/Aura.Foundation/Llm/AzureOpenAiProvider.cs` - Function calling implementation
- `src/Aura.Foundation/Llm/OpenAiProvider.cs` - Function calling implementation
- `src/Aura.Foundation/Llm/OllamaProvider.cs` - Function calling implementation
- `src/Aura.Foundation/Agents/ConfigurableAgent.cs` - Tool execution loop
- `src/Aura.Foundation/Agents/ConfigurableAgentFactory.cs` - Updated to inject tool dependencies
- `src/Aura.Foundation/Tools/IToolConfirmationService.cs` - Confirmation service interface and auto-approve implementation

---

## Problem Statement

Agents can declare tools in their markdown definitions (e.g., `file.write`, `file.read`), but the runtime doesn't actually execute these tools. When an agent needs to write a file (like a README), it can only output text describing what to do rather than actually performing the action.

Additionally, **prompt templates** (`.prompt` files) need a way to specify which tools should be available during execution of that specific task, independent of the agent's default tool set.

## Current State

### What Exists
- `WriteFileTool`, `ReadFileTool`, and 15+ other tools in `Aura.Module.Developer/Tools/`
- Agent definitions can include `## Tools Available` section listing tools
- `AgentDefinition.Tools` property stores the list of tool names
- `AgentOutput.ToolCalls` property exists for recording tool invocations
- `IToolRegistry` for discovering available tools
- Prompt templates have YAML frontmatter with `ragQueries:` for context retrieval

### What's Missing
1. **Tool binding**: No code connects an agent's declared tools to actual tool instances
2. **Tool execution loop**: `ConfigurableAgent.ExecuteAsync` calls LLM once and returns - no loop to handle tool calls
3. **LLM function calling**: Providers don't pass tool schemas to the LLM or parse tool call responses
4. **Human-in-the-loop for tools**: Tools like `file.write` have `RequiresConfirmation = true` but no confirmation flow exists
5. **Prompt-level tool declaration**: No `tools:` frontmatter in `.prompt` files to specify task-specific tools

## Part 1: Prompt Template Tool Frontmatter

### Design

Add `tools:` to prompt template YAML frontmatter, parallel to existing `ragQueries:`:

```yaml
---
description: Executes a documentation workflow step
ragQueries:
  - "README documentation getting started"
  - "project structure architecture"
tools:
  - file.read
  - file.write
  - git.status
---
Execute this documentation step:
...
```

### Implementation

#### 1. PromptTemplate Record

Add `Tools` property to `PromptTemplate`:

```csharp
public sealed class PromptTemplate
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Template { get; init; }
    public string? SourcePath { get; init; }
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> RagQueries { get; init; } = [];
    public IReadOnlyList<string> Tools { get; init; } = [];  // NEW
}
```

#### 2. PromptRegistry Parsing

Update frontmatter parsing to handle `tools:` list:

```csharp
// In LoadPromptFile:
var tools = new List<string>();
var inTools = false;

// ... existing parsing loop ...
if (inTools && trimmed.StartsWith("-"))
{
    var tool = trimmed[1..].Trim().Trim('"', '\'');
    if (!string.IsNullOrEmpty(tool))
    {
        tools.Add(tool);
    }
    continue;
}

if (trimmed.StartsWith("tools:", StringComparison.OrdinalIgnoreCase))
{
    inTools = true;
    inRagQueries = false;  // End ragQueries section
}
```

#### 3. IPromptRegistry Interface

Add method to retrieve tools for a prompt:

```csharp
public interface IPromptRegistry
{
    // Existing methods...
    IReadOnlyList<string> GetRagQueries(string name);
    
    // NEW
    IReadOnlyList<string> GetTools(string name);
}
```

### Tool Precedence

When executing a step, tools come from multiple sources. Precedence (highest to lowest):

1. **Prompt template `tools:`** - Task-specific tools (e.g., `step-execute-documentation.prompt`)
2. **Agent definition `## Tools Available`** - Agent's default tool set
3. **Empty** - No tools available (current behavior)

The runtime should **merge** these, with prompt-level tools taking priority for conflicts.## Proposed Solution

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     ConfigurableAgent                            │
├─────────────────────────────────────────────────────────────────┤
│  1. Build messages from context                                  │
│  2. Resolve tools from agent's Tools list via IToolRegistry     │
│  3. Convert tools to LLM function schema                        │
│  4. Call LLM with messages + functions                          │
│  5. If response contains tool_calls:                            │
│     a. For each tool call:                                      │
│        - If tool.RequiresConfirmation → request approval        │
│        - Execute tool with parsed arguments                      │
│        - Add tool result to messages                             │
│     b. Go to step 4 (loop until no more tool calls)             │
│  6. Return final text response + all tool calls made            │
└─────────────────────────────────────────────────────────────────┘
```

### Component Changes

#### 1. ILlmProvider Enhancement

```csharp
public interface ILlmProvider
{
    // Existing
    Task<LlmResponse> ChatAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken ct = default);
    
    // New: Chat with function calling support
    Task<LlmResponse> ChatWithFunctionsAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        IEnumerable<FunctionDefinition> functions,
        double temperature = 0.7,
        CancellationToken ct = default);
}

public record FunctionDefinition(
    string Name,
    string Description,
    JsonSchema Parameters);

public record LlmResponse(
    string Content,
    int TokensUsed,
    IReadOnlyList<FunctionCall>? FunctionCalls = null);

public record FunctionCall(
    string Name,
    string ArgumentsJson);
```

#### 2. Tool Resolution in ConfigurableAgent

```csharp
public ConfigurableAgent(
    AgentDefinition definition,
    ILlmProviderRegistry providerRegistry,
    IToolRegistry toolRegistry,  // NEW
    IHandlebars handlebars,
    ILogger<ConfigurableAgent> logger)
{
    // Resolve tools at construction time
    _tools = definition.Tools
        .Select(name => toolRegistry.GetTool(name))
        .Where(t => t != null)
        .ToList();
}
```

#### 3. Tool Execution Loop

```csharp
public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
{
    var messages = BuildMessages(context);
    var functions = _tools.Select(t => t.ToFunctionDefinition()).ToList();
    var toolCalls = new List<ToolCall>();
    
    const int maxIterations = 10; // Prevent infinite loops
    for (int i = 0; i < maxIterations; i++)
    {
        var response = await provider.ChatWithFunctionsAsync(
            _definition.Model, messages, functions, _definition.Temperature, ct);
        
        if (response.FunctionCalls is null || response.FunctionCalls.Count == 0)
        {
            // No tool calls - we're done
            return new AgentOutput(response.Content, response.TokensUsed, toolCalls);
        }
        
        // Execute each tool call
        foreach (var call in response.FunctionCalls)
        {
            var tool = _tools.First(t => t.ToolId == call.Name);
            
            // Handle confirmation if required
            if (tool.RequiresConfirmation)
            {
                var approved = await RequestToolApproval(tool, call, ct);
                if (!approved)
                {
                    messages.Add(new ChatMessage(Role.Tool, $"Tool {call.Name} was rejected by user"));
                    continue;
                }
            }
            
            var result = await tool.ExecuteAsync(call.ArgumentsJson, ct);
            toolCalls.Add(new ToolCall(call.Name, call.ArgumentsJson, result.ToJson()));
            
            // Add tool result to conversation
            messages.Add(new ChatMessage(Role.Tool, result.ToJson(), call.Name));
        }
    }
    
    throw new AgentException("Max tool iterations exceeded");
}
```

#### 4. Human-in-the-Loop Confirmation

For tools with `RequiresConfirmation = true`:

```csharp
public interface IToolConfirmationService
{
    /// <summary>
    /// Request user approval for a tool execution.
    /// Returns true if approved, false if rejected.
    /// </summary>
    Task<bool> RequestApprovalAsync(
        string toolName,
        string description,
        string argumentsSummary,
        CancellationToken ct = default);
}

// Implementation options:
// 1. WebSocket push to UI for interactive approval
// 2. Store pending approval in DB, poll from UI
// 3. Auto-approve in "autonomous" mode (configurable)
```

### Provider-Specific Implementation

#### Azure OpenAI / OpenAI

Native function calling support via `tools` parameter:

```json
{
  "model": "gpt-4",
  "messages": [...],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "file.write",
        "description": "Write content to a file",
        "parameters": {
          "type": "object",
          "properties": {
            "path": { "type": "string" },
            "content": { "type": "string" },
            "overwrite": { "type": "boolean" }
          },
          "required": ["path", "content"]
        }
      }
    }
  ]
}
```

#### Ollama

Function calling support varies by model:
- `llama3.1`, `mistral-nemo`: Native function calling
- Others: May need prompt-based function calling (less reliable)

```json
{
  "model": "llama3.1",
  "messages": [...],
  "tools": [...]  // Same format as OpenAI
}
```

### Migration Path

1. **Phase 1**: Add `ChatWithFunctionsAsync` to providers (Azure OpenAI first)
2. **Phase 2**: Update `ConfigurableAgent` with tool resolution and execution loop
3. **Phase 3**: Add `IToolConfirmationService` with simple auto-approve mode
4. **Phase 4**: Add UI for tool approval (WebSocket or polling)
5. **Phase 5**: Add Ollama function calling support for compatible models

### Configuration

```json
{
  "Aura": {
    "Agents": {
      "ToolExecution": {
        "Enabled": true,
        "MaxIterations": 10,
        "AutoApproveTools": ["file.read", "graph.query"],
        "RequireApprovalTools": ["file.write", "file.delete", "git.commit"]
      }
    }
  }
}
```

### Testing Strategy

1. **Unit tests**: Tool resolution, function schema generation
2. **Integration tests**: End-to-end tool execution with mock LLM
3. **Provider tests**: Verify function calling works with each provider
4. **Confirmation flow tests**: Approval/rejection scenarios

### Security Considerations

1. **Workspace sandboxing**: Tools should only access files within the workflow's workspace
2. **Tool allowlisting**: Only declared tools are available, not all registered tools
3. **Confirmation for destructive ops**: `file.write`, `file.delete`, `git.*` require approval
4. **Audit logging**: All tool executions logged with arguments and results

### Success Criteria

1. Documentation agent can write README.md to workspace during Finalize step
2. Coding agent can write source files and run tests
3. User can approve/reject file writes from the UI
4. Tool calls are recorded in step output for auditability

## Related Work

- ADR 019: Codebase Context Service (provides context for agents)
- Tool definitions in `Aura.Module.Developer/Tools/`
- Agent definitions in `agents/*.md`

## Open Questions

1. Should tools be scoped per-workflow or global?
2. How to handle tool execution timeout?
3. Should tool results be stored separately from agent output?
4. How to handle partial tool execution (some succeed, some fail)?
