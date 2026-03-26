# Aura Project Executive Summary

> **Review Date:** 2026-02-21
> **Current Version:** v1.3.1
> **Project Status:** ✅ Production Ready

## What Is Aura?

Aura is an **AI-powered development assistant with local code intelligence**. It indexes your codebase locally and exposes tools to GitHub Copilot via MCP (Model Context Protocol), enabling context-aware code generation, navigation, and refactoring.

**Key Architecture Principle:** Local code intelligence (Roslyn, TreeSitter, pgvector RAG) + cloud LLM inference (Azure OpenAI by default; Ollama for local).

---

## Current Capabilities

### 1. Code Intelligence (Local)

| Capability | Technology | Languages |
|------------|------------|-----------|
| **Semantic code search** | pgvector RAG | All indexed languages |
| **Code graph** (types, methods, relationships) | Roslyn | C# (full semantic) |
| **Code graph** (syntax-based) | TreeSitter | TypeScript, Python, Go, Rust, F#, PowerShell |
| **Navigation** (callers, implementations, derived types, usages) | Roslyn/ts-morph | C#, TypeScript |
| **Refactoring** (rename, extract method, move type, etc.) | Roslyn/ts-morph/rope | C#, TypeScript, Python |
| **Test generation** | Custom | C# |
| **Build/lint validation** | Native tooling | C#, TypeScript, Python, Go, Rust |

### 2. MCP Tools (8 Meta-Tools)

Exposed to GitHub Copilot for agentic workflows:

| Tool | Purpose |
|------|---------|
| `aura_search` | Semantic code search via RAG |
| `aura_navigate` | Find callers, implementations, references, usages |
| `aura_inspect` | Explore type members, list types |
| `aura_validate` | Run builds, tests, linting |
| `aura_refactor` | Rename, extract, move, change signature |
| `aura_generate` | Create types, tests, methods, properties |
| `aura_pattern` | Load step-by-step operational patterns |
| `aura_workflow` | Manage development stories |

### 3. Story/Workflow System

| Feature | Description |
|---------|-------------|
| **GitHub Issue Integration** | Start work from a GitHub issue URL |
| **Git Worktree Isolation** | Each story gets its own branch/directory |
| **Wave-based Execution** | Steps grouped into waves for parallel dispatch |
| **Assisted Mode** | Review each step before proceeding |
| **Autonomous Mode** | Let agents run multiple steps automatically |
| **Build-Fix Loops** | Iteratively build and fix until success |
| **PR Creation** | Commit, push, and create PR when done |

### 4. VS Code Extension

| Feature | Description |
|---------|-------------|
| **Story Tree View** | All stories grouped by status |
| **Story Panel** | Create, analyze, plan, execute stories |
| **System Status View** | Health, indexing stats, LLM status |
| **Research Library View** | (Researcher Module) Academic paper management |
| **Index Commands** | Onboard workspace, reindex, check status |

### 5. Agent System

| Feature | Description |
|---------|-------------|
| **Hot-reloadable** | Drop `.md` files in `agents/` → immediately available |
| **YAML-configurable language agents** | Define tools per language in `agents/languages/*.yaml` |
| **ReAct execution** | Tool-using agents with retry loops |
| **Sub-agent spawning** | Hierarchical task delegation |
| **Token budget awareness** | Agents monitor context window capacity |
| **Reflection** | Self-critique step for quality |

### 6. LLM Providers

| Provider | Status | Notes |
|----------|--------|-------|
| Azure OpenAI | ✅ | Default for production |
| OpenAI | ✅ | GPT-4, GPT-4o |
| Ollama | ✅ | Local inference |
| Streaming | ✅ | Token-by-token output |
| Structured output | ✅ | Schema-enforced JSON |

### 7. Researcher Module (New - Feb 2026)

Academic paper and research management:
- **Entities:** Source, Excerpt, Concept, ConceptLink, Synthesis
- **Fetchers:** ArXiv, Semantic Scholar, web pages
- **Services:** PDF extraction, library management
- **VS Code integration:** Import, search, excerpt extraction

---

## Technical Health

| Metric | Value |
|--------|-------|
| **Tests** | 849 passing |
| **Architecture** | .NET Aspire, PostgreSQL + pgvector, Windows Service |
| **Platform** | Windows (production), macOS (local dev) |
| **Recent cleanup** | 7,093 lines deleted (internal agent arch removed) |
| **API design** | RFC 7807 problem details, unified REST endpoints |

---

## Recent Major Releases

### v1.3.1 (Jan 23, 2026)
- Agentic Execution v2: sub-agents, retry loops, token budget tracking
- Modern C# support: required properties, init setters, primary constructors

### v1.3.0 (Jan 19, 2026)
- MCP tools consolidated (28 → 8 meta-tools)
- Full Roslyn refactoring suite
- Python refactoring via rope
- Pattern-driven workflows
- Guardian system for CI/coverage/docs
- Worktree support with cache invalidation

---

## What's In Progress

| Feature | Description |
|---------|-------------|
| Python Inspect & Validate | Extend aura_inspect/aura_validate to Python |
| macOS CI & Distribution | CI builds, Homebrew cask, menu bar app |

---

## What's Not Yet Done (Backlog)

- Azure DevOps / Jira integration (non-GitHub issue trackers)
- Web UI (browser-based interface)
- Dependency graph edges in code graph
- Azure AD authentication for LLM
- Cost tracking for cloud LLM usage
- Parallel step execution

---

## Key Takeaways

1. **Production-ready** for C# and TypeScript development workflows
2. **GitHub Copilot integration** via MCP is the primary execution path (internal agents removed)
3. **Local-first** code intelligence with cloud LLM acceleration
4. **Hot-reloadable** agents and patterns for rapid iteration
5. **Strong test coverage** (849 tests) and clean architecture
6. **Agent quality testing phase** - focus is on fixing Aura itself, not generated code

---

# Implementation Plan: Story-to-PR Gap Coverage

> **Goal:** Use Aura to implement missing features in the story-to-PR workflow, dogfooding the system to refine UX.

## Current Gaps

| Gap | Priority | Description |
|-----|----------|-------------|
| Issue progress comments | High | Post updates to GitHub issue during story execution |
| Issue closure on completion | High | Auto-close linked issue when PR is created |
| Draft → Ready guidance | Medium | UX to help user mark PR ready for review |
| PR template support | Medium | Use `.github/PULL_REQUEST_TEMPLATE.md` if present |
| Execution status visibility | High | Real-time feedback in extension during Copilot execution |

## Approach

We will create an **Aura story** (not from GitHub issue) to implement these features. The story will:
1. Create a worktree for isolated development
2. Use Copilot CLI with Aura MCP tools to implement the changes
3. Create a PR back to main when complete

This dogfoods the system and lets us observe UX friction points firsthand.

---

## Stories to Create

### Story 1: GitHub Issue Progress Updates

**Title:** Add progress comments to linked GitHub issues during story execution

**Description:**
When a story is linked to a GitHub issue, post progress comments at key milestones:
- When story analysis completes
- When planning completes (with step summary)
- When each wave of steps completes
- When story is ready for PR

Use existing `GitHubService.PostCommentAsync()`. Add calls in `StoryService` at appropriate lifecycle points.

**Acceptance Criteria:**
- [ ] Comment posted when story enters `Analyzing` state
- [ ] Comment posted when story enters `Planned` state (include step count)
- [ ] Comment posted when each wave completes (progress: "Wave 2/4 complete")
- [ ] Final comment when PR is created (include PR link)
- [ ] Comments are formatted with 🤖 badge and Aura branding
- [ ] Gracefully handle missing GitHub config (log warning, don't fail)

---

### Story 2: Auto-Close Issue on PR Creation

**Title:** Auto-close linked GitHub issue when PR is created

**Description:**
When a story with a linked issue creates a PR:
1. Include "Closes #N" or "Fixes {issueUrl}" in PR body (already done?)
2. Optionally auto-close the issue immediately (configurable)
3. Add a final comment to the issue with PR link

Check if "Closes #N" syntax is already in PR body. If not, add it. Make auto-close behavior configurable via `StoryOptions`.

**Acceptance Criteria:**
- [ ] PR body includes "Closes {issueUrl}" footer
- [ ] Issue receives comment with PR link when PR created
- [ ] Optional: `autoCloseIssue` config to close immediately (default: false, let GitHub handle via PR merge)
- [ ] Works for both `/complete` and `/finalize` endpoints

---

### Story 3: Execution Status Visibility in Extension

**Title:** Show real-time Copilot execution status in VS Code extension

**Description:**
When Copilot CLI is executing a step, the extension should show:
- Spinner/progress indicator on the step
- Elapsed time
- Ability to cancel execution
- Stream partial output if available

Currently the extension polls for step status. Enhance to show more granular progress.

**Acceptance Criteria:**
- [ ] Step card shows "Executing..." with spinner when in progress
- [ ] Elapsed time shown during execution
- [ ] Cancel button that calls `/steps/{stepId}/cancel` endpoint
- [ ] Output updates as they arrive (polling or SSE)

---

### Story 4: PR Template Support

**Title:** Use repository PR template when creating pull requests

**Description:**
When creating a PR, check for `.github/PULL_REQUEST_TEMPLATE.md` in the repository. If present, use it as the base for the PR body, filling in placeholders or appending Aura's generated content.

**Acceptance Criteria:**
- [ ] Check for template at `.github/PULL_REQUEST_TEMPLATE.md`
- [ ] Also check `.github/PULL_REQUEST_TEMPLATE/default.md`
- [ ] Merge template with Aura-generated content (append or fill placeholders)
- [ ] If no template, use current behavior

---

## Execution Plan

### Phase 1: Setup & Story 1 (Progress Comments)

1. **Create story via Aura extension** (`Ctrl+Shift+W`)
   - Title: "Add progress comments to linked GitHub issues"
   - Repository: `C:\work\aura`
   - This creates worktree + branch

2. **Open worktree in new VS Code window**

3. **Execute story with Copilot CLI**
   - Use MCP tools to navigate codebase
   - Implement changes to `StoryService.cs`
   - Add tests

4. **Validate & finalize**
   - Build passes
   - Tests pass
   - Create PR via `/finalize`

5. **Observe UX friction** and note improvements needed

### Phase 2: Stories 2-4

Repeat the process for remaining stories, incorporating UX learnings from each iteration.

---

---

## Completed Work

### Story 1: GitHub Issue Progress Comments ✅
**PR:** https://github.com/johnazariah/aura/pull/6
- IStoryProgressCommentService with 4 lifecycle hooks
- 17 tests (9 unit + 8 integration), 290 total passing
- Implemented by Aura (6 Copilot CLI steps)

### Story 2: Auto-Close Issue on PR Creation ✅
**PR:** https://github.com/johnazariah/aura/pull/7
- Added "Closes {issueUrl}" to /finalize PR body
- Added progress comment to /finalize endpoint
- Added attribution banner and workflow ID

### Story 3: Elapsed Time During Execution ✅
**PR:** https://github.com/johnazariah/aura/pull/8
- Live elapsed time counter: "Executing... (12s)" → "✓ Completed (1m 45s)"
- Timer starts/stops with step lifecycle

### Story 4: PR Template Support ✅
**PR:** https://github.com/johnazariah/aura/pull/9
- Checks .github/PULL_REQUEST_TEMPLATE.md (and 2 other locations)
- Appends template to both /complete and /finalize PR bodies

### Story 5: Step Status Reconciliation After Crash ✅
**PR:** https://github.com/johnazariah/aura/pull/10
- POST /api/developer/stories/{id}/reconcile endpoint
- Matches git commits to steps by pattern
- Recovers pending/running steps that were actually committed
- Wave 1 automated by Aura, implementation completed manually after service crash

### Story 6: Add Duration & Step Summary to API ✅
**PR:** https://github.com/johnazariah/aura/pull/11
- Added `stepsDone`, `stepsTotal`, `durationMinutes` computed fields to all 3 story endpoints
- Aligned list endpoint with get endpoint fields (automationMode, issueProvider, issueOwner, issueRepo)
- **First story executed via interactive `/work-on-story` pattern from Copilot CLI**
- Created story → analyzed → decomposed → implemented in worktree → pushed → PR

The automated pipeline (`decompose → run` with waves) is **fragile**:
- Service crashes during long Copilot CLI executions (exit code -1)
- Step status gets stuck after crashes
- Most implementation ended up being manual anyway

**Recommendation: Use `/work-on-story` going forward.** The interactive pattern already exists:
- `.github/prompts/aura.work-on-story.prompt.md` — Copilot slash command
- `patterns/interactive-story.md` — Full operational pattern
- Uses `aura_workflow` MCP tool with `next_step`, `start_step`, `update_step` operations
- Copilot works step-by-step in the worktree, using Aura MCP tools for code intelligence
- Human reviews each step before proceeding
- No wave orchestration, no service crashes

---

## Interactive Story Execution from Copilot CLI (Implemented)

> **Date:** 2026-02-21
> **Status:** ✅ Deployed and verified

### What Was Built

Enabled driving Aura stories from creation to PR entirely within a Copilot CLI session, without switching VS Code windows. The worktree still provides git isolation, but all interaction happens from the main workspace.

### Changes (6 files, +212 lines)

| File | Action | Purpose |
|------|--------|---------|
| `.vscode/mcp.json` | Created | Static MCP server registration — fixes Copilot CLI not discovering `aura_*` tools |
| `McpHandler.Workflow.cs` | +191 lines | 3 new `aura_workflow` operations + `BuildStepContext` helper |
| `McpHandler.cs` | Modified | Tool schema updated with new operations |
| `patterns/interactive-story.md` | Created | Operational pattern for interactive story execution |
| `.github/prompts/aura.work-on-story.prompt.md` | Created | Bootstrap prompt (`/work-on-story`) |
| `.github/copilot-instructions.md` | Modified | Added Interactive Story Workflow section |

### New MCP Operations

| Operation | Purpose |
|-----------|---------|
| `next_step` | Returns next actionable step (first Pending in lowest wave) with worktree paths, prior outputs, progress |
| `start_step` | Atomically marks step Running + returns full context. Validates step is Pending or Failed. |
| `step_context` | Read-only rich context for any step — no status change |

All three return: `stepId`, `name`, `description`, `worktreePath`, `worktreeSolutionPath`, `repositoryPath`, `analysis`, `priorStepOutputs`, `progress`, `allSteps`

### Interactive Workflow

```
1. aura_workflow(operation: "list")                    # Find story
2. aura_workflow(operation: "next_step", storyId: ...) # Get next step
3. aura_workflow(operation: "start_step", ...)          # Mark Running
4. Edit files in worktree via absolute paths            # Do the work
5. aura_validate(solutionPath: worktreeSolutionPath)    # Verify
6. aura_workflow(operation: "update_step", status: "completed") # Done
7. Repeat 2-6 until all steps complete
8. aura_workflow(operation: "complete", ...)             # → PR
```

### Key Finding: MCP Registration Gap

Aura MCP server was running and healthy, but Copilot CLI couldn't see any `aura_*` tools. The extension only used dynamic registration (`registerMcpServerDefinitionProvider`) which doesn't reliably reach CLI sessions. Fixed with static `.vscode/mcp.json`.

---

## Active Story

| Field | Value |
|-------|-------|
| **Story ID** | `3ca3e3a1-4204-46f6-8a29-0ad7a7a339ba` |
| **Title** | Add progress comments to linked GitHub issues during story execution |
| **Status** | Created |
| **Branch** | `workflow/add-progress-comments-to-linked-github-issues-during-s` |
| **Worktree** | `C:\work\aura-workflow-add-progress-comments-to-linked-github-issues-during-s` |

### Next Steps

1. **Open worktree in new VS Code window:**
   ```powershell
   code "C:\work\aura-workflow-add-progress-comments-to-linked-github-issues-during-s"
   ```

2. **In that window, use Aura to analyze and plan the story**

3. **Execute steps with Copilot CLI**

4. **Finalize to create PR**

---

## How to Start (Alternative)

```
Command: Aura: Create Story (Ctrl+Shift+W)

Title: Add progress comments to linked GitHub issues during story execution

Description:
When a story is linked to a GitHub issue, post progress comments at key milestones:
- When story analysis completes
- When planning completes (with step summary)  
- When each wave of steps completes
- When story is ready for PR

Use existing GitHubService.PostCommentAsync(). Add calls in StoryService at appropriate lifecycle points.

Gracefully handle missing GitHub config (log warning, don't fail story execution).
```

This will create the worktree and set up the story for execution.
