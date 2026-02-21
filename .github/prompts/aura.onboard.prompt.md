---
description: Onboard the current repository to Aura — register workspace, trigger RAG indexing, and confirm readiness.
---

# Aura Onboard

Register the current repository as an Aura workspace, trigger code indexing, and verify everything is ready for code intelligence.

## Step 1: Check Aura Health

Verify the Aura service is running:

```bash
curl -s http://localhost:5300/health
```

If it fails, tell the user: "Aura service is not running. Start it or run `Update-LocalInstall.ps1` as Administrator."

## Step 2: Check Existing Workspaces

```
aura_workspace(operation: "list")
```

If the current repo path is already registered, skip to **Step 4** to check index status.

## Step 3: Register Workspace

Ask the user for an optional short alias (e.g., "aura", "my-app"), then register:

```
aura_workspace(operation: "add", path: "<repo-root-path>", alias: "<alias>")
```

The response includes the workspace `id` — save it for the next steps.

## Step 4: Check Index Status

```
aura_workspace(operation: "status", path: "<repo-root-path>")
```

The response shows:
- `indexed: true/false` — whether RAG indexing has been done
- `chunkCount` — number of indexed code chunks

If already indexed with a reasonable chunk count, skip to **Step 6**.

## Step 5: Trigger Indexing

Use the workspace ID from registration (or from the list):

```bash
curl -s -X POST http://localhost:5300/api/workspaces/<workspace-id>/index
```

Then poll for progress:

```bash
curl -s http://localhost:5300/api/workspaces/<workspace-id>/index
```

Report progress to the user:
- **Queued/Processing** → show `processedItems / totalItems` and `progressPercent`
- **Completed** → move to Step 6
- **Failed** → show the error and suggest retrying

Poll every 10-15 seconds until complete. Don't poll more than 20 times — if still running, tell the user it's indexing in the background and they can check with `/aura-onboard` again later.

## Step 6: Confirm Ready

Once indexing is complete, summarize:

```
✅ Workspace registered: <alias or path>
✅ RAG index: <chunk-count> chunks across <file-count> files
✅ Code intelligence ready

You can now use:
  • aura_search — semantic code search
  • aura_navigate — find callers, implementations, references
  • aura_inspect — explore type structures
  • /aura-create-story — start working on features
```

## Notes

- If the user provides a path that isn't the current directory, use that path instead.
- If indexing takes more than a few minutes, it's fine — large repos can take 5-10 minutes. Let the user know.
- The `set_default` operation can mark this as the default workspace: `aura_workspace(operation: "set_default", id: "<id>")`
