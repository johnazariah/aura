# Streaming Responses

**Status:** 🔄 In Progress (Phase 1-5 Complete)
**Priority:** High  
**Estimated Effort:** Medium (2-3 days)

## Implementation Progress

### ✅ Completed
- **Phase 1**: Added `StreamChatAsync` to `ILlmProvider` interface with `LlmToken` record
- **Phase 2**: Implemented streaming for Ollama provider (uses native NDJSON streaming)
- **Phase 3**: Implemented streaming for Azure OpenAI and OpenAI providers (uses SDK streaming)
- **Phase 4**: Added `/api/agents/{agentId}/chat/stream` SSE endpoint
- **Phase 5**: Updated extension to consume SSE stream in chat panel

### ⏳ Remaining
- Add integration tests for streaming endpoint
- Test end-to-end with live LLM providers

## Overview

Add token-by-token streaming for chat responses, providing immediate feedback and better UX for longer generations.

## Problem Statement

Currently, chat responses block until the entire LLM generation completes. For complex queries or detailed explanations, users wait 10-30 seconds with no feedback, making the system feel unresponsive.

## Goals

1. Stream chat responses token-by-token to the VS Code extension
2. Maintain compatibility with non-streaming scenarios (tool execution, workflows)
3. Support all LLM providers (Ollama, Azure OpenAI, OpenAI)

## Design

### API Changes

New streaming endpoint alongside existing chat:

```
POST /api/agents/{agentId}/chat          # Existing - returns complete response
POST /api/agents/{agentId}/chat/stream   # New - returns SSE stream
```

Response format (Server-Sent Events):

```
event: token
data: {"content": "Hello"}

event: token
data: {"content": " world"}

event: done
data: {"totalTokens": 150, "finishReason": "stop"}

event: error
data: {"message": "Provider error", "code": "llm_error"}
```

### Provider Interface

Extend `ILlmProvider` with streaming support:

```csharp
public interface ILlmProvider
{
    // Existing
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
    
    // New
    IAsyncEnumerable<LlmToken> StreamAsync(LlmRequest request, CancellationToken ct);
    bool SupportsStreaming { get; }
}

public record LlmToken(string Content, bool IsComplete, string? FinishReason = null);
```

### Provider Implementation

| Provider | Streaming Support | Notes |
|----------|-------------------|-------|
| Ollama | ✅ Native | Uses `/api/generate` with `stream: true` |
| Azure OpenAI | ✅ Native | Uses `GetChatCompletionsStreamingAsync` |
| OpenAI | ✅ Native | Uses `CreateChatCompletionAsync` with streaming |

### Extension Changes

Update `ChatPanelProvider` to handle SSE:

```typescript
async function streamChat(agentId: string, message: string) {
    const response = await fetch(`${baseUrl}/api/agents/${agentId}/chat/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message })
    });
    
    const reader = response.body?.getReader();
    const decoder = new TextDecoder();
    
    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        const chunk = decoder.decode(value);
        // Parse SSE events and update UI
        appendToResponse(parseSSE(chunk));
    }
}
```

## Non-Goals

- Streaming for tool execution (tools need complete responses to parse)
- Streaming for workflow steps (step completion is atomic)
- WebSocket support (SSE is sufficient and simpler)

## Implementation Plan

1. **Phase 1**: Add `StreamAsync` to `ILlmProvider` interface
2. **Phase 2**: Implement for Ollama provider
3. **Phase 3**: Implement for Azure/OpenAI providers
4. **Phase 4**: Add `/chat/stream` API endpoint
5. **Phase 5**: Update extension to consume SSE stream

## Testing

- Unit test: Mock provider streaming
- Integration test: End-to-end stream with Ollama
- Manual test: Verify smooth token rendering in VS Code

## Success Criteria

- [ ] First token appears within 500ms of request
- [ ] Tokens render smoothly without flickering
- [ ] Cancellation (user closes panel) stops generation
- [ ] Error mid-stream shows graceful error message
