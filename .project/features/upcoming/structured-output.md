# Structured Output Mode

**Status:** 📋 Planned  
**Priority:** High  
**Estimated Effort:** Low (1-2 days)

## Overview

Leverage LLM providers' native JSON mode and schema enforcement to guarantee valid structured output, reducing parsing failures and improving reliability.

## Problem Statement

Current approach relies on prompt engineering to get JSON output:

```
You must respond with valid JSON in this format:
{"action": "...", "parameters": {...}}
```

This fails when:
- LLM adds markdown code fences around JSON
- LLM includes explanatory text before/after JSON
- LLM produces syntactically invalid JSON
- LLM hallucinates fields not in the schema

We currently mitigate this with `ExtractFinalAnswer` and regex cleanup, but this is fragile.

## Goals

1. Use provider-native JSON mode where available
2. Define response schemas declaratively
3. Fall back gracefully on providers without native support
4. Reduce JSON parsing failures to near-zero

## Provider Capabilities

| Provider | JSON Mode | Schema Enforcement | How |
|----------|-----------|-------------------|-----|
| OpenAI | ✅ | ✅ Full | `response_format: { type: "json_schema", json_schema: {...} }` |
| Azure OpenAI | ✅ | ✅ Full | Same as OpenAI (GPT-4o, GPT-4 Turbo) |
| Ollama | ⚠️ Partial | ❌ None | `format: "json"` (no schema enforcement) |

## Design

### Schema Definition

Add schema support to `LlmRequest`:

```csharp
public record LlmRequest
{
    // Existing properties...
    
    /// <summary>
    /// When set, request structured JSON output matching this schema.
    /// Provider will use native JSON mode if available, otherwise falls back to prompt-based.
    /// </summary>
    public JsonSchema? ResponseSchema { get; init; }
}

public record JsonSchema(
    string Name,
    string? Description,
    JsonElement Schema,  // JSON Schema object
    bool Strict = true   // For OpenAI strict mode
);
```

### Usage in ReAct Executor

Define the ReAct response schema:

```csharp
private static readonly JsonSchema ReActSchema = new(
    Name: "react_response",
    Description: "ReAct agent response with thought, action, and parameters",
    Schema: JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "thought": { "type": "string" },
            "action": { "type": "string" },
            "action_input": { "type": "object" }
        },
        "required": ["thought", "action"],
        "additionalProperties": false
    }
    """).RootElement,
    Strict: true
);
```

Then use it in requests:

```csharp
var request = new LlmRequest
{
    Messages = messages,
    ResponseSchema = ReActSchema
};
```

### Provider Implementation

Each provider handles the schema appropriately:

**OpenAI/Azure OpenAI:**
```csharp
if (request.ResponseSchema is not null)
{
    options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
        request.ResponseSchema.Name,
        BinaryData.FromString(request.ResponseSchema.Schema.GetRawText()),
        strictSchemaEnabled: request.ResponseSchema.Strict
    );
}
```

**Ollama:**
```csharp
if (request.ResponseSchema is not null)
{
    // Ollama only supports basic JSON mode, no schema
    requestBody["format"] = "json";
    // Schema enforcement via prompt (fallback)
    systemPrompt += $"\n\nRespond with JSON matching: {request.ResponseSchema.Schema}";
}
```

### Common Schemas

Create a `WellKnownSchemas` static class:

```csharp
public static class WellKnownSchemas
{
    public static readonly JsonSchema ReActResponse = ...;
    public static readonly JsonSchema WorkflowPlan = ...;
    public static readonly JsonSchema CodeModification = ...;
}
```

## Implementation Plan

1. Add `ResponseSchema` to `LlmRequest`
2. Implement in Azure OpenAI provider (most used for production)
3. Implement in OpenAI provider
4. Add Ollama fallback (JSON mode + prompt)
5. Update ReActExecutor to use schema
6. Update workflow planning to use schema

## Testing

- Unit test: Schema serialization
- Integration test: OpenAI returns valid schema-conforming JSON
- Integration test: Ollama fallback produces parseable JSON
- Regression test: Existing prompts continue working

## Success Criteria

- [ ] Zero JSON parse failures on OpenAI/Azure with schema
- [ ] ReAct executor uses structured output
- [ ] Workflow planning uses structured output
- [ ] Graceful fallback on Ollama (no regression)

## References

- [OpenAI Structured Outputs](https://platform.openai.com/docs/guides/structured-outputs)
- [Azure OpenAI JSON Mode](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/json-mode)
