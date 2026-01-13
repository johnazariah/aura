# Structured Output Mode

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Last Updated:** 2026-01-13

## Overview

Leverage LLM providers' native JSON mode and schema enforcement to guarantee valid structured output, reducing parsing failures and improving reliability.

## Implementation Summary

### Phase 1 - Provider Support
- Added `JsonSchema` and `ChatOptions` records to define response schemas
- Implemented schema support in `AzureOpenAiProvider` using `ChatResponseFormat.CreateJsonSchemaFormat`
- Implemented schema support in `OpenAiProvider` using the same approach
- Added Ollama fallback with `format: "json"` + schema injection into system prompt
- Created `WellKnownSchemas` static class with ReActResponse, WorkflowPlan, and CodeModification schemas

### Phase 2 - ReActExecutor Integration
- Added `UseStructuredOutput` option to `ReActOptions`
- Updated `ReActExecutor.ExecuteAsync` to use `ChatAsync` with `WellKnownSchemas.ReActResponse` when enabled
- Added `ParseStructuredResponse` method to parse JSON from structured output
- Updated `BuildSystemPrompt` to use simpler prompt when schema enforces format
- Chat messages are properly accumulated for multi-turn conversations in structured mode

### Phase 3 - Workflow Planning Integration
- Updated `WellKnownSchemas.WorkflowPlan` to match `StepDefinition` structure (name, capability, language, description)
- Modified `WorkflowService.PlanAsync` to use structured output when provider supports it
- Added `CanUseStructuredOutput` helper to detect Azure OpenAI and OpenAI providers
- Added `ParseStepsFromStructuredResponse` for reliable JSON parsing from structured output
- Fallback to text-based parsing for Ollama and other providers

## Problem Statement

Previous approach relied on prompt engineering to get JSON output:

```
You must respond with valid JSON in this format:
{"action": "...", "parameters": {...}}
```

This failed when:
- LLM adds markdown code fences around JSON
- LLM includes explanatory text before/after JSON
- LLM produces syntactically invalid JSON
- LLM hallucinates fields not in the schema

We mitigated this with `ExtractFinalAnswer` and regex cleanup, but this was fragile.

## Goals

1. ✅ Use provider-native JSON mode where available
2. ✅ Define response schemas declaratively
3. ✅ Fall back gracefully on providers without native support
4. ✅ Reduce JSON parsing failures to near-zero

## Provider Capabilities

| Provider | JSON Mode | Schema Enforcement | How |
|----------|-----------|-------------------|-----|
| OpenAI | ✅ | ✅ Full | `response_format: { type: "json_schema", json_schema: {...} }` |
| Azure OpenAI | ✅ | ✅ Full | Same as OpenAI (GPT-4o, GPT-4 Turbo) |
| Ollama | ⚠️ Partial | ❌ None | `format: "json"` (no schema enforcement) |

## Key Files

- `src/Aura.Foundation/Llm/JsonSchema.cs` - Schema definition record
- `src/Aura.Foundation/Llm/ChatOptions.cs` - Chat options with schema support
- `src/Aura.Foundation/Llm/WellKnownSchemas.cs` - Predefined schemas (ReActResponse, WorkflowPlan, CodeModification)
- `src/Aura.Foundation/Llm/AzureOpenAiProvider.cs` - Azure OpenAI schema implementation
- `src/Aura.Foundation/Llm/OpenAiProvider.cs` - OpenAI schema implementation
- `src/Aura.Foundation/Llm/OllamaProvider.cs` - Ollama fallback with JSON mode
- `src/Aura.Foundation/Agents/ReActExecutor.cs` - ReAct structured output integration
- `src/Aura.Module.Developer/Services/WorkflowService.cs` - Workflow planning structured output

## Usage

### ReAct Executor with Structured Output

```csharp
var options = new ReActOptions
{
    UseStructuredOutput = true,  // Enable schema enforcement
    MaxSteps = 10,
    // ...
};

var result = await executor.ExecuteAsync(messages, tools, options, ct);
```

### Workflow Planning (Automatic)

When using Azure OpenAI or OpenAI providers, `WorkflowService.PlanAsync` automatically uses structured output for reliable step parsing. Ollama falls back to text-based parsing.

### Common Schemas

```csharp
// ReAct agent responses
WellKnownSchemas.ReActResponse

// Workflow step planning
WellKnownSchemas.WorkflowPlan

// Code modifications (future use)
WellKnownSchemas.CodeModification
```

## References

- [OpenAI Structured Outputs](https://platform.openai.com/docs/guides/structured-outputs)
- [Azure OpenAI JSON Mode](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/json-mode)
