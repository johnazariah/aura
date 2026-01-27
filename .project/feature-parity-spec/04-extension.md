# VS Code Extension Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

The VS Code extension provides the user interface for interacting with Aura. It communicates with the Aura API via HTTP and displays results in tree views, webview panels, and the status bar.

## Package Configuration

### Extension Manifest (`package.json`)

```json
{
  "name": "aura",
  "displayName": "Aura - AI Development Assistant",
  "version": "1.4.0",
  "engines": { "vscode": "^1.85.0" },
  "activationEvents": ["onStartupFinished"],
  "main": "./dist/extension.js",
  "contributes": {
    "viewsContainers": { ... },
    "views": { ... },
    "commands": [ ... ],
    "menus": { ... }
  }
}
```

### MCP Server Definition

Exposes Aura as an MCP server for Copilot:

```json
{
  "mcpServerDefinitionProviders": [
    {
      "id": "aura.context",
      "label": "Aura Codebase Context"
    }
  ]
}
```

---

## 1. View Containers and Views

### 1.1 Activity Bar

Aura adds an activity bar icon with these views:

| View ID | Name | When Visible |
|---------|------|--------------|
| `aura.welcome` | Welcome | `!aura.workspaceOnboarded` |
| `aura.status` | System Status | Always |
| `aura.workflows` | Workflows | `aura.workspaceOnboarded` |
| `aura.agents` | Agents | Always |

### 1.2 Welcome View

Shown when workspace is not onboarded:
- Displays "Welcome to Aura" message
- "Onboard Workspace" button
- Explains what onboarding does

### 1.3 Status View

Tree structure:
```
System Status
├── Health
│   ├── API: ✅ Connected
│   └── Ollama: ✅ Running
├── Models
│   ├── llama3.2
│   └── nomic-embed-text
└── Index
    ├── RAG: 1,234 chunks
    ├── Graph: 567 nodes
    └── Last indexed: 5 min ago
```

### 1.4 Workflow View

Tree structure grouped by status:
```
Workflows
├── 🔄 In Progress (2)
│   ├── Add user authentication
│   └── Fix login bug
├── ⏸️ Waiting (1)
│   └── Add caching layer
└── ✅ Completed (5)
    └── ...
```

### 1.5 Agent View

Tree structure grouped by capability:
```
Agents
├── coding
│   ├── C# Coding Agent
│   ├── Python Coding Agent
│   └── Polyglot Agent
├── analysis
│   └── Business Analyst Agent
└── documentation
    └── Documentation Agent
```

---

## 2. Webview Panels

### 2.1 Workflow Panel

Full-page panel for workflow management:

**Header:**
- Story title and status badge
- Branch name with copy button
- Issue link (if linked)

**Tabs:**
- **Steps** - Step list with status indicators
- **Chat** - Conversation with story context
- **Files** - Modified files in worktree
- **History** - Execution history

**Step Row:**
```
[Status Icon] Step Name                    [Execute] [Approve] [Reject] [Skip]
              Description text...
              Agent: C# Coding Agent
              Output: Created UserService.cs...
```

**Actions:**
- Execute step
- Approve/Reject/Skip step
- Chat with step context
- Edit step description
- Reassign to different agent
- Add new step
- Delete step

### 2.2 Chat Window

Standalone chat panel:
- Agent selector dropdown
- Message history
- Streaming response display
- RAG context indicator
- Token usage display

---

## 3. Commands

### 3.1 General Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.refreshStatus` | Refresh Status | Refresh all tree views |
| `aura.startServices` | Start Aura Services | (Placeholder) |
| `aura.stopServices` | Stop Aura Services | (Placeholder) |

### 3.2 Agent Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.executeAgent` | Execute Agent | Run agent with prompt |
| `aura.quickExecute` | Aura: Execute Agent | Quick pick agent and run |
| `aura.selectAgent` | Select Agent | Show agent actions menu |
| `aura.openChat` | Aura: Open Chat | Open chat window |

### 3.3 RAG Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.indexWorkspace` | Index Workspace | Start background indexing |
| `aura.showRagStats` | Show RAG Stats | Display index statistics |
| `aura.clearRagIndex` | Clear RAG Index | Remove all indexed data |
| `aura.clearAndReindex` | Clear and Reindex | Full reindex |
| `aura.showIndexingProgress` | Show Indexing Progress | Display active jobs |

### 3.4 Workspace Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.onboardWorkspace` | Onboard Workspace | Register and index workspace |

### 3.5 Workflow Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.createWorkflow` | Create Workflow | Create new story |
| `aura.openWorkflow` | Open Workflow | Open workflow panel |
| `aura.executeStep` | Execute Step | Execute a specific step |
| `aura.refreshWorkflows` | Refresh Workflows | Refresh workflow tree |
| `aura.deleteWorkflow` | Delete Workflow | Delete workflow |
| `aura.analyzeWorkflow` | Analyze Workflow | Run analysis agent |
| `aura.planWorkflow` | Plan Workflow | Generate steps |
| `aura.executeAllSteps` | Execute All Steps | Run all steps |
| `aura.completeWorkflow` | Complete Workflow | Mark as complete |
| `aura.cancelWorkflow` | Cancel Workflow | Cancel execution |
| `aura.approveStep` | Approve Step | Approve completed step |
| `aura.rejectStep` | Reject Step | Reject with feedback |
| `aura.skipStep` | Skip Step | Skip step |

### 3.6 Story Commands

| Command | Title | Description |
|---------|-------|-------------|
| `aura.startStoryFromIssue` | Start Story from Issue | Create from GitHub issue |
| `aura.showCurrentStory` | Show Current Story | Open panel for current worktree's story |

---

## 4. Services

### 4.1 AuraApiService

Main API client:

```typescript
export class AuraApiService {
    private readonly baseUrl: string;
    private readonly client: AxiosInstance;
    
    // Health
    async checkHealth(): Promise<HealthResponse>;
    async checkRagHealth(): Promise<RagHealthResponse>;
    
    // Agents
    async getAgents(): Promise<AgentInfo[]>;
    async getAgent(id: string): Promise<AgentInfo>;
    async executeAgent(id: string, prompt: string): Promise<string>;
    
    // RAG
    async indexDirectory(path: string): Promise<BackgroundIndexJob>;
    async queryRag(query: string, limit?: number): Promise<RagQueryResult[]>;
    async getRagStats(): Promise<RagStats>;
    
    // Workspaces
    async onboardWorkspace(path: string): Promise<Workspace>;
    async getWorkspaceByPath(path: string): Promise<Workspace | null>;
    async listWorkspaces(): Promise<Workspace[]>;
    
    // Workflows/Stories
    async createStory(request: CreateStoryRequest): Promise<Story>;
    async getStory(id: string): Promise<Story>;
    async listStories(status?: string, repositoryPath?: string): Promise<StoryList>;
    async analyzeStory(id: string): Promise<Story>;
    async planStory(id: string): Promise<Story>;
    async executeStep(storyId: string, stepId: string): Promise<StoryStep>;
    async approveStep(storyId: string, stepId: string): Promise<StoryStep>;
    async rejectStep(storyId: string, stepId: string, feedback: string): Promise<StoryStep>;
    async completeStory(id: string): Promise<Story>;
    
    // Streaming
    streamStoryExecution(storyId: string, callbacks: StoryStreamCallbacks): void;
    streamChat(agentId: string, messages: ChatMessage[], callbacks: StreamChatCallbacks): void;
}
```

### 4.2 HealthCheckService

Periodic health monitoring:

```typescript
export class HealthCheckService {
    private readonly api: AuraApiService;
    private interval?: NodeJS.Timer;
    
    async checkHealth(): Promise<HealthStatus>;
    startPolling(intervalMs: number): void;
    stopPolling(): void;
    
    onHealthChange(callback: (status: HealthStatus) => void): void;
}
```

### 4.3 GitService

Git operations (uses VS Code's built-in Git extension):

```typescript
export const gitService = {
    async getCurrentBranch(): Promise<string | undefined>;
    async getRepositoryPath(): Promise<string | undefined>;
    async isWorkspaceAGitRepo(): Promise<boolean>;
};
```

### 4.4 LogService

Extension logging:

```typescript
export class LogService {
    private readonly outputChannel: vscode.OutputChannel;
    
    info(message: string): void;
    warn(message: string): void;
    error(message: string, error?: Error): void;
}
```

---

## 5. Tree Providers

### 5.1 StatusTreeProvider

```typescript
export class StatusTreeProvider implements vscode.TreeDataProvider<StatusItem> {
    private _onDidChangeTreeData = new vscode.EventEmitter<StatusItem | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    
    constructor(private healthService: HealthCheckService);
    
    refresh(): void;
    getTreeItem(element: StatusItem): vscode.TreeItem;
    getChildren(element?: StatusItem): Thenable<StatusItem[]>;
}
```

### 5.2 AgentTreeProvider

```typescript
export class AgentTreeProvider implements vscode.TreeDataProvider<AgentItem> {
    constructor(private api: AuraApiService);
    
    // Groups agents by capability
    getChildren(element?: AgentItem): Thenable<AgentItem[]>;
}
```

### 5.3 WorkflowTreeProvider

```typescript
export class WorkflowTreeProvider implements vscode.TreeDataProvider<WorkflowItem> {
    constructor(private api: AuraApiService);
    
    // Groups workflows by status
    // Filters by current workspace repository path
    getChildren(element?: WorkflowItem): Thenable<WorkflowItem[]>;
}
```

### 5.4 WelcomeViewProvider

```typescript
export class WelcomeViewProvider implements vscode.TreeDataProvider<WelcomeItem> {
    constructor(private api: AuraApiService);
    
    // Returns onboarding message and button
    getChildren(): Thenable<WelcomeItem[]>;
}
```

---

## 6. Panel Providers

### 6.1 WorkflowPanelProvider

```typescript
export class WorkflowPanelProvider {
    private panels: Map<string, vscode.WebviewPanel> = new Map();
    
    constructor(
        private extensionUri: vscode.Uri,
        private api: AuraApiService
    );
    
    async openWorkflowPanel(workflowId: string): Promise<void>;
    
    // Webview receives messages:
    // - executeStep { stepId }
    // - approveStep { stepId }
    // - rejectStep { stepId, feedback }
    // - skipStep { stepId, reason }
    // - chatWithStep { stepId, message }
    // - addStep { name, capability, description }
    // - deleteStep { stepId }
}
```

### 6.2 ChatWindowProvider

```typescript
export class ChatWindowProvider {
    constructor(
        private extensionUri: vscode.Uri,
        private api: AuraApiService
    );
    
    async openChatWindow(agent: AgentInfo): Promise<void>;
    
    // Webview receives messages:
    // - sendMessage { content }
    // - selectContextMode { mode: 'none' | 'rag' | 'graph' | 'both' }
}
```

---

## 7. Status Bar

Single status bar item showing:
- Aura health status (icon)
- Current story (if in worktree)
- Click opens current story panel

States:
- `$(check)` - Healthy
- `$(warning)` - Degraded
- `$(error)` - Disconnected
- `$(sync~spin)` - Indexing

---

## 8. Context Values

Set via `setContext` command:

| Context | Description |
|---------|-------------|
| `aura.workspaceOnboarded` | True if workspace is registered |
| `aura.workspaceNotOnboarded` | Inverse of above |
| `aura.hasActiveStory` | True if current worktree has a story |

---

## 9. Menus

### 9.1 View Title Menus

```json
"view/title": [
  { "command": "aura.refreshStatus", "when": "view == aura.status" },
  { "command": "aura.createWorkflow", "when": "view == aura.workflows" },
  { "command": "aura.refreshWorkflows", "when": "view == aura.workflows" }
]
```

### 9.2 View Item Context Menus

```json
"view/item/context": [
  { "command": "aura.openWorkflow", "when": "viewItem == workflow" },
  { "command": "aura.deleteWorkflow", "when": "viewItem == workflow" },
  { "command": "aura.selectAgent", "when": "viewItem == agent" }
]
```

---

## 10. Activation

### 10.1 Activation Flow

```typescript
export async function activate(context: vscode.ExtensionContext) {
    // 1. Set default context values
    await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
    
    // 2. Initialize services
    auraApiService = new AuraApiService();
    healthCheckService = new HealthCheckService(auraApiService);
    
    // 3. Initialize providers
    statusTreeProvider = new StatusTreeProvider(healthCheckService);
    workflowTreeProvider = new WorkflowTreeProvider(auraApiService);
    // ... etc
    
    // 4. Register views
    vscode.window.createTreeView('aura.status', { treeDataProvider: statusTreeProvider });
    // ... etc
    
    // 5. Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('aura.refreshStatus', refreshAll),
        // ... etc
    );
    
    // 6. Start health polling
    healthCheckService.startPolling(10000);
    
    // 7. Check workspace onboarding status
    await checkWorkspaceOnboarding();
}
```

### 10.2 Deactivation

```typescript
export function deactivate() {
    healthCheckService?.stopPolling();
    logService?.dispose();
}
```

---

## 11. Webview Communication

### 11.1 Message Protocol

Extension → Webview:
```typescript
panel.webview.postMessage({
    type: 'storyUpdate',
    story: { ... }
});
```

Webview → Extension:
```typescript
vscode.postMessage({
    type: 'executeStep',
    stepId: '...'
});
```

### 11.2 Content Security Policy

```html
<meta http-equiv="Content-Security-Policy" content="
    default-src 'none';
    style-src ${webview.cspSource} 'unsafe-inline';
    script-src ${webview.cspSource};
    img-src ${webview.cspSource} https:;
">
```

---

## 12. Path Normalization

All paths sent to API are normalized:

```typescript
function normalizePath(path: string): string {
    return path
        .replace(/\\\\/g, '/')  // Escaped backslashes
        .replace(/\\/g, '/')     // Backslashes
        .toLowerCase();
}
```

This matches the C# `PathNormalizer` for consistent path handling.
