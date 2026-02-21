---
description: Work on an Aura story interactively — browse steps, write code, and finalize to PR without leaving this Copilot session.
---

# Work on Story

You are working on an Aura story interactively from the main workspace. All code changes target the story's git worktree via absolute paths.

## Step 1: Find or Select Story

List active stories and let the user choose:

```
aura_workflow(operation: "list")
```

If the user provides a story ID, get its details:

```
aura_workflow(operation: "get", storyId: "<id>")
```

Present the story title, status, step count, and worktree path. If the story has no steps yet, suggest using `enrich` to add them.

## Step 2: Get Next Step

```
aura_workflow(operation: "next_step", storyId: "<id>")
```

Present the step to the user:
- **Step name** and description
- **Progress** (e.g., "2/8 steps done, Wave 2/3")
- **Worktree path** for file operations
- **Prior step outputs** for context

Ask the user if they want to proceed with this step or jump to a different one.

## Step 3: Start the Step

```
aura_workflow(operation: "start_step", storyId: "<id>", stepId: "<stepId>")
```

This marks the step as Running. Now implement it.

## Step 4: Implement

Use these conventions for the worktree:

- **Edit files**: Use absolute paths — `edit(path: "<worktreePath>/src/...")`.
- **View files**: `view(path: "<worktreePath>/src/...")`.
- **Search code**: `aura_search(query: "...", workspacePath: "<repositoryPath>")` — RAG indexes the main repo.
- **Navigate**: `aura_navigate(...)` with `solutionPath: "<worktreeSolutionPath>"`.
- **Validate**: `aura_validate(operation: "compilation", solutionPath: "<worktreeSolutionPath>")`.
- **Git**: Run git commands with `cd <worktreePath> && git ...`.

## Step 5: Complete the Step

When the work is done and validated:

```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "completed", output: "Summary of changes made")
```

Then go back to **Step 2** for the next step.

## Step 6: Finalize

When all steps are complete, finalize the story:

```
aura_workflow(operation: "complete", storyId: "<id>")
```

Report the PR URL to the user.

## Key Reminders

- Your cwd is the **main workspace**, not the worktree — always use absolute paths
- The `worktreeSolutionPath` from step context tells you where the `.sln` file is in the worktree
- RAG queries use `repositoryPath` (main repo), file edits use `worktreePath`
- Load the full pattern for reference: `aura_pattern(operation: "get", name: "interactive-story")`
