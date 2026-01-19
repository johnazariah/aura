# Agent Reflection

**Status:** ✅ Complete  
**Completed:** 2025-01-19  
**Priority:** Medium  
**Estimated Effort:** Low (1 day)

## Overview

Add a self-critique step where agents review their own output before returning it, catching errors and improving response quality.

## Problem Statement

Agents sometimes produce responses with:

- Incomplete implementations (missing edge cases)
- Incorrect assumptions about the codebase
- Formatting issues (wrong indentation, missing imports)
- Hallucinated file paths or symbol names

A reflection step allows the agent to catch these issues before the user sees them.

## Goals

1. Improve response quality through self-review
2. Catch common errors (hallucinations, incomplete code)
3. Minimal latency impact (single additional LLM call)
4. Optional per-agent configuration

## Design

### Reflection Prompt

After the agent generates its response, a reflection prompt reviews it:

```handlebars
You are reviewing your own response before sending it to the user.

## Original Task
{{task}}

## Your Response
{{response}}

## Review Checklist
- Does the response fully address the task?
- Are there any factual errors or hallucinations?
- Is code syntactically correct and properly formatted?
- Are file paths and symbol names accurate?
- Is anything missing or incomplete?

## Instructions
If the response is good, output: APPROVED
If changes are needed, output the corrected response.
```

### Agent Configuration

Add `reflection` option to agent markdown:

```yaml
---
name: coding-agent
capabilities:
  - coding
reflection: true  # Enable self-review
reflectionPrompt: coding-reflection  # Optional custom prompt
---
```

### Implementation

In `AgentExecutor` (or specialized executors):

```csharp
public async Task<AgentResponse> ExecuteAsync(AgentRequest request)
{
    // Generate initial response
    var response = await GenerateResponseAsync(request);
    
    // Apply reflection if enabled
    if (_agent.Reflection)
    {
        response = await ReflectAsync(request.Task, response);
    }
    
    return response;
}

private async Task<AgentResponse> ReflectAsync(string task, AgentResponse response)
{
    var prompt = await _promptRegistry.RenderAsync(
        _agent.ReflectionPrompt ?? "agent-reflection",
        new { task, response = response.Content }
    );
    
    var result = await _llm.CompleteAsync(new LlmRequest { Messages = [prompt] });
    
    // If approved, return original; otherwise return corrected
    if (result.Content.Trim().Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
    {
        return response;
    }
    
    return response with { Content = result.Content, WasReflected = true };
}
```

### ReAct Executor Integration

For tool-using agents, reflection happens on the final answer:

```csharp
// In ReActExecutor, after agent outputs "finish" action
if (_agent.Reflection && action == "finish")
{
    var reflectedAnswer = await ReflectOnFinalAnswerAsync(task, answer);
    return reflectedAnswer;
}
```

### Prompt Template

Create `prompts/agent-reflection.prompt`:

```handlebars
---
description: Self-review prompt for agent responses
---
You are critically reviewing a response before it's sent to the user.

## Original Request
{{task}}

## Generated Response
{{response}}

## Review Questions
1. Does this fully address what was asked?
2. Is all information accurate and verifiable?
3. Is code syntactically correct with proper formatting?
4. Are there any missing pieces or incomplete sections?

If the response is complete and correct, respond with exactly: APPROVED

If there are issues, provide the corrected response without any preamble.
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `reflection` | bool | false | Enable reflection for this agent |
| `reflectionPrompt` | string | "agent-reflection" | Custom reflection prompt |
| `reflectionModel` | string | (same as agent) | Use different model for reflection |

### Using Smaller Model for Reflection

For cost optimization, use a smaller/faster model for reflection:

```yaml
---
name: coding-agent
model: gpt-4o
reflection: true
reflectionModel: gpt-4o-mini  # Cheaper model for review
---
```

## Non-Goals

- Multi-round reflection (diminishing returns)
- Reflection for every chat message (too slow)
- Reflection for tool calls (only final output)

## Implementation Plan

1. Add `Reflection` and `ReflectionPrompt` to agent schema
2. Create `agent-reflection.prompt` template
3. Add reflection step to `AgentExecutor`
4. Add reflection step to `ReActExecutor` final answer
5. Enable for `coding-agent` by default

## Testing

- Unit test: Reflection returns APPROVED for good response
- Unit test: Reflection corrects obvious error
- Integration test: End-to-end with reflection enabled

## Success Criteria

- [ ] Agents catch hallucinated file paths
- [ ] Agents fix obvious code syntax errors
- [ ] Latency increase < 2 seconds average
- [ ] User can disable reflection if desired

## Future Enhancements

- **Confidence scoring**: Agent rates confidence 1-10
- **Selective reflection**: Only reflect on low-confidence answers
- **Multi-perspective**: Use different "reviewer personas"
