# Assisted Workflow UI

**Status**: Active Development  
**Created**: 2025-12-05  
**Author**: AI-assisted specification

## Overview

Transform the workflow UI from a simple "execute and wait" model to an **assisted collaboration** where users and agents work together step by step.

## Design Principles

1. **Compact by default** - Steps are collapsed, expand on demand
2. **Human in control** - Skip, reorder, reassign, approve/reject
3. **Conversational refinement** - Chat with agent before/after execution
4. **Transparency** - See what context the agent used

## UI Components

### Step Card States

#### Pending (Collapsed)

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ○ 2. Define README Structure                    [documentation-agent]│
│   Propose outline based on analysis                                  │
│                                                     [💬] [▶] [⋮]    │
└──────────────────────────────────────────────────────────────────────┘
```

#### Running

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ◐ 3. Draft Overview Section                     [documentation-agent]│
│   Write the overview and installation sections                       │
│                                                 [streaming... ━━━━━] │
└──────────────────────────────────────────────────────────────────────┘
```

#### Completed (Expandable)

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ● 2. Define README Structure                    [documentation-agent]│
│   Propose outline based on analysis                                  │
│                                                     [💬] [👁] [⋮]    │
└──────────────────────────────────────────────────────────────────────┘
```

#### Expanded with Output

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ● 2. Define README Structure                    [documentation-agent]│
│   Propose outline based on analysis                                  │
├──────────────────────────────────────────────────────────────────────┤
│ ▼ Output                                               [✓ Approve]  │
│ ┌────────────────────────────────────────────────────────────────┐  │
│ │ ## Proposed README Structure                                    │  │
│ │ 1. Overview - Project purpose and components                    │  │
│ │ 2. Installation - Prerequisites and build                       │  │
│ │ 3. Quick Start - Basic usage examples                          │  │
│ │ 4. API Reference - Key classes and methods                     │  │
│ │ 5. Best Practices - Usage recommendations                      │  │
│ │ 6. Troubleshooting - Common issues                             │  │
│ └────────────────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────────────┤
│ ▶ Chat with agent                                                   │
└──────────────────────────────────────────────────────────────────────┘
```

#### Expanded with Chat

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ○ 4. Draft Quick Start                          [documentation-agent]│
│   Write usage examples section                                       │
├──────────────────────────────────────────────────────────────────────┤
│ ▼ Chat                                                              │
│ ┌────────────────────────────────────────────────────────────────┐  │
│ │ You: Focus on the ActionBuilder pattern, that's the key API    │  │
│ │                                                                 │  │
│ │ Agent: Understood. I'll center the Quick Start around          │  │
│ │ ActionBuilder with examples showing:                            │  │
│ │ - Creating a builder instance                                   │  │
│ │ - Configuring actions                                           │  │
│ │ - Executing the built action                                    │  │
│ │ Ready to execute with this focus?                               │  │
│ ├────────────────────────────────────────────────────────────────┤  │
│ │ [Type a message...                            ] [Send] [▶ Run] │  │
│ └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### Status Indicators

| Icon | State | Meaning |
|------|-------|---------|
| ○ | Pending | Ready to execute (if dependencies met) |
| ◐ | Running | Currently executing |
| ● | Completed | Finished successfully |
| ✗ | Failed | Execution failed |
| ⊘ | Skipped | User skipped this step |
| ◑ | Blocked | Dependencies not met (Execute disabled) |

### Action Buttons

| Button | State | Action |
|--------|-------|--------|
| 💬 | All | Toggle chat panel |
| ▶ | Pending | Execute step |
| 👁 | Completed | Toggle output panel |
| ✓ | Completed | Approve output |
| ⋮ | All | More options menu |

### More Options Menu (⋮)

- **Skip step** - Mark as skipped, proceed to next
- **Reassign agent** - Change which agent handles this step
- **Edit description** - Modify the step instructions
- **View context** - See RAG context used
- **Retry** - Re-execute (for failed/completed steps)

## API Changes

### New Endpoints

#### Step Chat

```json
POST /api/developer/workflows/{workflowId}/steps/{stepId}/chat
Content-Type: application/json

{
  "message": "Focus on ActionBuilder pattern"
}

Response:
{
  "stepId": "...",
  "response": "Understood. I'll center the Quick Start around...",
  "updatedDescription": "Write usage examples focusing on ActionBuilder pattern"
}
```

#### Skip Step

```json
POST /api/developer/workflows/{workflowId}/steps/{stepId}/skip
Content-Type: application/json

{
  "reason": "Not needed for this README"  // optional
}

Response:
{
  "stepId": "...",
  "status": "Skipped"
}
```

#### Reassign Step

```json
POST /api/developer/workflows/{workflowId}/steps/{stepId}/reassign
Content-Type: application/json

{
  "agentId": "coding-agent"
}

Response:
{
  "stepId": "...",
  "assignedAgentId": "coding-agent"
}
```

#### Approve/Reject Output

```json
POST /api/developer/workflows/{workflowId}/steps/{stepId}/approve
POST /api/developer/workflows/{workflowId}/steps/{stepId}/reject

{
  "feedback": "Add more detail about error handling"  // for reject
}
```

### Modified Endpoints

#### Get Workflow (add more step details)

```json
GET /api/developer/workflows/{workflowId}

Response includes for each step:
{
  "id": "...",
  "name": "Define README Structure",
  "description": "Propose outline based on analysis",
  "capability": "documentation",
  "assignedAgentId": "documentation-agent",
  "status": "Completed",
  "order": 2,
  "canExecute": true,  // NEW: false if dependencies not met
  "output": "...",
  "chatHistory": [     // NEW: conversation with agent
    { "role": "user", "content": "..." },
    { "role": "agent", "content": "..." }
  ],
  "approval": null     // NEW: "approved" | "rejected" | null
}
```

## Data Model Changes

### WorkflowStep Entity

Add fields:

```csharp
public class WorkflowStep
{
    // Existing...
    
    // New fields
    public string? ChatHistory { get; set; }  // JSON array of messages
    public StepApproval? Approval { get; set; }  // Approved, Rejected, null
    public string? ApprovalFeedback { get; set; }
    public string? SkipReason { get; set; }
}

public enum StepApproval
{
    Approved,
    Rejected
}
```

## Implementation Phases

### Phase 1: Step Card Redesign

- [ ] Update webview HTML with new card layout
- [ ] Add collapsible sections (Output, Chat)
- [ ] Status indicators and action buttons
- [ ] CSS styling for compact/expanded states

### Phase 2: Execute Gating

- [ ] Add `canExecute` logic based on step order/dependencies
- [ ] Disable Execute button when not ready
- [ ] Show blocked indicator

### Phase 3: Output Approval

- [ ] Add Approve/Reject buttons to output section
- [ ] Create API endpoints
- [ ] Store approval state
- [ ] Visual feedback for approved/rejected

### Phase 4: Step Chat

- [ ] Add chat panel to step card
- [ ] Create chat API endpoint
- [ ] Store chat history
- [ ] Agent responds in context of step

### Phase 5: Step Management

- [ ] Skip step functionality
- [ ] Reassign agent dropdown
- [ ] Edit description inline
- [ ] View context panel

## File Changes

### Extension (VS Code)

```text
extension/
├── src/
│   ├── panels/
│   │   └── WorkflowPanel.ts      # Update webview generation
│   ├── services/
│   │   └── AuraService.ts        # Add new API calls
│   └── extension.ts
├── media/
│   ├── workflow.css              # New styling
│   └── workflow.js               # Client-side interactivity
```

### API

```text
src/Aura.Api/
└── Program.cs                    # Add new endpoints

src/Aura.Module.Developer/
├── Data/
│   └── Entities/
│       └── WorkflowStep.cs       # Add new fields
├── Services/
│   ├── IWorkflowService.cs       # Add new methods
│   └── WorkflowService.cs        # Implement new methods
```

## Success Criteria

1. Steps display in compact card format by default
2. Clicking a step expands to show output/chat
3. Execute button disabled for steps with unmet dependencies
4. User can chat with agent before executing a step
5. User can approve/reject step output
6. User can skip or reassign steps
7. All state persisted and visible on refresh

## Open Questions

1. Should chat history persist across sessions?
2. How to handle reassignment mid-execution?
3. Should approval be required before proceeding to next step?
4. How to visualize step dependencies (if non-linear)?
