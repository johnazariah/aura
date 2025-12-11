# VS Code Extension

**Status:** ✅ Complete  
**Completed:** 2025-11-29  
**Last Updated:** 2025-12-12

## Overview

The VS Code extension provides the developer interface for Aura. It's a control surface for managing workflows, interacting with agents, and reviewing generated code—not an autonomous system.

## Design Principles

1. **User is in control** - Every action is explicit
2. **Transparency** - Show what agents are doing
3. **Direct manipulation** - Click to execute, drag to reorder
4. **Minimal state** - Extension is stateless, API is source of truth

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    VS Code Extension                         │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                    Extension Host                    │    │
│  │                                                      │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │    │
│  │  │ TreeViews   │  │ WebViews    │  │ Commands    │ │    │
│  │  │             │  │             │  │             │ │    │
│  │  │ - Issues    │  │ - Workflow  │  │ - Execute   │ │    │
│  │  │ - Agents    │  │   Detail    │  │ - ENRICH    │ │    │
│  │  │ - Workflows │  │ - Chat      │  │ - Plan      │ │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘ │    │
│  │                         │                           │    │
│  │                         ▼                           │    │
│  │  ┌─────────────────────────────────────────────┐   │    │
│  │  │              AuraService                     │   │    │
│  │  │                                              │   │    │
│  │  │  - HTTP client to Aura API                  │   │    │
│  │  │  - SSE subscription for updates             │   │    │
│  │  │  - Typed request/response                   │   │    │
│  │  └─────────────────────────────────────────────┘   │    │
│  │                         │                           │    │
│  └─────────────────────────┼───────────────────────────┘    │
│                            │                                 │
│                            ▼                                 │
│                    ┌───────────────┐                        │
│                    │   Aura API    │                        │
│                    │  :5258        │                        │
│                    └───────────────┘                        │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Views

### 1. Issues Panel (TreeView)

Shows issues from connected providers.

```
ISSUES
├── github:owner/repo
│   ├── #123 Add user authentication
│   ├── #124 Fix login bug
│   └── #125 Update documentation
└── ado:org/project
    └── #456 Implement feature X
```

**Actions:**
- Refresh (sync with provider)
- Import as Workflow (right-click)
- View in browser (click)

### 2. Agents Panel (TreeView)

Shows registered agents sorted by priority (specialists first).

```
AGENTS
├── ✓ Roslyn Agent (csharp-coding, validation) [30]
├── ✓ Python Agent (python-coding) [40]
├── ✓ Coding Agent (coding) [60]
├── ✓ Testing Agent (testing) [50]
├── ✓ Chat Agent (chat, general) [80]          ← Default fallback
└── 📁 agents/ folder
```

**Badge meanings:**
- `[30]` = Priority (lower = more specialized)
- Capabilities shown in parentheses

**Actions:**
- View details (click) → Shows capabilities, provider, model, description
- Open in editor (right-click on markdown agents)
- Refresh (reload from agents/ folder)

**Agent Details Panel:**

```
┌─────────────────────────────────────────────────────────────┐
│ Roslyn Agent                                         [Close]│
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ Priority: 30 (Specialist)                                    │
│ Capabilities: csharp-coding, csharp-validation, refactoring │
│ Provider: ollama                                             │
│ Model: qwen2.5-coder:7b                                      │
│                                                              │
│ Description:                                                 │
│ Generates C# code with Roslyn-based compilation and         │
│ validation. Iterates until code compiles successfully.       │
│                                                              │
│ Source: Coded (ships with Aura)                              │
│                                                              │
│ [Test Agent] [View Source]                                   │
└─────────────────────────────────────────────────────────────┘
```

### 3. Workflows Panel (TreeView)

Shows all workflows with status.

```
WORKFLOWS
├── 🔵 WF-001: Add authentication [Planned]
├── 🟡 WF-002: Fix login bug [Executing]
├── ✓ WF-003: Update docs [Completed]
└── ❌ WF-004: Refactor API [Failed]
```

**Actions:**
- Open detail view (click)
- Delete workflow (right-click)
- Filter by status

## Workflow Detail View (WebView)

The main interaction surface. Shows workflow phases and steps.

```
┌─────────────────────────────────────────────────────────────┐
│ Workflow: Add user authentication                      [X]  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ PHASE 3: EXECUTE                                             │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Step 1: Implement AuthService     [Roslyn ▼] [▶ Run]    │ │
│ │ Status: ✓ Completed                                      │ │
│ │ Output: src/Services/AuthService.cs (142 lines)         │ │
│ │ [View Code] [View Diff] [Retry]                         │ │
│ ├─────────────────────────────────────────────────────────┤ │
│ │ Step 2: Write AuthService tests   [Testing ▼] [▶ Run]   │ │
│ │ Status: Pending                                          │ │
│ ├─────────────────────────────────────────────────────────┤ │
│ │ Step 3: Update documentation      [Docs ▼] [▶ Run]      │ │
│ │ Status: Pending                                          │ │
│ └─────────────────────────────────────────────────────────┘ │
│ [+ Add Step] [Run All] [Re-Plan]                            │
│                                                              │
│ PHASE 2: PLAN                                                │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ✓ Plan created with 3 steps                             │ │
│ │ [View Plan] [Edit Plan]                                  │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ PHASE 1: ENRICH                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ✓ Context extracted                                      │ │
│ │ Relevant files: 5 | Patterns detected: 3                │ │
│ │ [View Context]                                           │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ORIGINAL REQUEST                                             │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ As a user, I want to log in with my email and password  │ │
│ │ so that I can access my account.                        │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Step Interactions

| Action | Behavior |
|--------|----------|
| Agent dropdown | Select which agent runs this step |
| Run button | Execute step with selected agent |
| View Code | Open generated file in editor |
| View Diff | Show git diff in editor |
| Retry | Re-run step with optional feedback |
| Run All | Execute all pending steps sequentially |
| Re-Plan | Request new plan with feedback |

## Chat Panel (WebView)

Augmented development chat within workflow context.

```
┌─────────────────────────────────────────────────────────────┐
│ Chat: Add user authentication                               │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ You: Add rate limiting to the AuthService               │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Aura: I'll add rate limiting. I've updated the plan:   │ │
│ │                                                         │ │
│ │ + Step 4: Add rate limiting middleware                 │ │
│ │                                                         │ │
│ │ The new step will use the csharp-coding capability.    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Type a message...                              [Send]   │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## AuraService (TypeScript)

Thin client for the Aura API.

```typescript
export class AuraService {
  private baseUrl = 'http://localhost:5258/api';
  private eventSource?: EventSource;
  
  // Agents
  async getAgents(): Promise<Agent[]>;
  async getAgentsByCapability(capability: string): Promise<Agent[]>;
  async registerAgent(agent: AgentDefinition): Promise<Agent>;
  
  // Workflows
  async getWorkflows(): Promise<WorkflowSummary[]>;
  async getWorkflow(id: string): Promise<Workflow>;
  async createWorkflow(request: CreateWorkflowRequest): Promise<Workflow>;
  async deleteWorkflow(id: string): Promise<void>;
  
  // Phases
  async EnrichWorkflow(id: string): Promise<EnrichResult>;
  async planWorkflow(id: string): Promise<PlanResult>;
  async replanWorkflow(id: string, feedback: string): Promise<PlanResult>;
  
  // Steps
  async executeStep(workflowId: string, stepId: string, agentId?: string): Promise<StepResult>;
  async retryStep(workflowId: string, stepId: string, feedback?: string): Promise<StepResult>;
  async skipStep(workflowId: string, stepId: string): Promise<void>;
  
  // Chat
  async sendMessage(workflowId: string, message: string): Promise<ChatResponse>;
  async getChatHistory(workflowId: string): Promise<ChatMessage[]>;
  
  // Real-time
  subscribeToWorkflow(id: string, callback: (event: WorkflowEvent) => void): () => void {
    this.eventSource = new EventSource(`${this.baseUrl}/workflows/${id}/events`);
    this.eventSource.onmessage = (e) => callback(JSON.parse(e.data));
    return () => this.eventSource?.close();
  }
}
```

## Commands

| Command | Keybinding | Description |
|---------|------------|-------------|
| `aura.refreshIssues` | - | Sync issues from providers |
| `aura.importIssue` | - | Import issue as workflow |
| `aura.openWorkflow` | - | Open workflow detail view |
| `aura.executeStep` | - | Execute current step |
| `aura.openChat` | `Ctrl+Shift+A` | Open chat panel |

## Configuration

```json
{
  "aura.apiUrl": "http://localhost:5258",
  "aura.autoRefresh": true,
  "aura.refreshInterval": 30000,
  "aura.showNotifications": true
}
```

## State Management

**The extension is stateless.** All state lives in the API.

- TreeViews fetch on activation and on refresh
- WebViews fetch on open and subscribe to SSE
- No local caching (simplicity over performance)

## Error Handling

| Error | User Experience |
|-------|-----------------|
| API unreachable | Show "Aura not running" with retry |
| Step failed | Show error in step, enable retry |
| Agent unavailable | Disable agent in dropdown |

## What We Remove

From current extension:
- Complex polling logic
- Orchestration state tracking
- Multiple service classes
- Status bar complexity
- Auto-execution features

## File Structure

```
extension/
├── src/
│   ├── extension.ts           # Activation, command registration
│   ├── auraService.ts         # API client
│   ├── views/
│   │   ├── issuesTreeProvider.ts
│   │   ├── agentsTreeProvider.ts
│   │   ├── workflowsTreeProvider.ts
│   │   └── workflowDetailPanel.ts
│   └── types.ts               # TypeScript interfaces
├── webview/
│   ├── workflow.html
│   ├── workflow.css
│   └── workflow.js
├── package.json
└── tsconfig.json
```

## Open Questions

1. **Inline diff** - Show diff in webview or open VS Code diff editor?
2. **Multi-workflow** - Allow multiple workflow panels open?
3. **Offline mode** - Graceful degradation when API down?
4. **Theming** - Match VS Code theme in webviews?
