---
description: Onboard the current repository to Aura — register workspace, trigger indexing, and confirm readiness.
---

# Aura Onboard

Register the current repository as an Aura workspace via MCP tools, trigger indexing, and verify readiness for code intelligence.

## Step 1: Check Aura Health

Verify the Aura service is running:

```
curl -s http://localhost:5300/health
```

If it fails, tell the user: "Aura service is not running. Start it or redeploy with `scripts\Deploy-Dev.ps1` from an elevated shell."

## Step 2: Check Existing Workspaces

```
aura_workspace(operation: "list")
```

If the current repo path is already registered, skip to **Step 4** to check index status.

## Step 3: Register Workspace

Register the repository with `aura_workspace add`:

```
aura_workspace(operation: "add", path: "<repo-root-path>", alias: "<short-name>")
```

Use the directory name as the alias (e.g., `aura` for `C:\work\aura`). The response includes the workspace `id` — save it for subsequent steps.

## Step 4: Check Index Status

```
aura_workspace(operation: "status", path: "<repo-root-path>")
```

The response shows:
- `indexed: true/false` — whether indexing has been done
- `chunkCount` — number of indexed chunks

If already indexed with a reasonable chunk count, skip to **Step 6**.

## Step 5: Trigger Indexing

Use `aura_index` to start indexing:

```
aura_index(operation: "index_directory", path: "<repo-root-path>")
```

Then poll for progress:

```
aura_index(operation: "status", jobId: "<job-id>")
```

Report progress to the user:
- **Queued/Processing** → show `processedItems / totalItems` and `progressPercent`
- **Completed** → move to Step 6
- **Failed** → show the error and suggest retrying

Poll every 10–15 seconds until complete. After 20 polls, tell the user indexing is running in the background.

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
  • aura_tree — hierarchical codebase exploration
  • aura_generate — code generation (types, methods, tests)
  • aura_refactor — rename, extract, change signatures
  • aura_validate — compilation and test checks
  • aura_index — re-index folders and files
  • aura_workspace — manage workspace registry
```

## Notes

- If the user provides a specific path, use that instead of the current directory.
- Large repos can take 5–10 minutes to index — this is expected.
- Use `aura_workspace(operation: "set_default", id: "<id>")` to mark as the default workspace.
