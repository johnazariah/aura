# Pattern: Interactive Story Execution

Drive an Aura story from creation to PR entirely within a Copilot CLI session, using worktrees for git isolation.

> **Entry point**: Use `.github/prompts/aura.run-story.prompt.md` (`/aura-run-story`) to bootstrap a session.

## When to Use

- You want to work on an Aura story interactively from the main workspace
- You prefer to review and write code yourself rather than automated dispatch
- You want to use Aura's code intelligence (RAG, Roslyn, TreeSitter) while editing in a worktree

## Prerequisites

- Aura service is running (`curl http://localhost:5300/health`)
- Aura MCP tools are available (`.vscode/mcp.json` configured)

## Interactive Workflow

### Phase 1: Story Setup

Create a new story or pick up an existing one.

**Create from a description (no GitHub issue):**
```
aura_workflow(operation: "create", title: "My feature", description: "...", repositoryPath: "/path/to/repo")
```

**Create from a GitHub issue:**
```
aura_workflow(operation: "create", issueUrl: "https://github.com/owner/repo/issues/123", repositoryPath: "/path/to/repo")
```

**List active stories:**
```
aura_workflow(operation: "list")
```

**Get full story context:**
```
aura_workflow(operation: "get", storyId: "<id>")
```

Note the `worktreePath` and `repositoryPath` from the response — all file operations target the worktree, all RAG queries use the main repo.

### Phase 2: Add Steps

If the story has no steps, break it down and add them via `enrich`:

```
aura_workflow(operation: "enrich", storyId: "<id>", steps: [
  { "name": "Implement core logic", "capability": "coding", "description": "..." },
  { "name": "Add tests", "capability": "testing", "description": "..." },
  { "name": "Validate build", "capability": "validation", "description": "Run build and fix errors" }
])
```

**Tips for good steps:**
- Use `aura_search` first to understand the codebase
- 3-6 focused steps per story
- Each step = one committable unit of work
- Include validation/build as a step

### Phase 3: Step Execution Loop

Repeat for each step until all are complete.

#### 3a. Get Next Step

```
aura_workflow(operation: "next_step", storyId: "<id>")
```

This returns:
- Step details (name, description, capability)
- `worktreePath` and `worktreeSolutionPath` for file/build operations
- Prior step outputs for context
- Progress summary

#### 3b. Start the Step

```
aura_workflow(operation: "start_step", storyId: "<id>", stepId: "<stepId>")
```

Marks the step as Running and returns full context.

#### 3c. Do the Work

Use Aura tools for code intelligence — they work from the main workspace because RAG indexes the main repository:

- `aura_search(query: "...", workspacePath: "<repositoryPath>")` — find relevant code
- `aura_navigate(operation: "callers", ...)` — understand relationships
- `aura_inspect(operation: "type_members", ...)` — explore types

Edit files using **absolute paths** to the worktree:
```
edit(path: "<worktreePath>/src/MyProject/MyFile.cs", ...)
view(path: "<worktreePath>/src/MyProject/MyFile.cs")
```

Validate changes using the worktree solution path:
```
aura_validate(operation: "compilation", solutionPath: "<worktreeSolutionPath>")
aura_validate(operation: "tests", projectPath: "<worktreePath>/tests/MyProject.Tests")
```

Run git commands in the worktree:
```
cd <worktreePath> && git add -A && git commit -m "implement step N"
```

#### 3d. Complete the Step

```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "completed", output: "Brief summary of what was done")
```

If the step doesn't apply or was already covered by a previous step:
```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "skipped", skipReason: "Already handled in step 2")
```

If the step failed and needs retry:
```
aura_workflow(operation: "update_step", storyId: "<id>", stepId: "<stepId>",
              status: "failed", error: "What went wrong")
```

Then loop back to 3a for the next step.

### Phase 4: Finalize

When all steps are complete or skipped:

```
aura_workflow(operation: "complete", storyId: "<id>")
```

This will:
1. Squash all commits into one clean commit
2. Push the branch
3. Create a draft PR with story context
4. For issue-based stories: include "Closes {issueUrl}" in PR body

## Key Rules

- **Always use absolute paths** when editing worktree files — your cwd is the main workspace
- **Use `repositoryPath` for RAG queries** — the main repo is indexed, not the worktree
- **Use `worktreeSolutionPath` for validation** — build/test must run in the worktree
- **Commit in the worktree** — `cd <worktreePath> && git commit ...`
- **Don't modify the main workspace** — all code changes go to the worktree

## Jumping to a Specific Step

You can skip the guided flow and work on any step directly:

```
aura_workflow(operation: "step_context", storyId: "<id>", stepId: "<stepId>")
aura_workflow(operation: "start_step", storyId: "<id>", stepId: "<stepId>")
```

## Anti-patterns

- ❌ Editing files in the main workspace instead of the worktree
- ❌ Running `aura_validate` without specifying the worktree solution path
- ❌ Forgetting to call `update_step` after completing work
- ❌ Using relative paths — they resolve to cwd (main workspace), not worktree
- ❌ Leaving steps as Pending when they don't apply — skip them explicitly