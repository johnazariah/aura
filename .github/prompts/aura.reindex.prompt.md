---
description: Rebuild the RAG index for the current workspace — useful after large changes, branch switches, or stale indexes.
---

# Aura Reindex

Trigger a full RAG reindex for the current workspace and monitor progress.

## Step 1: Identify Workspace

```
aura_workspace(operation: "list")
```

Find the workspace matching the current repo path. If not registered, tell the user to run `/aura-onboard` first.

Save the workspace `id` for the next steps.

## Step 2: Check Current Index State

```bash
curl -s http://localhost:5300/api/workspaces/<workspace-id>/index
```

Show the user the current state:
- **Status**: fresh / stale / not-indexed
- **RAG chunks**: count
- **Last indexed**: timestamp
- **Commits behind**: how stale it is

If an indexing job is already running, show its progress instead of starting a new one.

## Step 3: Trigger Reindex

```bash
curl -s -X POST http://localhost:5300/api/workspaces/<workspace-id>/index
```

If `isNewJob: false`, indexing was already in progress — just monitor it.

## Step 4: Monitor Progress

Poll the index status endpoint every 10-15 seconds:

```bash
curl -s http://localhost:5300/api/workspaces/<workspace-id>/index
```

Show a progress update each time:
- `processedItems / totalItems` files
- `progressPercent`%
- Elapsed time

Stop polling after completion or 20 attempts. If still running, tell the user:
"Indexing is running in the background. Check again with `/aura-reindex` or `aura_workspace(operation: \"status\", path: \"...\")` later."

## Step 5: Confirm Complete

```
✅ Reindex complete
   Files: <file-count>
   Chunks: <chunk-count>
   Duration: ~<elapsed>
   Code intelligence is up to date.
```

## Options

If the user says "clear" or "fresh", clear the index first before reindexing:

```bash
curl -s -X DELETE http://localhost:5300/api/workspaces/<workspace-id>/index
curl -s -X POST http://localhost:5300/api/workspaces/<workspace-id>/index
```
