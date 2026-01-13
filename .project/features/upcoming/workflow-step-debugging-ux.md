# Workflow Step Debugging & Review UX

**Status:** � In Progress  
**Priority:** High  
**Effort:** Medium (3-5 days)  
**Started:** 2025-01-14

## Implementation Progress

- [x] **US-1**: Step metadata with agent ID, tokens, timing, timestamps
- [x] **US-2**: Tool steps (ReAct trace) with thought/action/observation
- [x] **US-4**: Error styling with reset/retry buttons, previous output toggle
- [x] **US-5**: Reset & Retry buttons on failed/error steps
- [x] Copy buttons for step output
- [ ] **US-3**: Artifacts tab, diff view, open in editor
- [ ] **US-6**: Step-scoped chat
- [ ] **US-7**: Approve/reject persisted state
- [ ] **US-8**: Worktree changes view

## Problem Statement

When workflow steps fail or produce unexpected results, debugging requires:
1. Running curl commands to inspect step output
2. Parsing JSON manually to find errors
3. Using terminal to reset/retry steps
4. No visibility into agent tool calls or reasoning

This friction makes it hard to understand what went wrong and iterate on fixes.

## User Stories

### US-1: View Step Execution Details
**As a** developer reviewing a workflow  
**I want to** see detailed execution information for each step  
**So that** I can understand what the agent did and why

**Acceptance Criteria:**
- [ ] Click on a step to expand a details panel
- [ ] See agent ID and execution duration
- [ ] See token usage for the step
- [ ] See step description and capability type
- [ ] Timestamp for started/completed

### US-2: View Agent Tool Steps (ReAct Trace)
**As a** developer debugging a failed step  
**I want to** see the sequence of tool calls the agent made  
**So that** I can understand its reasoning and identify where it went wrong

**Acceptance Criteria:**
- [ ] Expandable "Tool Steps" section in step details
- [ ] For each tool step, show:
  - Thought (agent's reasoning)
  - Action (tool ID)
  - Action Input (parameters)
  - Observation (tool output, truncated with expand option)
- [ ] Visual indicator for failed tool calls
- [ ] Copy button for tool inputs/outputs

### US-3: View Step Output & Artifacts
**As a** developer reviewing completed work  
**I want to** see the agent's final output and any artifacts  
**So that** I can verify the work before approving

**Acceptance Criteria:**
- [ ] "Output" tab showing formatted agent response
- [ ] "Artifacts" tab showing files created/modified
- [ ] For code artifacts: syntax-highlighted preview
- [ ] "Open in Editor" action for file artifacts
- [ ] Diff view for modified files

### US-4: View Step Errors
**As a** developer debugging a failed step  
**I want to** see clear error information  
**So that** I can understand what went wrong

**Acceptance Criteria:**
- [ ] Red error banner on failed steps
- [ ] Error message prominently displayed
- [ ] Stack trace available (expandable)
- [ ] Previous attempt output shown if step was retried
- [ ] Link to relevant logs

### US-5: Reset & Retry Steps
**As a** developer fixing a failed step  
**I want to** reset it and try again  
**So that** I can iterate without recreating the workflow

**Acceptance Criteria:**
- [ ] "Reset" button on failed/completed steps
- [ ] Confirmation dialog explaining what reset does
- [ ] Option to edit step description before retry
- [ ] Option to reassign to different agent
- [ ] "Retry" button (reset + immediate execute)

### US-6: Step Chat for Guidance
**As a** developer wanting to guide an agent  
**I want to** chat with the agent before/after step execution  
**So that** I can provide additional context or ask clarifying questions

**Acceptance Criteria:**
- [ ] "Chat" button on each step
- [ ] Opens chat panel scoped to that step
- [ ] Chat history persisted with step
- [ ] Agent has access to step context and previous output
- [ ] Can provide guidance before execution

### US-7: Approve/Reject Workflow
**As a** developer reviewing completed work  
**I want to** approve or reject each step's output  
**So that** I can ensure quality before finalizing

**Acceptance Criteria:**
- [ ] "Approve" / "Request Changes" buttons on completed steps
- [ ] Approval state visible in step list
- [ ] "Request Changes" opens dialog for feedback
- [ ] Feedback passed to agent on retry
- [ ] Workflow can't finalize with unapproved steps (optional setting)

### US-8: View Worktree Changes
**As a** developer reviewing a workflow  
**I want to** see what files changed in the worktree  
**So that** I can review the overall impact

**Acceptance Criteria:**
- [ ] "Changes" tab in workflow view
- [ ] List of modified/added/deleted files
- [ ] Click to open diff in editor
- [ ] "Open Worktree in Explorer" action
- [ ] Git status (staged/unstaged) shown

## UI Mockup

```
┌─────────────────────────────────────────────────────────────────┐
│ Workflow: Implement TODO: Get remote URL                        │
│ Status: In Progress  Branch: workflow/implement-todo-...       │
├─────────────────────────────────────────────────────────────────┤
│ Steps                                          │ Details        │
│ ┌─────────────────────────────────────────────┐│                │
│ │ ✅ 1. Analyze existing code         00:06   ││ Step 2         │
│ │ ❌ 2. Implement GetRemoteUrlAsync   FAILED  ││ ───────────    │
│ │ ⏳ 3. Write unit tests              Pending ││ Status: Failed │
│ │ ⏳ 4. Validate with integration     Pending ││ Agent: coding  │
│ │ ⏳ 5. Refactor and finalize         Pending ││ Duration: 45s  │
│ └─────────────────────────────────────────────┘│ Tokens: 12,450 │
│                                                │                │
│                                                │ ┌─Error───────┐│
│                                                │ │A task was   ││
│                                                │ │canceled.    ││
│                                                │ └─────────────┘│
│                                                │                │
│                                                │ [Reset][Retry] │
│                                                │                │
│                                                │ ▼ Tool Steps   │
│                                                │ ┌─────────────┐│
│                                                │ │1. file.read ││
│                                                │ │  → Success  ││
│                                                │ │2. file.write││
│                                                │ │  → Timeout  ││
│                                                │ └─────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

## Technical Design

### API Requirements

Current API already supports most operations:
- `GET /workflows/{id}` - includes step details and output
- `POST /steps/{id}/reset` - reset step to pending
- `POST /steps/{id}/execute` - execute step
- `POST /steps/{id}/approve` - approve step
- `POST /steps/{id}/reject` - reject with feedback
- `POST /steps/{id}/chat` - chat with agent

**New/Enhanced endpoints needed:**
- Step output already includes `toolSteps` array - just need to parse and display
- May want `GET /workflows/{id}/changes` for file diff summary

### Extension Changes

1. **WorkflowStepDetailView** - New webview panel for step details
2. **StepOutputParser** - Parse agent output JSON to extract tool steps
3. **WorkflowTreeProvider** - Enhanced to show step status icons
4. **WorkflowActionsProvider** - Context menu actions (reset, retry, chat)

### Data Model

Step output JSON structure (already exists):
```json
{
  "agentId": "coding-agent",
  "content": "Final response text",
  "artifacts": { "filePath": "content" },
  "toolSteps": [
    {
      "action": "file.read",
      "thought": "I need to read the file...",
      "actionInput": "{\"path\": \"...\"}",
      "observation": "File contents..."
    }
  ],
  "durationMs": 6296,
  "tokensUsed": 4941
}
```

## Success Metrics

- Reduce time to diagnose failed steps by 80%
- Enable step retry without recreating workflow
- Provide visibility into agent reasoning

## Dependencies

- None (uses existing API)

## Out of Scope

- Real-time streaming of step execution (separate feature)
- Editing step code directly in extension
- Multi-step undo/rollback

## Open Questions

1. Should step approval be required before finalize? (Configurable?)
2. How to handle very long tool outputs? (Truncate + "Show More"?)
3. Should we show cost estimates per step? (Token → $ conversion)
