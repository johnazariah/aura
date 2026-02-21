---
description: List all active Aura stories with progress, status, and suggested actions.
---

# Aura Stories

Show a dashboard of all active Aura stories.

## What to Do

1. Call `aura_workflow(operation: "list")` to get all active stories.

2. Format as a clear summary table:

   | # | Title | Status | Progress | Branch | Age |
   |---|-------|--------|----------|--------|-----|
   | 1 | Add retry logic | Executing | 3/5 steps | workflow/add-retry-... | 2h |

3. If the current workspace is a worktree, highlight which story it belongs to using `aura_workflow(operation: "get_by_path", workspacePath: "<cwd>")`.

4. Suggest actions based on status:
   - **Created/Analyzed/Planned** → "Run `/aura-run-story` to start working"
   - **Executing** → "Run `/aura-run-story` to resume"
   - **ReadyToComplete** → "Run `/aura-run-story` to finalize and create PR"
   - **Completed** → "PR created, consider `/aura-cleanup`"

5. If no stories exist, suggest creating one with `/aura-create-story`.
