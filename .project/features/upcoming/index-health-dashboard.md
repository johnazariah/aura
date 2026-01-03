# Index Health Dashboard

**Status:** 🚧 In Progress  
**Priority:** High  
**Estimated Effort:** Medium (2-3 days)

## Overview

Provide comprehensive visibility into the indexing state of the workspace, enabling users to understand coverage, staleness, and take action to maintain a healthy index.

## Problem Statement

Users currently have no visibility into:

1. **What is indexed** - Which files/directories are covered
2. **Index freshness** - How current is the indexed data
3. **Index quality** - Success/failure rates, parsing errors
4. **Coverage gaps** - Files that should be indexed but aren't

## Proposed Solution

### Index Status Tree View

Add a new tree view in the Aura sidebar showing index health:

```text
📊 INDEX STATUS
├── ✅ Code Graph (1,247 nodes, 3,891 edges)
│   ├── 📁 src/Aura.Foundation (523 nodes)
│   ├── 📁 src/Aura.Module.Developer (412 nodes)
│   └── 📁 src/Aura.Api (312 nodes)
├── ✅ Text Embeddings (423 chunks)
│   ├── 📁 src/ (387 chunks)
│   ├── 📁 docs/ (28 chunks)
│   └── 📁 agents/ (8 chunks)
└── ⚠️ Freshness
    ├── Last indexed: Jan 1, 2026 10:00 AM
    ├── Last commit: Jan 3, 2026 2:30 PM
    └── Status: 5 commits behind
```

### Quick Actions

Context menu and inline buttons:

- **Re-index All** - Full re-index of workspace
- **Re-index Directory** - Re-index selected directory
- **Re-index Stale** - Only index files changed since last index
- **Clear Index** - Remove all indexed data
- **View Details** - Open detailed stats panel

### Detailed Stats Panel

Webview showing comprehensive index information:

| Metric | Value |
|--------|-------|

| **Code Graph** | |
| Total Nodes | 1,247 |
| Total Edges | 3,891 |
| Classes | 234 |
| Interfaces | 89 |
| Methods | 1,456 |
| Properties | 567 |
| **Text Embeddings** | |
| Total Chunks | 423 |
| Total Documents | 87 |
| Avg Chunk Size | 512 tokens |
| **Coverage** | |
| .cs files | 87/87 (100%) |
| .md files | 12/15 (80%) |
| .ts files | 0/23 (0%) |
| **Freshness** | |
| Index Date | Jan 1, 2026 10:00 AM |
| Indexed Commit | abc123 |
| Current Commit | def456 |
| Commits Behind | 5 |
| Files Changed | 12 |

### Freshness Calculation

Use git to determine index freshness:

```typescript
interface IndexFreshness {
  lastIndexedAt: Date;
  indexedCommitSha: string;
  currentCommitSha: string;
  currentCommitAt: Date;
  commitsBehind: number;
  filesChangedSinceIndex: string[];
  status: 'current' | 'stale' | 'very-stale' | 'not-indexed';
}
```

**Status Rules:**

- `current`: indexedCommit === currentCommit OR lastIndexedAt > currentCommitAt
- `stale`: 1-10 commits behind
- `very-stale`: >10 commits behind OR >7 days old
- `not-indexed`: No index data exists

### Coverage Analysis

Track which file types are indexed vs present in workspace:

```typescript
interface CoverageStats {
  byExtension: {
    extension: string;      // ".cs"
    totalFiles: number;     // 87
    indexedFiles: number;   // 87
    coverage: number;       // 1.0
  }[];
  totalCoverage: number;    // 0.85
  unindexedFiles: string[]; // Files not covered by any ingestor
}
```

## Technical Design

### API Changes

#### `GET /api/index/coverage`

```json
{
  "workspacePath": "/path/to/repo",
  "byExtension": [
    { "extension": ".cs", "total": 87, "indexed": 87, "coverage": 1.0 },
    { "extension": ".md", "total": 15, "indexed": 12, "coverage": 0.8 },
    { "extension": ".ts", "total": 23, "indexed": 0, "coverage": 0.0 }
  ],
  "totalFiles": 125,
  "indexedFiles": 99,
  "totalCoverage": 0.792,
  "unindexedExtensions": [".ts", ".json"]
}
```

#### `GET /api/index/freshness`

```json
{
  "workspacePath": "/path/to/repo",
  "lastIndexedAt": "2026-01-01T10:00:00Z",
  "indexedCommitSha": "abc123def",
  "currentBranch": "main",
  "currentCommitSha": "def456abc",
  "currentCommitAt": "2026-01-03T14:30:00Z",
  "commitsBehind": 5,
  "filesChangedSinceIndex": [
    "src/Aura.Api/Program.cs",
    "src/Aura.Foundation/Agents/AgentRegistry.cs"
  ],
  "status": "stale"
}
```

#### `POST /api/index/stale`

Re-index only files changed since last index:

```json
{
  "workspacePath": "/path/to/repo",
  "filesReindexed": 12,
  "duration": "00:00:45"
}
```

### Data Storage

Store index metadata in PostgreSQL:

```sql
CREATE TABLE index_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_path TEXT NOT NULL,
    indexed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    commit_sha TEXT,
    commit_at TIMESTAMPTZ,
    index_type TEXT NOT NULL, -- 'rag' or 'graph'
    stats JSONB
);

CREATE INDEX idx_index_metadata_workspace ON index_metadata(workspace_path, index_type);
```

### Extension Changes

#### New: `IndexStatusTreeProvider`

```typescript
export class IndexStatusTreeProvider implements vscode.TreeDataProvider<IndexStatusItem> {
  private _onDidChangeTreeData = new vscode.EventEmitter<IndexStatusItem | undefined>();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  async getChildren(element?: IndexStatusItem): Promise<IndexStatusItem[]>;
  async refresh(): Promise<void>;
}
```

#### Commands

- `aura.index.refresh` - Refresh status display
- `aura.index.reindexAll` - Full re-index
- `aura.index.reindexStale` - Re-index changed files only
- `aura.index.reindexDirectory` - Re-index selected directory
- `aura.index.clear` - Clear all index data
- `aura.index.showDetails` - Open detailed stats webview

### Automatic Refresh

- Refresh on extension activation
- Refresh when indexing completes (listen to background indexer events)
- Refresh on workspace folder change
- Periodic refresh every 5 minutes (configurable)

## User Experience

### Sidebar View

```text
AURA
├── 💬 Chat
├── 🤖 Agents
├── 📋 Workflows
└── 📊 Index Status        <- New
    ├── Code Graph ✅
    │   └── 1,247 nodes • 3,891 edges
    ├── Text Index ✅
    │   └── 423 chunks • 87 files
    └── Freshness ⚠️
        └── 5 commits behind [Re-index]
```

### Status Bar Item

Optional status bar showing quick health:

```text
📊 Index: ✅ | 🔴 5 behind
```

Clicking opens the detailed stats panel.

## Testing Strategy

### Unit Tests

- Coverage calculation with various file mixes
- Freshness status determination
- Tree item generation

### Integration Tests

- API endpoints return correct data
- Git integration for commit comparison
- Index metadata persistence

### Manual Testing

- Tree view updates after indexing
- Re-index actions work correctly
- Stats panel shows accurate data

## Dependencies

- `GitService` for commit info and file diffs
- `IRagService` for embedding stats
- `ICodeGraphService` for graph stats
- Background indexer for re-index operations
- `IIngestorRegistry` for supported extensions

## Future Enhancements

- Auto-index on git pull/commit hooks
- File-level index status in explorer decorations
- Index progress indicator during re-index
- Historical index stats (trends over time)
- Exclude patterns configuration UI

## Acceptance Criteria

- [ ] Index Status tree view appears in sidebar
- [ ] Shows code graph node/edge counts
- [ ] Shows text embedding chunk/file counts
- [ ] Shows freshness with commit comparison
- [ ] "Re-index All" command works
- [ ] "Re-index Stale" only processes changed files
- [ ] Stats panel shows detailed breakdown
- [ ] Coverage shows by file extension
- [ ] Status bar item shows quick health (optional)
- [ ] Auto-refresh after indexing completes
