---
description: Create a new Aura story from a description or GitHub issue URL, with worktree and branch.
---

# Create Story

Create a new Aura story. This sets up a git worktree and feature branch for isolated development.

## Two Creation Paths

### From a description (no GitHub issue)

If the user provides a plain description:

```
aura_workflow(operation: "create", title: "<title>", description: "<description>", repositoryPath: "<repoPath>")
```

The `repositoryPath` should be the current workspace root.

### From a GitHub issue

If the user provides an issue URL or number:

```
aura_workflow(operation: "create", issueUrl: "https://github.com/owner/repo/issues/123", repositoryPath: "<repoPath>")
```

This fetches the issue title and body from GitHub and posts a "work started" comment.

## After Creation

1. Show the created story: ID, title, branch, worktree path
2. Ask if the user wants to:
   - **Add steps now** — Break the story into implementation steps via `aura_workflow(operation: "enrich", ...)`
   - **Start working** — Jump straight to `/aura-run-story`

## End State

- **Plain stories** → Will produce a draft PR when completed
- **Issue-based stories** → Will produce a draft PR with "Closes {issueUrl}" (auto-closes issue on merge)

## Tips

- Keep titles short and descriptive (they become branch names)
- For issue-based stories, the issue body becomes the story description
- Each story gets its own worktree — you can work on multiple stories in parallel
