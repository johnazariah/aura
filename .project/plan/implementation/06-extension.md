# Phase 6: VS Code Extension Update

**Duration:** 3-4 hours  
**Dependencies:** Phase 4 (API Endpoints)  
**Output:** Updated extension for new API

## Objective

Update the VS Code extension to work with the new simplified API, focusing on the 3-phase workflow model.

## Approach

**Incremental update, not rewrite.** Keep working UI, replace service layer.

## Tasks

### 6.1 Create New AuraService

Replace the complex service classes with a single, simple API client.

**src/auraService.ts:**
```typescript
import * as vscode from 'vscode';

export interface Agent {
  id: string;
  name: string;
  capabilities: string[];
  priority: number;
  provider: string;
  model: string;
  description?: string;
}

export interface WorkflowSummary {
  id: string;
  workItemId: string;
  workItemTitle: string;
  status: string;
  stepCount: number;
  createdAt: string;
}

export interface Workflow {
  id: string;
  workItemId: string;
  workItemTitle: string;
  workItemDescription?: string;
  status: string;
  workspacePath?: string;
  gitBranch?: string;
  digestedContext?: string;
  steps: WorkflowStep[];
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowStep {
  id: string;
  order: number;
  name: string;
  capability: string;
  description?: string;
  status: string;
  assignedAgentId?: string;
}

export interface StepResult {
  stepId: string;
  status: string;
  agentId: string;
  output?: string;
  error?: string;
  durationMs: number;
}

export class AuraService {
  private baseUrl: string;
  private eventSource?: EventSource;

  constructor() {
    this.baseUrl = vscode.workspace.getConfiguration('aura').get('apiUrl', 'http://localhost:5258');
  }

  // ==================== Agents ====================

  async getAgents(): Promise<Agent[]> {
    const response = await this.fetch('/api/agents');
    return response.agents;
  }

  async getAgentsByCapability(capability: string): Promise<Agent[]> {
    const response = await this.fetch(`/api/agents?capability=${encodeURIComponent(capability)}`);
    return response.agents;
  }

  // ==================== Workflows ====================

  async getWorkflows(status?: string): Promise<WorkflowSummary[]> {
    const url = status ? `/api/workflows?status=${status}` : '/api/workflows';
    const response = await this.fetch(url);
    return response.workflows;
  }

  async getWorkflow(id: string): Promise<Workflow> {
    return await this.fetch(`/api/workflows/${id}`);
  }

  async createWorkflow(request: {
    workItemId: string;
    workItemTitle: string;
    workItemDescription?: string;
    workspacePath?: string;
  }): Promise<Workflow> {
    return await this.fetch('/api/workflows', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async deleteWorkflow(id: string): Promise<void> {
    await this.fetch(`/api/workflows/${id}`, { method: 'DELETE' });
  }

  // ==================== Phases ====================

  async digestWorkflow(id: string): Promise<{ status: string; context?: string }> {
    return await this.fetch(`/api/workflows/${id}/digest`, { method: 'POST' });
  }

  async planWorkflow(id: string): Promise<{ status: string; steps: WorkflowStep[] }> {
    return await this.fetch(`/api/workflows/${id}/plan`, { method: 'POST' });
  }

  async replanWorkflow(id: string, feedback: string): Promise<{ status: string; steps: WorkflowStep[] }> {
    return await this.fetch(`/api/workflows/${id}/replan`, {
      method: 'POST',
      body: JSON.stringify({ feedback }),
    });
  }

  // ==================== Steps ====================

  async executeStep(workflowId: string, stepId: string, agentId?: string): Promise<StepResult> {
    return await this.fetch(`/api/workflows/${workflowId}/steps/${stepId}/execute`, {
      method: 'POST',
      body: agentId ? JSON.stringify({ agentId }) : undefined,
    });
  }

  async retryStep(workflowId: string, stepId: string, feedback?: string): Promise<StepResult> {
    return await this.fetch(`/api/workflows/${workflowId}/steps/${stepId}/retry`, {
      method: 'POST',
      body: feedback ? JSON.stringify({ feedback }) : undefined,
    });
  }

  async skipStep(workflowId: string, stepId: string): Promise<void> {
    await this.fetch(`/api/workflows/${workflowId}/steps/${stepId}/skip`, { method: 'POST' });
  }

  // ==================== Chat ====================

  async sendChatMessage(workflowId: string, message: string): Promise<{
    response: string;
    planUpdated?: boolean;
    newSteps?: WorkflowStep[];
  }> {
    return await this.fetch(`/api/workflows/${workflowId}/chat`, {
      method: 'POST',
      body: JSON.stringify({ message }),
    });
  }

  // ==================== Events ====================

  subscribeToWorkflow(id: string, callback: (event: any) => void): () => void {
    this.eventSource = new EventSource(`${this.baseUrl}/api/workflows/${id}/events`);
    
    this.eventSource.onmessage = (e) => {
      try {
        callback(JSON.parse(e.data));
      } catch (err) {
        console.error('Failed to parse SSE event', err);
      }
    };
    
    this.eventSource.onerror = (e) => {
      console.error('SSE error', e);
    };
    
    return () => {
      this.eventSource?.close();
      this.eventSource = undefined;
    };
  }

  // ==================== Health ====================

  async isHealthy(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/health`);
      return response.ok;
    } catch {
      return false;
    }
  }

  // ==================== Internal ====================

  private async fetch(path: string, options: RequestInit = {}): Promise<any> {
    const url = `${this.baseUrl}${path}`;
    
    const response = await fetch(url, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });
    
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: response.statusText }));
      throw new Error(error.message || error.error?.message || 'API request failed');
    }
    
    if (response.status === 204) {
      return undefined;
    }
    
    return await response.json();
  }
}

// Singleton instance
let auraService: AuraService | undefined;

export function getAuraService(): AuraService {
  if (!auraService) {
    auraService = new AuraService();
  }
  return auraService;
}
```

### 6.2 Update Tree Providers

Simplify tree providers to use new service.

**src/views/workflowsTreeProvider.ts:**
```typescript
import * as vscode from 'vscode';
import { getAuraService, WorkflowSummary } from '../auraService';

export class WorkflowsTreeProvider implements vscode.TreeDataProvider<WorkflowItem> {
  private _onDidChangeTreeData = new vscode.EventEmitter<WorkflowItem | undefined>();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  refresh(): void {
    this._onDidChangeTreeData.fire(undefined);
  }

  getTreeItem(element: WorkflowItem): vscode.TreeItem {
    return element;
  }

  async getChildren(element?: WorkflowItem): Promise<WorkflowItem[]> {
    if (element) {
      return []; // No nested items for now
    }

    try {
      const service = getAuraService();
      const workflows = await service.getWorkflows();
      
      return workflows.map(w => new WorkflowItem(w));
    } catch (err) {
      vscode.window.showErrorMessage(`Failed to load workflows: ${err}`);
      return [];
    }
  }
}

class WorkflowItem extends vscode.TreeItem {
  constructor(public readonly workflow: WorkflowSummary) {
    super(workflow.workItemTitle, vscode.TreeItemCollapsibleState.None);
    
    this.description = workflow.status;
    this.tooltip = `${workflow.workItemId}\n${workflow.status}\n${workflow.stepCount} steps`;
    this.contextValue = 'workflow';
    
    // Status icon
    this.iconPath = this.getStatusIcon(workflow.status);
    
    // Click to open
    this.command = {
      command: 'aura.openWorkflow',
      title: 'Open Workflow',
      arguments: [workflow.id],
    };
  }

  private getStatusIcon(status: string): vscode.ThemeIcon {
    switch (status.toLowerCase()) {
      case 'completed': return new vscode.ThemeIcon('check', new vscode.ThemeColor('testing.iconPassed'));
      case 'failed': return new vscode.ThemeIcon('x', new vscode.ThemeColor('testing.iconFailed'));
      case 'executing': return new vscode.ThemeIcon('sync~spin');
      case 'planned': return new vscode.ThemeIcon('list-ordered');
      case 'digested': return new vscode.ThemeIcon('book');
      default: return new vscode.ThemeIcon('circle-outline');
    }
  }
}
```

### 6.3 Update Workflow Detail Panel

Simplify the webview panel for 3-phase workflow.

**src/views/workflowDetailPanel.ts:**
```typescript
import * as vscode from 'vscode';
import { getAuraService, Workflow, WorkflowStep, Agent } from '../auraService';

export class WorkflowDetailPanel {
  public static currentPanel: WorkflowDetailPanel | undefined;
  private readonly _panel: vscode.WebviewPanel;
  private readonly _extensionUri: vscode.Uri;
  private _workflowId: string;
  private _disposables: vscode.Disposable[] = [];

  public static async createOrShow(extensionUri: vscode.Uri, workflowId: string) {
    const column = vscode.window.activeTextEditor
      ? vscode.window.activeTextEditor.viewColumn
      : undefined;

    if (WorkflowDetailPanel.currentPanel) {
      WorkflowDetailPanel.currentPanel._workflowId = workflowId;
      WorkflowDetailPanel.currentPanel._panel.reveal(column);
      await WorkflowDetailPanel.currentPanel.refresh();
      return;
    }

    const panel = vscode.window.createWebviewPanel(
      'auraWorkflow',
      'Workflow',
      column || vscode.ViewColumn.One,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
      }
    );

    WorkflowDetailPanel.currentPanel = new WorkflowDetailPanel(panel, extensionUri, workflowId);
  }

  private constructor(panel: vscode.WebviewPanel, extensionUri: vscode.Uri, workflowId: string) {
    this._panel = panel;
    this._extensionUri = extensionUri;
    this._workflowId = workflowId;

    this._update();

    this._panel.onDidDispose(() => this.dispose(), null, this._disposables);

    this._panel.webview.onDidReceiveMessage(
      async (message) => {
        switch (message.command) {
          case 'digest':
            await this.handleDigest();
            break;
          case 'plan':
            await this.handlePlan();
            break;
          case 'executeStep':
            await this.handleExecuteStep(message.stepId, message.agentId);
            break;
          case 'retryStep':
            await this.handleRetryStep(message.stepId);
            break;
          case 'skipStep':
            await this.handleSkipStep(message.stepId);
            break;
          case 'refresh':
            await this.refresh();
            break;
        }
      },
      null,
      this._disposables
    );
  }

  public async refresh() {
    await this._update();
  }

  private async _update() {
    const service = getAuraService();
    
    try {
      const workflow = await service.getWorkflow(this._workflowId);
      const agents = await service.getAgents();
      
      this._panel.title = workflow.workItemTitle;
      this._panel.webview.html = this._getHtml(workflow, agents);
    } catch (err) {
      this._panel.webview.html = this._getErrorHtml(String(err));
    }
  }

  private async handleDigest() {
    const service = getAuraService();
    
    try {
      vscode.window.showInformationMessage('Digesting workflow...');
      await service.digestWorkflow(this._workflowId);
      await this.refresh();
      vscode.window.showInformationMessage('Digestion complete!');
    } catch (err) {
      vscode.window.showErrorMessage(`Digestion failed: ${err}`);
    }
  }

  private async handlePlan() {
    const service = getAuraService();
    
    try {
      vscode.window.showInformationMessage('Creating plan...');
      await service.planWorkflow(this._workflowId);
      await this.refresh();
      vscode.window.showInformationMessage('Plan created!');
    } catch (err) {
      vscode.window.showErrorMessage(`Planning failed: ${err}`);
    }
  }

  private async handleExecuteStep(stepId: string, agentId?: string) {
    const service = getAuraService();
    
    try {
      const result = await service.executeStep(this._workflowId, stepId, agentId);
      await this.refresh();
      
      if (result.status === 'Completed') {
        vscode.window.showInformationMessage('Step completed successfully!');
      } else {
        vscode.window.showWarningMessage(`Step failed: ${result.error}`);
      }
    } catch (err) {
      vscode.window.showErrorMessage(`Execution failed: ${err}`);
    }
  }

  private async handleRetryStep(stepId: string) {
    const feedback = await vscode.window.showInputBox({
      prompt: 'Enter feedback for retry (optional)',
      placeHolder: 'e.g., Use async/await pattern',
    });
    
    const service = getAuraService();
    
    try {
      await service.retryStep(this._workflowId, stepId, feedback);
      await this.refresh();
    } catch (err) {
      vscode.window.showErrorMessage(`Retry failed: ${err}`);
    }
  }

  private async handleSkipStep(stepId: string) {
    const service = getAuraService();
    
    try {
      await service.skipStep(this._workflowId, stepId);
      await this.refresh();
    } catch (err) {
      vscode.window.showErrorMessage(`Skip failed: ${err}`);
    }
  }

  private _getHtml(workflow: Workflow, agents: Agent[]): string {
    // Generate HTML for workflow detail view
    // Include: phases, steps, agent selectors, action buttons
    return `<!DOCTYPE html>
    <html>
    <head>
      <style>
        body { font-family: var(--vscode-font-family); padding: 20px; }
        .phase { margin-bottom: 20px; padding: 15px; border: 1px solid var(--vscode-panel-border); }
        .phase-title { font-size: 14px; font-weight: bold; margin-bottom: 10px; }
        .step { padding: 10px; margin: 5px 0; background: var(--vscode-editor-background); }
        .step-header { display: flex; justify-content: space-between; align-items: center; }
        .status-completed { color: var(--vscode-testing-iconPassed); }
        .status-failed { color: var(--vscode-testing-iconFailed); }
        .status-running { color: var(--vscode-charts-yellow); }
        button { margin: 0 5px; padding: 5px 10px; }
        select { padding: 5px; }
      </style>
    </head>
    <body>
      <h2>${workflow.workItemTitle}</h2>
      <p><strong>Status:</strong> ${workflow.status}</p>
      
      ${this._renderPhases(workflow, agents)}
      
      <script>
        const vscode = acquireVsCodeApi();
        
        function digest() {
          vscode.postMessage({ command: 'digest' });
        }
        
        function plan() {
          vscode.postMessage({ command: 'plan' });
        }
        
        function executeStep(stepId) {
          const agentSelect = document.getElementById('agent-' + stepId);
          const agentId = agentSelect ? agentSelect.value : undefined;
          vscode.postMessage({ command: 'executeStep', stepId, agentId: agentId || undefined });
        }
        
        function retryStep(stepId) {
          vscode.postMessage({ command: 'retryStep', stepId });
        }
        
        function skipStep(stepId) {
          vscode.postMessage({ command: 'skipStep', stepId });
        }
        
        function refresh() {
          vscode.postMessage({ command: 'refresh' });
        }
      </script>
    </body>
    </html>`;
  }

  private _renderPhases(workflow: Workflow, agents: Agent[]): string {
    const canDigest = workflow.status === 'Created';
    const canPlan = workflow.status === 'Digested';
    const canExecute = workflow.status === 'Planned' || workflow.status === 'Executing';
    
    return `
      <div class="phase">
        <div class="phase-title">Phase 1: Digest</div>
        ${workflow.digestedContext ? '✓ Context extracted' : 'Not started'}
        ${canDigest ? '<button onclick="digest()">▶ Digest</button>' : ''}
      </div>
      
      <div class="phase">
        <div class="phase-title">Phase 2: Plan</div>
        ${workflow.steps.length > 0 ? `✓ ${workflow.steps.length} steps` : 'No plan yet'}
        ${canPlan ? '<button onclick="plan()">▶ Create Plan</button>' : ''}
      </div>
      
      <div class="phase">
        <div class="phase-title">Phase 3: Execute</div>
        ${workflow.steps.map(step => this._renderStep(step, agents, canExecute)).join('')}
      </div>
    `;
  }

  private _renderStep(step: WorkflowStep, agents: Agent[], canExecute: boolean): string {
    const matchingAgents = agents.filter(a => 
      a.capabilities.includes(step.capability)
    );
    
    const statusClass = `status-${step.status.toLowerCase()}`;
    
    return `
      <div class="step">
        <div class="step-header">
          <span><strong>${step.order}. ${step.name}</strong></span>
          <span class="${statusClass}">${step.status}</span>
        </div>
        <div>
          <small>Capability: ${step.capability}</small>
        </div>
        ${canExecute && step.status === 'Pending' ? `
          <div style="margin-top: 10px;">
            <select id="agent-${step.id}">
              ${matchingAgents.map(a => `<option value="${a.id}">${a.name}</option>`).join('')}
            </select>
            <button onclick="executeStep('${step.id}')">▶ Run</button>
          </div>
        ` : ''}
        ${step.status === 'Failed' ? `
          <button onclick="retryStep('${step.id}')">↻ Retry</button>
          <button onclick="skipStep('${step.id}')">⏭ Skip</button>
        ` : ''}
      </div>
    `;
  }

  private _getErrorHtml(error: string): string {
    return `<html><body><h2>Error</h2><p>${error}</p></body></html>`;
  }

  public dispose() {
    WorkflowDetailPanel.currentPanel = undefined;
    this._panel.dispose();
    this._disposables.forEach(d => d.dispose());
  }
}
```

### 6.4 Update Extension Activation

**src/extension.ts:**
```typescript
import * as vscode from 'vscode';
import { WorkflowsTreeProvider } from './views/workflowsTreeProvider';
import { AgentsTreeProvider } from './views/agentsTreeProvider';
import { WorkflowDetailPanel } from './views/workflowDetailPanel';
import { getAuraService } from './auraService';

export function activate(context: vscode.ExtensionContext) {
  // Tree providers
  const workflowsProvider = new WorkflowsTreeProvider();
  const agentsProvider = new AgentsTreeProvider();
  
  vscode.window.registerTreeDataProvider('aura.workflows', workflowsProvider);
  vscode.window.registerTreeDataProvider('aura.agents', agentsProvider);
  
  // Commands
  context.subscriptions.push(
    vscode.commands.registerCommand('aura.refreshWorkflows', () => {
      workflowsProvider.refresh();
    }),
    
    vscode.commands.registerCommand('aura.refreshAgents', () => {
      agentsProvider.refresh();
    }),
    
    vscode.commands.registerCommand('aura.openWorkflow', (workflowId: string) => {
      WorkflowDetailPanel.createOrShow(context.extensionUri, workflowId);
    }),
    
    vscode.commands.registerCommand('aura.createWorkflow', async () => {
      const title = await vscode.window.showInputBox({
        prompt: 'Workflow title',
        placeHolder: 'e.g., Add user authentication',
      });
      
      if (!title) return;
      
      const service = getAuraService();
      const workflow = await service.createWorkflow({
        workItemId: `manual-${Date.now()}`,
        workItemTitle: title,
        workspacePath: vscode.workspace.workspaceFolders?.[0]?.uri.fsPath,
      });
      
      workflowsProvider.refresh();
      WorkflowDetailPanel.createOrShow(context.extensionUri, workflow.id);
    })
  );
  
  // Status bar
  const statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left);
  statusBar.text = '$(aura-icon) Aura';
  statusBar.command = 'aura.refreshWorkflows';
  statusBar.show();
  
  // Check API health
  checkHealth(statusBar);
  setInterval(() => checkHealth(statusBar), 30000);
}

async function checkHealth(statusBar: vscode.StatusBarItem) {
  const service = getAuraService();
  const healthy = await service.isHealthy();
  
  statusBar.text = healthy 
    ? '$(check) Aura' 
    : '$(warning) Aura (disconnected)';
}

export function deactivate() {}
```

### 6.5 Update Package.json

Add/update configuration and commands:

```json
{
  "contributes": {
    "configuration": {
      "title": "Aura",
      "properties": {
        "aura.apiUrl": {
          "type": "string",
          "default": "http://localhost:5258",
          "description": "Aura API URL"
        }
      }
    },
    "commands": [
      { "command": "aura.refreshWorkflows", "title": "Refresh Workflows", "category": "Aura" },
      { "command": "aura.refreshAgents", "title": "Refresh Agents", "category": "Aura" },
      { "command": "aura.openWorkflow", "title": "Open Workflow", "category": "Aura" },
      { "command": "aura.createWorkflow", "title": "Create Workflow", "category": "Aura" }
    ],
    "views": {
      "aura": [
        { "id": "aura.workflows", "name": "Workflows" },
        { "id": "aura.agents", "name": "Agents" }
      ]
    }
  }
}
```

## Verification

1. ✅ Extension compiles: `npm run compile`
2. ✅ Extension runs: F5 in VS Code
3. ✅ Workflows tree shows workflows
4. ✅ Agents tree shows agents
5. ✅ Open workflow shows detail panel
6. ✅ Digest/Plan/Execute buttons work
7. ✅ Agent selection works

## Deliverables

- [ ] New `AuraService` class (single file, ~200 lines)
- [ ] Updated tree providers
- [ ] Simplified workflow detail panel
- [ ] Updated extension activation
- [ ] Updated package.json configuration

## What We Removed

| Removed | Lines | Reason |
|---------|-------|--------|
| AgentOrchestratorService | ~800 | Replaced by AuraService |
| GitHubService | ~300 | Move to API-side agent |
| Complex polling | ~200 | SSE instead |
| Status tracking | ~400 | Simplified to refresh |
| Multiple webview panels | ~500 | Single panel |

**Estimated reduction:** ~2,200 lines → ~400 lines
