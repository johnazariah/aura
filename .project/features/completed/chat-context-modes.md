# Chat Context Modes

**Status:** ✅ Complete  
**Completed:** 2026-01-13  
**Priority:** High

## Overview

Context mode selector in the chat panel, giving users control over how queries are enriched with codebase knowledge, plus real-time index health indicators.

## Implementation Summary

### Context Modes

| Mode | Description | Behavior |
|------|-------------|----------|
| **No context** | Pure agent chat | Skips RAG and graph enrichment |
| **Text RAG** | Semantic search only | Uses text embeddings for context |
| **Graph RAG** | Code structure only | Uses code graph relationships |
| **Full context** | Both text + graph | Maximum context richness (default) |

### Index Health Indicator

Displays real-time workspace index status:
- 🟢 **Indexed** - Up to date with latest commit
- 🟡 **N behind** - Index is behind by N commits
- ⚪ **Not indexed** - Workspace needs indexing

### Key Features

1. **Context Mode Selector** - Dropdown in chat header
2. **Health Indicator** - Colored dot with status text
3. **Mode-Aware Queries** - `useRag` and `useCodeGraph` passed to API
4. **Streaming Support** - Works with streaming responses
5. **State Persistence** - Mode synced with conversation state

## API Endpoints

### GET /api/index/health

Returns comprehensive index health for a workspace:

```json
{
  "workspacePath": "/path/to/repo",
  "rag": {
    "totalChunks": 423,
    "totalDocuments": 87,
    "lastIndexedAt": "2026-01-01T10:00:00Z",
    "commitsBehind": 0
  },
  "graph": {
    "totalNodes": 1247,
    "totalEdges": 3891,
    "lastIndexedAt": "2026-01-01T10:00:00Z"
  },
  "overallStatus": "fresh"
}
```

## Key Files

| File | Purpose |
|------|---------|
| [extension/src/providers/chatPanelProvider.ts](../../../extension/src/providers/chatPanelProvider.ts) | Chat panel with mode selector and health indicator |
| [extension/src/providers/chatWindowProvider.ts](../../../extension/src/providers/chatWindowProvider.ts) | Agent chat window with same features |
| [extension/src/services/auraApiService.ts](../../../extension/src/services/auraApiService.ts) | `getIndexHealth()`, `executeAgentWithRag()` |
| [src/Aura.Api/Program.cs](../../../src/Aura.Api/Program.cs) | `/api/index/health` endpoint |

## User Interface

```text
┌─────────────────────────────────────────────────┐
│ Chat Agent    ● Indexed  [Full context ▼] [+]   │
├─────────────────────────────────────────────────┤
│                                                 │
│  [User]: How does the WorkflowService handle   │
│          step execution?                        │
│                                                 │
│  [Assistant]: Based on the codebase...         │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Acceptance Criteria

- [x] Context mode selector visible in chat panel
- [x] Mode selection persists across sessions
- [x] Index health shows chunk and node counts
- [x] Freshness indicator uses git commit comparison
- [x] Stale index shows "X commits behind"
- [x] "No Context" mode skips all enrichment
- [x] Sources shown in responses when context used

## Implementation Details

### ChatPanelProvider

```typescript
export type ContextMode = 'none' | 'text' | 'graph' | 'full';

private _contextMode: ContextMode = 'full';

// Mode affects query behavior
const useRag = this._contextMode === 'text' || this._contextMode === 'full';
const useGraph = this._contextMode === 'graph' || this._contextMode === 'full';
```

### Health Status CSS Classes

```css
.health-indicator.fresh .health-dot { background: #3fb950; }
.health-indicator.stale .health-dot { background: #d29922; }
.health-indicator.not-indexed .health-dot { background: #8b949e; }
```

## Related

- [Streaming Responses](./streaming-responses.md)
- [Code-Aware Chat](./code-aware-chat.md)
- [Index Health Dashboard](./index-health-dashboard.md)
