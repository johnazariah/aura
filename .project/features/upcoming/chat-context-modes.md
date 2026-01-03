# Chat Context Modes

**Status:** 🚧 In Progress  
**Priority:** High  
**Estimated Effort:** Medium (3-5 days)

## Overview

Enhance the chat panel with explicit context mode selection, giving users control over how their queries are enriched with codebase knowledge. Add real-time index health indicators so users understand the quality and freshness of the context.

## Problem Statement

Currently, the chat panel has a simple RAG toggle but users lack:

1. **Visibility** into what context is being used (text embeddings vs code graph)
2. **Control** over performance/richness tradeoff
3. **Awareness** of index staleness - queries may use outdated information

## Proposed Solution

### Context Mode Selector

Add a dropdown/segmented control to the chat panel header with modes:

| Mode | Description | Performance |
|------|-------------|-------------|

| **No Context** | Pure agent chat, no codebase enrichment | Fastest |
| **Text Search** | RAG text embeddings only | Fast |
| **Code Graph** | Structural queries (types, methods, relationships) | Medium |
| **Full Context** | Both text embeddings + code graph | Slowest, richest |

### Index Health Indicators

Display real-time index status below the mode selector:

```text
📊 423 chunks • 1,247 nodes • ✅ Up to date with main
```

Or when stale:

```text
📊 423 chunks • 1,247 nodes • 🔴 5 commits behind (3 days ago)
```

#### Freshness Calculation

Compare:

- `lastIndexedAt` - timestamp of most recent indexing operation
- `lastCommitAt` - timestamp of HEAD commit in the repository

| Condition | Status |
|-----------|--------|

| `lastIndexedAt > lastCommitAt` | ✅ Current |
| `lastCommitAt - lastIndexedAt < 24h` | ⚠️ Possibly stale |
| `lastCommitAt - lastIndexedAt >= 24h` | 🔴 Stale |
| No index exists | ⭕ Not indexed |

Additionally, show commit count behind:

- Use `git rev-list --count <indexed-commit>..HEAD` to count commits since index

### Quick Actions

- **"Re-index"** button when stale
- **"Index Workspace"** when not indexed
- Clicking the status opens detailed index health panel

## Technical Design

### API Changes

#### New Endpoint: `GET /api/index/health`

Returns comprehensive index health for a workspace:

```json
{
  "workspacePath": "/path/to/repo",
  "rag": {
    "totalChunks": 423,
    "totalDocuments": 87,
    "lastIndexedAt": "2026-01-01T10:00:00Z"
  },
  "codeGraph": {
    "totalNodes": 1247,
    "totalEdges": 3891,
    "lastIndexedAt": "2026-01-01T10:00:00Z"
  },
  "git": {
    "currentBranch": "main",
    "headCommitSha": "abc123",
    "headCommitAt": "2026-01-03T14:30:00Z",
    "commitsBehind": 5,
    "indexedCommitSha": "def456"
  },
  "status": "stale",
  "statusMessage": "5 commits behind main (3 days ago)"
}
```

#### Extension Service Changes

Add to `AuraApiService`:

```typescript
async getIndexHealth(workspacePath: string): Promise<IndexHealthResponse>;
```

### Extension UI Changes

#### ChatPanelProvider Updates

1. Add context mode state:

   ```typescript
   type ContextMode = 'none' | 'text' | 'graph' | 'full';
   private _contextMode: ContextMode = 'full';
   ```

2. Add mode selector to webview HTML
3. Add index health display with refresh on focus
4. Pass context mode to `executeAgentWithRag` calls

#### New Message Types

```typescript
// Webview -> Extension
{ type: 'setContextMode', mode: ContextMode }
{ type: 'refreshIndexHealth' }
{ type: 'triggerReindex' }

// Extension -> Webview  
{ type: 'indexHealthUpdate', health: IndexHealthResponse }
{ type: 'reindexStarted' }
{ type: 'reindexComplete', success: boolean }
```

### Backend Changes

1. **Add `GET /api/index/health` endpoint** in `Program.cs`
2. **Extend `IRagService`** with `GetLastIndexedCommitAsync()`
3. **Store indexed commit SHA** when indexing (new column or metadata)
4. **Use `GitService`** to get current HEAD and count commits

## User Experience

### Initial State (Not Indexed)

```text
┌─────────────────────────────────────────┐
│ Context: [Full Context ▼]               │
│ ⭕ Workspace not indexed                │
│ [Index Workspace]                       │
├─────────────────────────────────────────┤
│                                         │
│   Index your workspace to enable        │
│   code-aware chat responses.            │
│                                         │
└─────────────────────────────────────────┘
```

### Healthy State

```text
┌─────────────────────────────────────────────────┐
│ Context: [Full Context ▼]                       │
│ 📊 423 chunks • 1,247 nodes • ✅ Up to date     │
├─────────────────────────────────────────────────┤
│ [User]: How does the WorkflowService handle    │
│         step execution?                         │
│                                                 │
│ [Assistant]: Based on the codebase...          │
│                                                 │
│ Sources:                                        │
│ • WorkflowService.cs:245-312                   │
│ • IStepExecutor.cs:15-42                       │
└─────────────────────────────────────────────────┘
```

### Stale State

```text
┌──────────────────────────────────────────────────┐
│ Context: [Full Context ▼]                        │
│ 📊 423 chunks • 1,247 nodes                      │
│ 🔴 5 commits behind main (3 days) [Re-index]     │
├──────────────────────────────────────────────────┤
```

## Testing Strategy

### Unit Tests

- Context mode state management
- Health status calculation logic
- Commit counting edge cases

### Integration Tests

- `/api/index/health` endpoint returns correct structure
- Health updates when index changes
- Git integration works in repo and non-repo directories

### Manual Testing

- Mode switching updates query behavior
- Re-index button triggers background index
- Status updates after indexing completes

## Dependencies

- Existing `GitService` for commit info
- Existing `IRagService` for chunk stats
- Existing `ICodeGraphService` for node/edge stats
- Background indexer for re-index action

## Future Enhancements

- Show index coverage by directory (tree view)
- "Auto-index on commit" option
- Index diff view (what changed since last index)
- Per-file staleness indicators in explorer

## Acceptance Criteria

- [ ] Context mode selector visible in chat panel
- [ ] Mode selection persists across sessions
- [ ] Index health shows chunk and node counts
- [ ] Freshness indicator uses git commit comparison
- [ ] Stale index shows "X commits behind"
- [ ] Re-index button triggers background indexing
- [ ] Status updates after indexing completes
- [ ] "No Context" mode skips all enrichment
- [ ] Sources shown in responses when context used
