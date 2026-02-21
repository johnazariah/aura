---
description: Clean up stale Aura stories — delete completed, cancelled, or abandoned stories and their worktrees.
---

# Aura Cleanup

Find and remove stale stories that are no longer needed.

## Steps

1. **List all stories.** Call `aura_workflow(operation: "list")` to get everything.

2. **Identify cleanup candidates.** A story is a candidate if:
   - Status is **Completed** or **Cancelled** (work is done)
   - Status is **Created** and older than 7 days (abandoned)
   - Status is **Failed** (unrecoverable)

3. **Show candidates.** Present them clearly:

   ```
   Stories to clean up:

   | # | Title | Status | Age | Worktree |
   |---|-------|--------|-----|----------|
   | 1 | Add retry logic | Completed | 3d | C:\work\aura-workflow-add-retry... |
   | 2 | Test Case | Created | 20d | C:\work\aura-workflow-test-case... |
   ```

4. **Ask for confirmation.** Let the user pick which to delete (all, specific numbers, or none).

5. **Delete selected stories.** For each confirmed story:
   ```
   aura_workflow(operation: "delete", storyId: "<id>")
   ```

6. **Clean up worktrees.** For each deleted story that had a worktree:
   ```
   cd <repositoryPath> && git worktree remove <worktreePath> --force
   ```
   If the worktree directory still exists after removal:
   ```
   Remove-Item -Recurse -Force <worktreePath>
   ```

7. **Prune stale worktree references:**
   ```
   cd <repositoryPath> && git worktree prune
   ```

8. **Report results.** Show what was cleaned up and disk space recovered.

## Safety

- Never delete stories in **Executing** or **Planned** status without explicit confirmation
- Always show the full list before deleting
- Warn if a story has uncommitted changes in its worktree
