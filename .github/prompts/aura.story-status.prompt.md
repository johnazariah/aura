---
description: Show detailed status for a specific Aura story including step progress, timing, and next actions.
---

# Story Status

Show detailed status for a specific story or the current worktree's story.

## Find the Story

If the user provides a story ID:
```
aura_workflow(operation: "get", storyId: "<id>")
```

If no ID provided, detect from current workspace:
```
aura_workflow(operation: "get_by_path", workspacePath: "<cwd>")
```

If neither works, fall back to `aura_workflow(operation: "list")` and let the user pick.

## Display Format

Show a clear status report:

```
📋 Story: Add retry logic to HTTP client
🔖 Status: Executing (Wave 2/3)
🌿 Branch: workflow/add-retry-logic-to-http-client
📂 Worktree: C:\work\aura-workflow-add-retry-logic-...
⏱️  Duration: 25 minutes
🔗 Issue: https://github.com/owner/repo/issues/42

Steps:
  ✅ 1. Implement RetryPolicy class (completed, 5m)
  ✅ 2. Add unit tests (completed, 8m)
  🔄 3. Wire into HttpClient (running)
  ⏳ 4. Integration tests (pending)
  ⏳ 5. Update documentation (pending)

Progress: 2/5 steps done
```

## Suggest Next Action

Based on status:
- **Has pending steps** → "Run `/aura-run-story` to continue"
- **All steps done** → "Run `/aura-run-story` to finalize and create PR"
- **Has failed steps** → "Step N failed: {error}. Resume with `/aura-run-story`"
- **Completed with PR** → "PR created: {url}"
