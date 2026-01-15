# Story Chat: Unified Development Experience

**Status:** ✅ Complete  
**Completed:** 2026-01-15  
**Created:** 2026-01-15

> Core implementation done. Quick Actions bar (Build/Test/Commit/PR buttons) deferred as polish item.

## Overview

Evolve the existing **Workflow** into a **Story** with chat-first interaction and bidirectional issue sync. The Workflow entity already provides everything we need - worktree, branch, steps, database persistence, and UI. We're adding:

1. **Workflow-level chat** - converse with the agent without pre-planning steps
2. **Bidirectional issue sync** - GitHub/ADO issues stay in sync with story state

## What We Already Have

| Capability | Status | Location |
|------------|--------|----------|
| Workflow entity with worktree/branch | ✅ | `Workflow.cs` |
| Steps with execution/approval | ✅ | `WorkflowStep.cs` |
| GitHub issue linking | ✅ | `IssueUrl`, `IssueOwner`, `IssueRepo`, `IssueNumber` |
| Workflow panel UI | ✅ | `workflowPanelProvider.ts` |
| Workflow tree view | ✅ | `workflowTreeProvider.ts` |
| Step-level chat | ✅ | `ChatWithStepAsync` |
| Database persistence | ✅ | `DeveloperDbContext` |
| Automation modes | ✅ | `AutomationMode` |

**A Workflow IS a Story.** We just need to add chat at the workflow level and make issue sync bidirectional.

## The Problem

Current workflow has rigid ceremony:

1. Create workflow
2. Click "Analyze" → wait
3. Click "Plan" → wait for steps
4. For each step: Execute → Review → Approve/Reject
5. Repeat

This doesn't match how developers actually work:

- "Just implement this feature"
- "Now add tests"  
- "The build is broken, fix it"
- "Actually, let's refactor that part"

## The Solution

Add a **chat input** to the existing workflow panel. The developer can:

1. Describe what they want → agent implements
2. Say "run the tests" → agent runs tests
3. Say "fix the build errors" → agent runs build-fix loop
4. Say "commit this" → agent commits
5. Say "create a PR" → agent creates PR

**Steps/checkpoints are optional**, created when planned or on user request.

---

## UX Design

### Entry Points

**Option A: From GitHub Issue**

```
Command: "Aura: Start Story from Issue"
    ↓
Enter issue URL: https://github.com/org/repo/issues/123
    ↓
[Creates worktree, opens new VS Code window]
    ↓
Story Chat panel auto-opens with issue context pre-loaded
```

**Option B: From Scratch**

```
Command: "Aura: Start New Story"
    ↓
Enter story title: "Add caching to UserService"
    ↓
[Creates worktree, opens new VS Code window]
    ↓
Story Chat panel auto-opens
```

**Option C: Continue Existing**

```
Open folder that is an Aura worktree
    ↓
Extension queries API: "workflow by worktree path?"
    ↓
Workflow Chat panel auto-opens with history
```

---

### The Story Chat Panel

```
┌─────────────────────────────────────────────────────────────────┐
│  📖 Add caching to UserService                          [≡] [×] │
│  ├─ Branch: feature/add-caching-abc123                          │
│  ├─ Issue: #123 (open in browser)                               │
│  └─ Worktree: ~/worktrees/add-caching                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  ⚙️ Settings                                                ││
│  │  ┌──────────────────┐  ┌──────────────────┐                 ││
│  │  │ [ ] Auto-approve safe actions                        │   ││
│  │  └──────────────────────────────────────────────────────┘   ││
│  └─────────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 🤖 Agent                                                    ││
│  │ I see you're working on issue #123: "Add caching to         ││
│  │ UserService". Based on the issue description:               ││
│  │                                                             ││
│  │ > The UserService.GetUserAsync method is called frequently  ││
│  │ > and hits the database every time. Add Redis caching.      ││
│  │                                                             ││
│  │ I can see `UserService.cs` in your codebase. Ready to       ││
│  │ implement when you are.                                     ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 👤 You                                                      ││
│  │ Go ahead and implement it                                   ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 🤖 Agent                                                    ││
│  │ I'll add caching to UserService.                            ││
│  │                                                             ││
│  │ ✅ Created: src/Services/ICacheService.cs                   ││
│  │    [View] [Revert]                                          ││
│  │                                                             ││
│  │ ✅ Modified: src/Services/UserService.cs                    ││
│  │    [View Diff] [Revert]                                     ││
│  │                                                             ││
│  │ ✅ Modified: src/Startup.cs (registered cache service)      ││
│  │    [View Diff] [Revert]                                     ││
│  │                                                             ││
│  │ 🔨 Building... ✅ Build succeeded                           ││
│  │                                                             ││
│  │ What's next? I can:                                         ││
│  │ • Add unit tests for the caching logic                      ││
│  │ • Add integration tests with Redis                          ││
│  │ • Commit these changes                                      ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 👤 You                                                      ││
│  │ Add unit tests, then commit                                 ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 🤖 Agent                                                    ││
│  │ ✅ Created: tests/UserServiceCacheTests.cs                  ││
│  │    [View] [Revert]                                          ││
│  │                                                             ││
│  │ 🧪 Running tests... ✅ 3 passed, 0 failed                   ││
│  │                                                             ││
│  │ 📝 Committed: "feat(user): add Redis caching to UserService"││
│  │    [View Commit]                                            ││
│  │                                                             ││
│  │ Ready to push and create PR?                                ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  [Type a message...]                                    [Send]  │
│                                                                 │
│  Quick Actions: [Run Tests] [Build] [Commit] [Create PR]        │
└─────────────────────────────────────────────────────────────────┘
```

---

### Key UX Elements

#### 1. Story Header

- Title (from issue or manual)
- Branch name (clickable → opens git)
- Issue link (clickable → opens GitHub)
- Worktree path (clickable → opens folder)

#### 2. Settings Bar

- **Auto-approve**: Skip confirmation for safe operations (read, search, build)
- Expands to show more: model selection, agent override

#### 3. Chat Area

- Streaming responses
- **File change cards**: Show files created/modified with [View] [Diff] [Revert]
- **Tool execution**: Show build/test output inline
- **Suggestions**: Agent offers next steps

#### 4. Quick Actions Bar

- Common actions as buttons (Run Tests, Build, Commit, Create PR)
- These just send pre-defined messages to the chat

---

## Develop-Test Loop

The agent has access to all the tools needed for the loop:

```
┌───────────────────────────────────────────────────────────┐
│                    DEVELOP-TEST LOOP                      │
├───────────────────────────────────────────────────────────┤
│                                                           │
│    ┌─────────┐    ┌─────────┐    ┌─────────┐            │
│    │  Code   │───▶│  Build  │───▶│  Test   │            │
│    └─────────┘    └─────────┘    └─────────┘            │
│         ▲              │              │                  │
│         │              ▼              ▼                  │
│         │        ┌─────────┐    ┌─────────┐            │
│         └────────│  Fix    │◀───│ Analyze │            │
│                  │ Errors  │    │ Failures│            │
│                  └─────────┘    └─────────┘            │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

### Tools Available in Story Chat

| Tool | Purpose |
|------|---------|
| `file.read`, `file.write`, `file.list` | Code editing |
| `dotnet.build_until_success` | Build with auto-fix loop |
| `cargo.build_until_success` | Rust build loop |
| `npm.build_until_success` | TypeScript build loop |
| `shell.execute` | Run tests, custom commands |
| `git.status`, `git.diff`, `git.commit` | Version control |
| `git.push`, `git.create_pr` | Publishing |
| `roslyn.validate`, `roslyn.find_symbol` | C# analysis |
| `rag.search` | Find relevant code |
| `github.list_workflow_runs` | Check CI status |
| `github.trigger_workflow` | Trigger CI |

### Build & Test Execution

Builds and tests run when:

1. **User asks** - "build it", "run tests", clicks Quick Action button
2. **Part of workflow step** - Agent includes build/test as part of completing a task
3. **NOT** automatically on every file write (too noisy)

The agent is smart enough to know when to build:

- After implementing a feature → build to verify
- After fixing errors → build to confirm fix
- Before committing → build + test to validate

---

## Bidirectional Issue Sync

Stories linked to GitHub/ADO issues stay synchronized. **Conflict-free by design:**

- **GitHub is append-only**: We never edit the issue body, only add comments
- **Comments are timestamped**: Full audit trail preserved
- **Local enrichments are posted**: Technical notes, analysis, decisions - all go to GitHub

### The Model

```
GitHub Issue (Append-Only Log):
┌─────────────────────────────────────────────────────────┐
│ Title: Add caching to UserService                       │
│ Body: Add Redis caching to improve performance          │  ← Immutable
├─────────────────────────────────────────────────────────┤
│ Comment 1: 🤖 Started work in branch `feature/...`      │
├─────────────────────────────────────────────────────────┤
│ Comment 2: 🤖 Technical Context:                        │  ← Analysis posted
│            - Use 5 min TTL                              │
│            - Invalidate on update                       │
├─────────────────────────────────────────────────────────┤
│ Comment 3: 🤖 Checkpoint: Caching implemented           │  ← Progress posted  
│            - Created ICacheService.cs                   │
│            - 3 tests passing                            │
├─────────────────────────────────────────────────────────┤
│ Comment 4: 🤖 PR created: #456                          │  ← Completion posted
└─────────────────────────────────────────────────────────┘
```

### Aura → Issue

| Event in Aura | Action on Issue |
|---------------|-----------------|
| Story created | Post comment: "Started work in branch `...`" |
| Analysis complete | Post comment: "Technical Context: ..." |
| Checkpoint completed | Post comment with summary |
| Local description enriched | Post comment with enrichments |
| PR created | Post comment with PR link |
| Story completed | Close issue with summary |

### Issue → Aura

| Event on Issue | Action in Aura |
|----------------|----------------|
| Title changed | Update `Workflow.Title` (last-write-wins, safe) |
| Body edited | Read as context, our enrichments are in comments |
| New comment added | Include in chat context on next sync |
| Issue closed | Mark workflow as completed |

**No merge conflicts** because:
- Issue body changes → we read it fresh each time
- Our enrichments → already posted as comments, never lost
- Everything is additive

### Sync Endpoint

```csharp
// POST /api/developer/workflows/{id}/sync-from-issue

public async Task<SyncResult> SyncFromIssueAsync(Guid workflowId, CancellationToken ct)
{
    var workflow = await GetByIdAsync(workflowId, ct);
    var issue = await _gitHub.GetIssueAsync(
        workflow.IssueOwner!, workflow.IssueRepo!, workflow.IssueNumber!.Value, ct);
    
    var changes = new List<string>();
    
    // Title: last-write-wins (simple, safe)
    if (workflow.Title != issue.Title)
    {
        workflow.Title = issue.Title;
        changes.Add("title");
    }
    
    // Status: if closed on GitHub, close here
    if (issue.State == "closed" && workflow.Status != WorkflowStatus.Completed)
    {
        workflow.Status = WorkflowStatus.Completed;
        changes.Add("status");
    }
    
    // Note: We DON'T overwrite Description - local enrichments live there
    // The original issue body is always available via API
    // Our enrichments are also posted as comments, so nothing is lost
    
    await UpdateAsync(workflow, ct);
    return new SyncResult(Updated: changes.Count > 0, Changes: changes);
}
```

### Post Enrichments to Issue

When we enrich locally (analysis, technical notes), post to GitHub:

```csharp
public async Task PostEnrichmentAsync(Guid workflowId, string enrichment, CancellationToken ct)
{
    var workflow = await GetByIdAsync(workflowId, ct);
    
    if (workflow.IssueOwner is null) return;
    
    await _gitHub.PostCommentAsync(
        workflow.IssueOwner,
        workflow.IssueRepo!,
        workflow.IssueNumber!.Value,
        $"📋 **Technical Context**\n\n{enrichment}",
        ct);
}
```
```

---

## Implementation

### Backend Changes

#### 1. Add Chat History to Workflow Entity

```csharp
// Workflow.cs

// Chat history for workflow-level conversation
public string? ChatHistory { get; set; }  // JSON array of messages
```

#### 2. Add EF Migration

```csharp
migrationBuilder.AddColumn<string>(
    name: "chat_history",
    table: "workflows",
    type: "jsonb",
    nullable: true);
```

#### 3. Add Workflow Chat Endpoint

```csharp
// POST /api/developer/workflows/{id}/chat
public record WorkflowChatRequest(string Message);

public record WorkflowChatResponse(
    string Response,
    IReadOnlyList<ToolExecution> ToolExecutions,
    IReadOnlyList<FileChange> FileChanges);
```

#### 4. Add Workflow Chat Streaming Endpoint

```csharp
// GET /api/developer/workflows/{id}/chat/stream?message=...
// Returns SSE stream
```

#### 5. Add WorkflowService.ChatAsync

```csharp
public async Task<WorkflowChatResponse> ChatAsync(
    Guid workflowId,
    string message,
    CancellationToken ct)
{
    var workflow = await GetByIdAsync(workflowId, ct);
    
    // Build context: issue description + RAG + recent chat history
    var context = BuildChatContext(workflow);
    
    // Get coding agent
    var agent = _agentRegistry.GetBestForCapability("software-development");
    
    // Execute with tools, working directory = worktree
    var result = await agent.ExecuteAsync(context, ct);
    
    // Append to chat history
    await AppendChatHistory(workflowId, message, result, ct);
    
    return result;
}
```

### Extension Changes

#### 1. Workflow Chat Panel Provider

Combines current workflow panel + agent chat into unified experience.

```typescript
// workflowChatPanelProvider.ts
export class WorkflowChatPanelProvider {
    openWorkflowChat(workflowId: string): void;
    sendMessage(message: string): Promise<void>;
    onStreamChunk(callback: (chunk: string) => void): void;
}
```

#### 2. Auto-Open Detection

```typescript
// extension.ts - on activation
const workflow = await auraApi.getWorkflowByWorktreePath(workspaceRoot);
if (workflow) {
    workflowChatPanelProvider.openWorkflowChat(workflow.id);
}
```

The workflow already has a GUID in the database with `WorktreePath` stored. No marker file needed.

### Quick Actions

Quick action buttons just send messages:

| Button | Message Sent |
|--------|--------------|
| Run Tests | "Run the tests and show me the results" |
| Build | "Build the project" |
| Commit | "Commit the current changes with an appropriate message" |
| Create PR | "Push and create a pull request" |

#### Quick Actions Prototype

```
┌─────────────────────────────────────────────────────────────────┐
│  Quick Actions                                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │ 🔨 Build│  │ 🧪 Test │  │ 📝 Commit│  │ 🚀 PR   │           │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘           │
│                                                                 │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │ 📊 Status│  │ ↩️ Undo │  │ 📋 Plan │  │ ✅ Done │           │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Button Actions:
- Build    → "Build the project and show any errors"
- Test     → "Run the tests and summarize results"  
- Commit   → "Commit the current changes with an appropriate message"
- PR       → "Push the branch and create a pull request"
- Status   → "Show git status and what's changed"
- Undo     → "Revert the last file changes you made"
- Plan     → "Outline the remaining work as a plan"
- Done     → "We're done - summarize what was accomplished and close the issue"
```

These are **accelerators**, not required. User can always just type.

---

### Checkpoints

Checkpoints are named moments in the story where work was reviewed. They provide:
- **Audit trail** - What was done and when
- **Rollback points** - "Undo everything since checkpoint X"
- **Progress visibility** - See what's been accomplished

#### When Checkpoints Are Created

1. **After completing a workflow step** - if the story has planned steps
2. **When user asks** - "Create a checkpoint called 'caching implemented'"
3. **Before risky operations** - agent auto-creates before major refactors

#### Checkpoint UI in Chat

```
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ ────────────── ✅ Checkpoint: Caching Implemented ───────── ││
│  │                                                             ││
│  │ Files changed:                                              ││
│  │ • src/Services/ICacheService.cs (created)                   ││
│  │ • src/Services/UserService.cs (modified)                    ││
│  │ • src/Startup.cs (modified)                                 ││
│  │                                                             ││
│  │ Tests: 3 passed, 0 failed                                   ││
│  │ Build: ✅ Success                                           ││
│  │                                                             ││
│  │ [View All Changes] [Rollback to Here]                       ││
│  └─────────────────────────────────────────────────────────────┘│
```

#### Checkpoint Data Model

Checkpoints reuse the existing `WorkflowStep` entity:
- `Name` → checkpoint name
- `Output` → summary of what was done
- `Status` → `Completed`
- `CompletedAt` → when checkpoint was created

No schema change needed - just a conceptual reframe.

---

## Migration Path

### Phase 1: Add Workflow Chat (This Spec)

- Add `ChatHistory` to Workflow
- Add chat endpoint
- Add `GET /api/developer/workflows/by-path?path=...` endpoint
- Add chat input to existing workflow panel
- Keep existing Analyze/Plan/Execute buttons working

### Phase 2: Bidirectional Issue Sync

- Add `/api/developer/workflows/{id}/sync-from-issue` endpoint
- Add "Refresh from Issue" button in UI
- Post checkpoint summaries to issue

### Phase 3: UX Polish

- Auto-open workflow panel when opening a workflow worktree
- Quick action buttons
- Streamlined panel layout

---

## Success Criteria

1. **User can implement a feature through chat alone** - no need to click Analyze/Plan first
2. **Chat persists across sessions** - reopen worktree, continue where you left off
3. **Issue sync works bidirectionally** - changes on GitHub reflect in Aura
4. **Existing workflow UI still works** - steps/checkpoints are optional, not removed
5. **Quick actions accelerate common tasks** - Build, Test, Commit, PR buttons work

---

## Out of Scope

- Webhooks for real-time issue sync (future - polling/manual for now)
- Voice input (future)
- Multi-agent collaboration (future)
- Azure DevOps issue sync (future - GitHub first)

---

## References

- [Copilot CLI Parity Spec](copilot-cli-parity.md)
- [ADR-012: Tool-Using Agents](../../adr/012-tool-using-agents.md)
- [Assisted Workflow UI Spec](../../spec/assisted-workflow-ui.md)
