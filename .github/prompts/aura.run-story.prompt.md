---
description: Run an Aura story interactively — create or pick up a story, break it into steps, implement each one, and finalize to PR.
---

# Work on Story

You are working on an Aura story interactively from the main workspace. All code changes target the story's git worktree via absolute paths.

## Step 1: Create or Select Story

**If the user wants to start from a description:**

```
aura_workflow(operation: "create", title: "<title>", description: "<description>", repositoryPath: "<repoPath>")
```

**If the user has a GitHub issue:**

```
aura_workflow(operation: "create", issueUrl: "https://github.com/owner/repo/issues/123", repositoryPath: "<repoPath>")
```

**If the user wants to pick up an existing story:**

```
aura_workflow(operation: "list")
aura_workflow(operation: "get", storyId: "<id>")
```

The response includes `worktreePath` (where files live) and `repositoryPath` (where RAG is indexed). Note both — you'll use them throughout.

## Step 2: Add Implementation Steps

If the story has no steps, break it down and add them:

```
aura_workflow(operation: "enrich", storyId: "<id>", steps: [
  { "name": "Implement XYZ service", "capability": "coding", "description": "Create the service class in src/..." },
  { "name": "Add unit tests", "capability": "testing", "description": "Test the new service..." },
  { "name": "Validate build", "capability": "validation", "description": "Run dotnet build and fix errors" }
])
```

**How to craft good steps:**
- Read the story description carefully
- Use `aura_search` to understand the codebase before writing steps
- Keep steps small and focused (3-6 steps is ideal)
- Each step should produce a committable unit of work
- Include a validation/build step

## Step 3: Get Next Step

```
aura_workflow(operation: "next_step", storyId: "<id>")
```

Present the step to the user:
- **Step name** and description
- **Progress** (e.g., "2/8 steps done, Wave 2/3")
- **Worktree path** for file operations
- **Prior step outputs** for context

Ask the user if they want to proceed with this step or skip it.

## Step 4: Start the Step

```
aura_workflow(operation: "start_step", storyId: "<id>", stepId: "<stepId>")
```

This marks the step as Running. Now implement it.

## Step 5: Implement

Use these conventions for the worktree:

- **Search code**: `aura_search(query: "...", workspacePath: "<repositoryPath>")` — RAG indexes the main repo.
- **Navigate**: `aura_navigate(...)` with `solutionPath: "<worktreeSolutionPath>"`.
- **Inspect types**: `aura_inspect(...)` with `solutionPath: "<worktreeSolutionPath>"`.
- **Edit files**: Use absolute paths — `edit(path: "<worktreePath>/src/...")`.
- **View files**: `view(path: "<worktreePath>/src/...")`.
- **Validate**: `aura_validate(operation: "compilation", solutionPath: "<worktreeSolutionPath>")`.
- **Git**: Run git commands with `cd <worktreePath> && git ...`.

**Prefer Aura tools over grep/manual search** — they understand code semantics.

## Step 6: Complete the Step

When the work is done and validated:

```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "completed", output: "Summary of changes made")
```

**If a step doesn't apply** (e.g., decomposition was wrong), skip it:

```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "skipped", skipReason: "Already handled in previous step")
```

Then go back to **Step 3** for the next step.

## Step 7: Finalize

When all steps are complete or skipped, finalize the story:

```
aura_workflow(operation: "complete", storyId: "<id>")
```

This squashes commits, pushes the branch, and creates a draft PR. Report the PR URL to the user.

**End state:**
- **Plain stories** → Draft PR on GitHub
- **Issue-based stories** → Draft PR with "Closes {issueUrl}" in the body (issue auto-closes when PR merges)

## Key Reminders

- Your cwd is the **main workspace**, not the worktree — always use absolute paths
- The `worktreeSolutionPath` from step context tells you where the `.sln` file is in the worktree
- RAG queries use `repositoryPath` (main repo), file edits use `worktreePath`
- Load the full pattern for reference: `aura_pattern(operation: "get", name: "interactive-story")`
- Don't modify the main workspace — all code changes go to the worktree
- Commit after each step: `cd <worktreePath> && git add -A && git commit -m "step N: description"`