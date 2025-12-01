# Developer Module Specification

**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2025-12-01

## Overview

The Developer Module is the **first vertical application** built on Aura Foundation. It provides a local-first workflow for automating software development tasks: from issue creation through implementation to PR generation.

**Key principle:** Local-only MVP. No GitHub/Azure DevOps sync required. Issues are created locally, processed locally, and PRs are prepared locally (push is optional).

## The Local-First Developer Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                    LOCAL-ONLY WORKFLOW                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. CREATE ISSUE (local)                                         │
│     └─> User creates issue with title + description              │
│     └─> Stored in local PostgreSQL                               │
│                                                                  │
│  2. CREATE WORKFLOW                                              │
│     └─> Issue → Workflow with WorkspacePath + GitBranch          │
│     └─> Create git worktree for isolated development             │
│                                                                  │
│  3. DIGEST ISSUE                                                 │
│     └─> issue-digester-agent → structured requirements           │
│     └─> RAG: index relevant codebase context                     │
│                                                                  │
│  4. PLAN IMPLEMENTATION                                          │
│     └─> business-analyst-agent → execution steps                 │
│     └─> Each step has capability + description                   │
│                                                                  │
│  5. EXECUTE STEPS (human-in-the-loop)                            │
│     └─> For each step:                                           │
│         ├─> Select agent by capability                           │
│         ├─> Execute with RAG context                             │
│         ├─> User reviews output                                  │
│         └─> Commit changes                                       │
│                                                                  │
│  6. COMPLETE WORKFLOW                                            │
│     └─> All steps done → workflow complete                       │
│     └─> Ready for PR (local branch exists)                       │
│     └─> Optional: push to remote, create PR                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Philosophy: Simplicity Over Sophistication

From the Origin Story:

> *"The best software is built not by adding features until it works, but by removing complexity until it can't fail."*

The Developer Module learns from hve-hack's mistakes:

| hve-hack (Deleted) | Aura Developer (New) |
|--------------------|----------------------|
| Complex orchestration engine | Simple step-by-step execution |
| Execution planner state machine | User clicks "execute next step" |
| Agent output validator | Agent output is the result |
| Workflow state machines | Linear status progression |
| Plugin discovery service | Capability-based agent selection |

**The user orchestrates. Aura executes.**

## Data Model

### Issue Entity (NEW)

Local issue storage - the starting point for workflows.

```csharp
public sealed class Issue
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.Open;
    public string? RepositoryPath { get; set; }  // Which repo this relates to
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    // Navigation
    public Workflow? Workflow { get; set; }  // One issue → one workflow
}

public enum IssueStatus
{
    Open,
    InProgress,  // Workflow created
    Completed,   // Workflow completed
    Closed       // Manually closed
}
```

### Workflow Entity (EXISTS - enhance)

```csharp
public sealed class Workflow
{
    public Guid Id { get; set; }
    
    // Link to issue (NEW)
    public Guid? IssueId { get; set; }
    public Issue? Issue { get; set; }
    
    // Work item info (for display, copied from issue)
    public required string WorkItemId { get; set; }      // "local:{issueId}" or "github:owner/repo#123"
    public required string WorkItemTitle { get; set; }
    public string? WorkItemDescription { get; set; }
    
    // Execution context
    public required string RepositoryPath { get; set; }  // Original repo path
    public string? WorkspacePath { get; set; }           // Worktree path (created during workflow)
    public string? GitBranch { get; set; }               // Branch name
    
    // Status
    public WorkflowStatus Status { get; set; }
    public string? DigestedContext { get; set; }         // JSON from digestion
    public string? ExecutionPlan { get; set; }           // JSON from planning
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    
    // Navigation
    public ICollection<WorkflowStep> Steps { get; set; } = [];
}
```

### WorkflowStep Entity (EXISTS - good as-is)

```csharp
public sealed class WorkflowStep
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    
    public int Order { get; set; }
    public required string Name { get; set; }
    public required string Capability { get; set; }  // Maps to agent capability
    public string? Description { get; set; }
    
    public StepStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    
    public string? Input { get; set; }   // JSON context for agent
    public string? Output { get; set; }  // JSON result from agent
    public string? Error { get; set; }
    
    public int Attempts { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

## Agent Capabilities Mapping

The workflow steps use **capability** to select agents:

| Step | Capability | Agent |
|------|------------|-------|
| Digest Issue | `digestion` | issue-digester-agent |
| Create Plan | `analysis` | business-analyst-agent |
| Implement Code | `coding` | coding-agent |
| Fix Build Errors | `fixing` | build-fixer-agent |
| Review Code | `review` | code-review-agent |
| Write Docs | `documentation` | documentation-agent |

The `IAgentRegistry.GetBestForCapability(capability)` selects the agent.

## Services

### IIssueService

```csharp
public interface IIssueService
{
    Task<Issue> CreateAsync(string title, string? description, string? repositoryPath, CancellationToken ct = default);
    Task<Issue?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Issue>> ListAsync(IssueStatus? status = null, CancellationToken ct = default);
    Task<Issue> UpdateAsync(Guid id, string? title, string? description, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### IWorkflowService

```csharp
public interface IWorkflowService
{
    // Create workflow from issue
    Task<Workflow> CreateFromIssueAsync(Guid issueId, CancellationToken ct = default);
    
    // Get workflow
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Workflow>> ListAsync(WorkflowStatus? status = null, CancellationToken ct = default);
    
    // Workflow lifecycle
    Task<Workflow> DigestAsync(Guid workflowId, CancellationToken ct = default);
    Task<Workflow> PlanAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowStep> ExecuteStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default);
    Task<Workflow> CompleteAsync(Guid workflowId, CancellationToken ct = default);
    Task<Workflow> CancelAsync(Guid workflowId, CancellationToken ct = default);
}
```

### Workflow Lifecycle

```
Issue Created
     │
     ▼
CreateFromIssueAsync(issueId)
     │
     ├─> Create git branch: feature/issue-{id}
     ├─> Create git worktree at WorkspacePath
     └─> Workflow.Status = Created
     │
     ▼
DigestAsync(workflowId)
     │
     ├─> Run issue-digester-agent
     ├─> Store DigestedContext JSON
     ├─> Index relevant code via RAG
     └─> Workflow.Status = Digested
     │
     ▼
PlanAsync(workflowId)
     │
     ├─> Run business-analyst-agent
     ├─> Parse response into WorkflowSteps
     ├─> Store ExecutionPlan JSON
     └─> Workflow.Status = Planned
     │
     ▼
ExecuteStepAsync(workflowId, stepId) [repeat for each step]
     │
     ├─> Select agent by step.Capability
     ├─> Execute with RAG context
     ├─> Store output, update step status
     └─> Commit changes to worktree
     │
     ▼
CompleteAsync(workflowId)
     │
     ├─> Mark Workflow.Status = Completed
     └─> Ready for PR (branch exists, changes committed)
```

## API Endpoints

All under `/api/developer/` prefix:

### Issues

```http
POST   /api/developer/issues              # Create issue
GET    /api/developer/issues              # List issues
GET    /api/developer/issues/{id}         # Get issue
PUT    /api/developer/issues/{id}         # Update issue
DELETE /api/developer/issues/{id}         # Delete issue
```

### Workflows

```http
POST   /api/developer/issues/{id}/workflow     # Create workflow from issue
GET    /api/developer/workflows                # List workflows
GET    /api/developer/workflows/{id}           # Get workflow with steps
POST   /api/developer/workflows/{id}/digest    # Digest issue context
POST   /api/developer/workflows/{id}/plan      # Create execution plan
POST   /api/developer/workflows/{id}/steps/{stepId}/execute  # Execute step
POST   /api/developer/workflows/{id}/complete  # Mark complete
POST   /api/developer/workflows/{id}/cancel    # Cancel workflow
```

## VS Code Extension Integration

### Sidebar: Workflows Tree View

The extension will add a **Workflows** tree view to the sidebar:

```
📂 Workflows
├── 📋 Issue: Add user authentication
│   ├── Status: Planned
│   ├── Branch: feature/issue-abc123
│   └── Steps:
│       ├── ✅ Digest Issue
│       ├── ✅ Create Plan
│       ├── 🔄 Implement UserService (Running)
│       ├── ⏳ Add unit tests
│       └── ⏳ Update documentation
└── 📋 Issue: Fix login bug
    └── Status: Open (no workflow yet)
```

### Workflow Tab: Unified Chat + Steps View

Clicking a workflow in the sidebar opens it as a **VS Code tab** with integrated chat.

**Key design:**

- **Reverse chronological order** - newest steps/messages at TOP, original request at BOTTOM
- **Unified timeline** - steps and chat messages interleaved in one scrollable view
- **Chat modifies the plan** - user can add/remove/reorder steps via conversation
- **Human-in-the-loop** - each step has explicit [Run] button, nothing auto-executes

```text
┌─────────────────────────────────────────────────────────────────┐
│ 📋 Add user authentication                              [Close] │
│ Branch: feature/issue-abc123 | Status: Planned                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ ┌─ CHAT INPUT ─────────────────────────────────────────────────┐│
│ │ Add rate limiting to the auth endpoint                 [Send]││
│ └───────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ═══════════════════ TIMELINE (newest first) ════════════════════│
│                                                                  │
│ ┌─ STEP 4 ──────────────────────────────────────────────────────┐│
│ │ 📝 Add rate limiting middleware                               ││
│ │ Capability: coding | Agent: coding-agent                      ││
│ │ Status: ⏳ Pending                    [▶ Run] [Edit] [Remove] ││
│ │ Added via chat: "Add rate limiting to the auth endpoint"     ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ CHAT MESSAGE ────────────────────────────────────────────────┐│
│ │ 🤖 Aura: Added step 4 for rate limiting. Ready when you are. ││
│ │ 🧑 You: Add rate limiting to the auth endpoint               ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ STEP 3 ──────────────────────────────────────────────────────┐│
│ │ 📝 Update API documentation                                   ││
│ │ Capability: documentation | Agent: documentation-agent        ││
│ │ Status: ⏳ Pending                    [▶ Run] [Edit] [Remove] ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ STEP 2 ──────────────────────────────────────────────────────┐│
│ │ 📝 Add unit tests for AuthService                             ││
│ │ Capability: testing | Agent: testing-agent                    ││
│ │ Status: ⏳ Pending                    [▶ Run] [Edit] [Remove] ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ STEP 1 ──────────────────────────────────────────────────────┐│
│ │ 📝 Implement AuthService with JWT                             ││
│ │ Capability: coding | Agent: roslyn-agent                      ││
│ │ Status: ✅ Completed                      [View Output] [Diff] ││
│ │ Output: src/Services/AuthService.cs (142 lines)               ││
│ │ Duration: 12.3s | Tokens: 1,247                               ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ PHASE: PLAN ─────────────────────────────────────────────────┐│
│ │ ✅ Plan created by business-analyst-agent                     ││
│ │ 3 steps generated | Duration: 4.2s                [View Plan] ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ PHASE: DIGEST ───────────────────────────────────────────────┐│
│ │ ✅ Context extracted by issue-digester-agent                  ││
│ │ 5 files indexed | 3 patterns detected          [View Context] ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ┌─ ORIGINAL REQUEST ────────────────────────────────────────────┐│
│ │ As a user, I want to log in with my email and password so    ││
│ │ that I can access my account.                                 ││
│ │ Created: 2025-12-01 10:30 AM                                  ││
│ └────────────────────────────────────────────────────────────────┘│
│                                                                  │
│ ─────────────────────── ⬇️ SCROLL FOR HISTORY ⬇️ ─────────────────│
└─────────────────────────────────────────────────────────────────┘
```

### Chat-Driven Plan Modification

The chat at the top allows natural language interaction:

| User Says | Effect |
|-----------|--------|
| "Add a step for logging" | → New step inserted after current |
| "Remove the documentation step" | → Step removed from plan |
| "Move testing before implementation" | → Steps reordered |
| "What files will this touch?" | → Query answered (no plan change) |
| "Use the roslyn-agent for step 2" | → Agent override applied |
| "Retry step 1 with more detail" | → Step re-executed with feedback |

The chat API (`POST /api/developer/workflows/{id}/chat`) returns:

```json
{
  "response": "I've added a rate limiting step after authentication.",
  "planModified": true,
  "stepsAdded": [{ "order": 4, "name": "Add rate limiting middleware", ... }],
  "stepsRemoved": [],
  "stepsReordered": false
}
```

### Step Actions

| Button | Behavior |
|--------|----------|
| [▶ Run] | Execute step with selected agent, show streaming output |
| [Edit] | Inline edit step name/description, capability |
| [Remove] | Remove step from plan (with confirmation) |
| [View Output] | Expand to show full agent output |
| [Diff] | Open VS Code diff view for changed files |

### Commands

- `Aura: Create Issue` - Quick issue creation
- `Aura: Start Workflow` - Create workflow from issue
- `Aura: Execute Next Step` - Run the next pending step
- `Aura: View Workflow` - Open workflow tab
- `Aura: Open Workflow Chat` - Focus the workflow's chat input

## Implementation Phases

### Phase 1: Data Layer ✅

- [x] Add `Issue` entity
- [x] Add `IssueId` FK to `Workflow`
- [x] Add `RepositoryPath`, `ExecutionPlan`, `CompletedAt` to `Workflow`
- [x] Create migration script (`scripts/apply-developer-migration.sql`)
- [x] Register `DeveloperDbContext`

### Phase 2: Services ✅

- [x] Implement `IIssueService` / `IssueService`
- [x] Implement `IWorkflowService` / `WorkflowService`
- [x] Wire up agent execution via `IAgentRegistry`
- [x] Wire up git worktree creation via `IGitWorktreeService`
- [x] Implement chat-based plan modification

### Phase 3: API Endpoints ✅

- [x] Issue CRUD endpoints (`/api/developer/issues/*`)
- [x] Workflow lifecycle endpoints (`/api/developer/workflows/*`)
- [x] Step execution and management endpoints
- [x] Chat endpoint for plan modification
- [x] Register in `DeveloperModule.ConfigureServices`

### Phase 4: Extension UI

- [ ] Workflows tree view in sidebar
- [ ] Workflow tab with unified chat + steps view
- [ ] Issue creation command
- [ ] Step execution with progress

## Non-Goals (MVP)

- ❌ GitHub/Azure DevOps sync (future)
- ❌ Automatic step execution (user triggers each step)
- ❌ Parallel step execution (sequential for MVP)
- ❌ Step dependencies (linear order for MVP)
- ❌ Multiple workflows per issue (1:1 for MVP)

## Success Criteria

**The silver thread test:**

1. User creates a local issue: "Add a greeting endpoint"
2. User creates workflow → worktree created
3. User digests issue → context extracted
4. User plans → steps created
5. User executes each step → code generated in worktree
6. User completes → branch ready for PR

All local. No external dependencies. Works offline.
