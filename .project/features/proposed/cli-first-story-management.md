# CLI-First Story Management

**Status:** 📋 Proposed
**Created:** 2026-02-21
**Priority:** High
**Category:** Developer Experience

## Problem

Aura's story management is currently accessible through:
1. **REST API** (`/api/developer/stories/*`) — low-level, requires curl/Invoke-RestMethod
2. **MCP tool** (`aura_workflow`) — only usable from within a Copilot Chat/CLI session
3. **VS Code extension** — GUI panels, not available in terminal workflows

The user's primary workflow is now **Copilot CLI in the terminal**. They need high-level slash commands that compose the MCP operations into natural developer workflows. Today, to work on a story, you need to know the exact MCP operation names and parameter formats. Instead, they should be able to say `/work-on-story` and have Copilot drive the entire flow.

## Current State

### Existing Slash Commands (`.github/prompts/`)

| Command | Purpose | Status |
|---------|---------|--------|
| `/aura-run-story` | Interactive step-by-step execution | ✅ Updated (this session) |
| `/start-feature` | Create spec, branch, worktree, scaffold | ✅ Exists |
| `/merge-worktree` | Merge worktree back to main | ✅ Exists |
| `/pick-next-work` | Suggest next piece of work | ✅ Exists |

### Existing MCP Operations (`aura_workflow`)

| Operation | Purpose | Status |
|-----------|---------|--------|
| `list` | List active stories | ✅ |
| `get` | Get story details | ✅ |
| `get_by_path` | Find story by worktree path | ✅ |
| `create` | Create from title/description OR issue URL | ✅ (just added) |
| `enrich` | Add steps from pattern or explicit list | ✅ |
| `next_step` | Get next actionable step | ✅ |
| `start_step` | Mark step as running | ✅ |
| `step_context` | Read-only step context | ✅ |
| `update_step` | Mark step completed/failed/skipped | ✅ |
| `complete` | Squash, push, create draft PR | ✅ |

### What's Missing

There is no slash command for:
- **Listing stories** at a glance
- **Creating a new story** (from description or issue)
- **Syncing GitHub issues** into stories
- **Checking story status** with step progress
- **Resuming** a story you left off
- **Cleaning up** old/stale stories

## Proposed Slash Commands

### Core Story Commands

#### `/aura-stories` — List & Status Dashboard

Show all active stories with progress at a glance.

```
/aura-stories
```

Copilot should:
1. Call `aura_workflow(operation: "list")`
2. Format as a table: title, status, stepsDone/stepsTotal, branch, age
3. Highlight the story for the current worktree (if any)
4. Suggest actions: "work on", "resume", "clean up"

#### `/aura-create-story` — Create a New Story

Create a story from a description or GitHub issue.

```
/aura-create-story Add retry logic to HTTP client with exponential backoff
/aura-create-story --issue https://github.com/owner/repo/issues/42
```

Copilot should:
1. Call `aura_workflow(operation: "create", ...)` with title/description or issueUrl
2. Show the created story: ID, branch, worktree path
3. Ask if user wants to add steps now (via `enrich`) or start working

#### `/aura-run-story` — Interactive Execution (exists as /work-on-story, rename)

Rename from `/work-on-story` to `/aura-run-story` for consistent `aura-*` naming. Covers the full lifecycle: pick story → add steps → implement → PR.

#### `/aura-sync-issues` — Sync GitHub Issues to Stories

Pull open issues from a GitHub repo and create stories for ones that don't have them yet.

```
/aura-sync-issues
/aura-sync-issues --labels bug,enhancement --repo owner/repo
```

Copilot should:
1. List open GitHub issues (using GitHub MCP tools or `gh issue list`)
2. Check which already have linked Aura stories
3. Show unlinked issues and ask which to import
4. For each selected issue, call `aura_workflow(operation: "create", issueUrl: ...)`

#### `/aura-story-status` — Detailed Status for One Story

Show detailed status for a specific story or the current worktree's story.

```
/aura-story-status
/aura-story-status <storyId>
```

Copilot should:
1. Call `aura_workflow(operation: "get", storyId: ...)` or `get_by_path`
2. Show: title, status, each step with status, elapsed time, branch info
3. Suggest next action: "resume working", "skip step", "finalize"

#### `/aura-cleanup` — Clean Up Stale Stories

Delete old stories and remove their worktrees.

```
/aura-cleanup
```

Copilot should:
1. Call `aura_workflow(operation: "list")`
2. Identify stale stories (completed/cancelled, or old with no progress)
3. Show candidates and ask for confirmation
4. Delete via REST API and remove worktrees

### Workflow Composition

These commands compose the low-level MCP operations into natural developer workflows:

```
Developer Journey:

1. "What should I work on?"
   → /pick-next-work (existing) or /aura-sync-issues

2. "Create a story for this"
   → /aura-create-story <description>
   → /aura-create-story --issue <url>

3. "Work on it"
   → /aura-run-story (picks up the story, adds steps, implements)

4. "Where was I?"
   → /aura-stories (overview)
   → /aura-story-status (detailed)

5. "Done, create PR"
   → Handled by /aura-run-story's finalize step

6. "Clean up old stuff"
   → /aura-cleanup
```

## Implementation

Each slash command is a `.github/prompts/aura.*.prompt.md` file. These are **prompt templates**, not code — they instruct Copilot on which MCP operations to call and how to present results.

### Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `.github/prompts/aura.run-story.prompt.md` | Rename from work-on-story | `/aura-run-story` |
| `.github/prompts/aura.stories.prompt.md` | Create | `/aura-stories` dashboard |
| `.github/prompts/aura.create-story.prompt.md` | Create | `/aura-create-story` |
| `.github/prompts/aura.sync-issues.prompt.md` | Create | `/aura-sync-issues` |
| `.github/prompts/aura.story-status.prompt.md` | Create | `/aura-story-status` |
| `.github/prompts/aura.cleanup.prompt.md` | Create | `/aura-cleanup` |
| `.github/prompts/aura.work-on-story.prompt.md` | Delete (renamed) | Replaced by `/aura-run-story` |
| `.github/copilot-instructions.md` | Update | Document all story commands |

### MCP Backend Changes

Most commands use existing MCP operations. New operations needed:

| Operation | For Command | Purpose |
|-----------|-------------|---------|
| `delete` | `/aura-cleanup` | Delete a story and its worktree (REST endpoint exists, wire to MCP) |

### No Server Code Needed (mostly)

The slash commands are prompt files that instruct Copilot to call existing `aura_workflow` MCP operations. The only server change is wiring the existing `DELETE /api/developer/stories/{id}` endpoint through the MCP handler.

## Success Criteria

1. User can create a story from CLI without knowing MCP operation names
2. User can see all stories and their progress at a glance
3. User can sync GitHub issues into Aura stories
4. User can resume working on a story they left off
5. User can clean up completed/stale stories
6. All commands work from Copilot CLI (not just VS Code Chat)

## Out of Scope

- PowerShell CLI tool (`aura.exe story create`) — future consideration
- Azure DevOps / Jira issue sync — only GitHub for now
- Automated story prioritization
- Story templates / archetypes
