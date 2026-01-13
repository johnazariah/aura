# Streaming Responses

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Last Updated:** 2026-01-13

## Overview

Add token-by-token streaming for chat responses, providing immediate feedback and better UX for longer generations.

## Implementation Summary

### Phase 1 - Provider Interface
- Added `StreamChatAsync` to `ILlmProvider` interface
- Created `LlmToken` record for streaming tokens
- Added `SupportsStreaming` property to providers

### Phase 2 - Ollama Provider
- Implemented native NDJSON streaming using `/api/chat` with `stream: true`
- Parses streaming JSON objects and yields tokens

### Phase 3 - Cloud Providers  
- Implemented streaming for Azure OpenAI using SDK's `GetChatCompletionsStreamingAsync`
- Implemented streaming for OpenAI using same approach
- Both providers yield tokens with content and finish reason

### Phase 4 - API Endpoint
- Added `/api/agents/{agentId}/chat/stream` SSE endpoint
- Returns Server-Sent Events with `token`, `done`, and `error` event types
- Proper SSE format with `event:` and `data:` lines

### Phase 5 - Extension Integration
- Updated `ChatPanelProvider` to consume SSE stream
- Uses EventSource-like parsing for SSE events
- Renders tokens incrementally in the chat panel
- Added streaming to workflow step execution panel

## Problem Statement

Previously, chat responses blocked until the entire LLM generation completed. For complex queries or detailed explanations, users waited 10-30 seconds with no feedback, making the system feel unresponsive.

## API

### Streaming Chat Endpoint

```
POST /api/agents/{agentId}/chat/stream
```

Request:
```json
{
  "prompt": "Explain async/await",
  "conversationHistory": [],
  "additionalContext": ""
}
```

Response (Server-Sent Events):
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

## Key Files

- `src/Aura.Foundation/Llm/ILlmProvider.cs` - StreamChatAsync interface
- `src/Aura.Foundation/Llm/OllamaProvider.cs` - NDJSON streaming implementation
- `src/Aura.Foundation/Llm/AzureOpenAiProvider.cs` - SDK streaming
- `src/Aura.Foundation/Llm/OpenAiProvider.cs` - SDK streaming
- `src/Aura.Api/Program.cs` - SSE streaming endpoint
- `extension/src/providers/chatPanelProvider.ts` - SSE consumption
- `extension/src/providers/workflowPanelProvider.ts` - Workflow step streaming

## Provider Support

| Provider | Streaming Support | Implementation |
|----------|-------------------|----------------|
| Ollama | ✅ Native | NDJSON with `stream: true` |
| Azure OpenAI | ✅ Native | SDK streaming API |
| OpenAI | ✅ Native | SDK streaming API |

## Non-Goals

- Streaming for tool execution (tools need complete responses to parse)
- Streaming for workflow steps (step completion is atomic)
- WebSocket support (SSE is sufficient and simpler)

## Success Metrics

- ✅ First token appears within 500ms of request
- ✅ Tokens render smoothly in chat panel
- ✅ Cancellation (user closes panel) stops generation
- ✅ Error mid-stream shows graceful error message
