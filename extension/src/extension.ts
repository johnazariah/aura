import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider, AgentItem } from './providers/agentTreeProvider';
import { ChatWindowProvider } from './providers/chatWindowProvider';
import { WorkflowTreeProvider } from './providers/workflowTreeProvider';
import { WorkflowPanelProvider } from './providers/workflowPanelProvider';
import { HealthCheckService } from './services/healthCheckService';
import { AuraApiService, AgentInfo } from './services/auraApiService';

let auraApiService: AuraApiService;
let healthCheckService: HealthCheckService;
let statusTreeProvider: StatusTreeProvider;
let agentTreeProvider: AgentTreeProvider;
let workflowTreeProvider: WorkflowTreeProvider;
let chatWindowProvider: ChatWindowProvider;
let workflowPanelProvider: WorkflowPanelProvider;
let statusBarItem: vscode.StatusBarItem;
let refreshInterval: NodeJS.Timeout | undefined;

export async function activate(context: vscode.ExtensionContext) {
    console.log('Aura extension activating...');

    // Initialize services
    auraApiService = new AuraApiService();
    healthCheckService = new HealthCheckService(auraApiService);

    // Initialize tree providers
    statusTreeProvider = new StatusTreeProvider(healthCheckService);
    agentTreeProvider = new AgentTreeProvider(auraApiService);
    workflowTreeProvider = new WorkflowTreeProvider(auraApiService);
    chatWindowProvider = new ChatWindowProvider(context.extensionUri, auraApiService);
    workflowPanelProvider = new WorkflowPanelProvider(context.extensionUri, auraApiService);

    // Register tree views
    const statusView = vscode.window.createTreeView('aura.status', {
        treeDataProvider: statusTreeProvider,
        showCollapseAll: false
    });

    const workflowView = vscode.window.createTreeView('aura.workflows', {
        treeDataProvider: workflowTreeProvider,
        showCollapseAll: true
    });

    const agentView = vscode.window.createTreeView('aura.agents', {
        treeDataProvider: agentTreeProvider,
        showCollapseAll: true
    });

    // Create status bar item
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'aura.refreshStatus';
    statusBarItem.show();
    updateStatusBar('checking');

    // Register commands
    const refreshCommand = vscode.commands.registerCommand('aura.refreshStatus', async () => {
        await refreshAll();
    });

    const startCommand = vscode.commands.registerCommand('aura.startServices', async () => {
        vscode.window.showInformationMessage('Starting Aura services... (Aspire orchestration coming soon)');
    });

    const stopCommand = vscode.commands.registerCommand('aura.stopServices', async () => {
        vscode.window.showInformationMessage('Stopping Aura services... (Aspire orchestration coming soon)');
    });

    const executeAgentCommand = vscode.commands.registerCommand('aura.executeAgent', async (item?: AgentItem) => {
        await executeAgent(item);
    });

    const quickExecuteCommand = vscode.commands.registerCommand('aura.quickExecute', async () => {
        await executeAgent();
    });

    const selectAgentCommand = vscode.commands.registerCommand('aura.selectAgent', async (agent: AgentInfo) => {
        const actions = [
            { label: '$(comment) Chat with this agent', action: 'chat' },
            { label: '$(info) View details', action: 'details' },
            { label: '$(copy) Copy agent ID', action: 'copy' }
        ];

        const selected = await vscode.window.showQuickPick(actions, {
            placeHolder: `${agent.name} - Select action`
        });

        if (!selected) return;

        switch (selected.action) {
            case 'chat':
                await openChatWindow(agent);
                break;
            case 'details':
                showAgentDetails(agent);
                break;
            case 'copy':
                await vscode.env.clipboard.writeText(agent.id);
                vscode.window.showInformationMessage(`Copied agent ID: ${agent.id}`);
                break;
        }
    });

    // Open Chat command
    const openChatCommand = vscode.commands.registerCommand('aura.openChat', async () => {
        try {
            const agents = await auraApiService.getAgents();
            if (agents.length === 0) {
                vscode.window.showWarningMessage('No agents available');
                return;
            }

            const items = agents.map(a => ({
                label: a.name,
                description: a.model,
                detail: a.description,
                agent: a
            }));

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'Select an agent to chat with'
            });

            if (selected) {
                await chatWindowProvider.openChatWindow(selected.agent);
            }
        } catch (error) {
            vscode.window.showErrorMessage('Failed to load agents');
        }
    });

    // RAG Commands
    const indexWorkspaceCommand = vscode.commands.registerCommand('aura.indexWorkspace', async () => {
        await indexWorkspace();
    });

    const showRagStatsCommand = vscode.commands.registerCommand('aura.showRagStats', async () => {
        await showRagStats();
    });

    const clearRagIndexCommand = vscode.commands.registerCommand('aura.clearRagIndex', async () => {
        await clearRagIndex();
    });

    // Workflow commands
    const createWorkflowCommand = vscode.commands.registerCommand('aura.createWorkflow', async () => {
        await createWorkflow();
    });

    const openWorkflowCommand = vscode.commands.registerCommand('aura.openWorkflow', async (workflowId: string) => {
        await workflowPanelProvider.openWorkflowPanel(workflowId);
    });

    const executeStepCommand = vscode.commands.registerCommand('aura.executeStep', async (workflowId: string, stepId: string) => {
        await executeStep(workflowId, stepId);
    });

    const refreshWorkflowsCommand = vscode.commands.registerCommand('aura.refreshWorkflows', async () => {
        workflowTreeProvider.refresh();
    });

    const deleteWorkflowCommand = vscode.commands.registerCommand('aura.deleteWorkflow', async (item?: any) => {
        await deleteWorkflow(item);
    });

    // Subscribe to configuration changes
    const configWatcher = vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('aura')) {
            setupAutoRefresh();
        }
    });

    // Add disposables
    context.subscriptions.push(
        statusView,
        workflowView,
        agentView,
        statusBarItem,
        refreshCommand,
        startCommand,
        stopCommand,
        executeAgentCommand,
        quickExecuteCommand,
        selectAgentCommand,
        openChatCommand,
        indexWorkspaceCommand,
        showRagStatsCommand,
        clearRagIndexCommand,
        createWorkflowCommand,
        openWorkflowCommand,
        executeStepCommand,
        refreshWorkflowsCommand,
        deleteWorkflowCommand,
        configWatcher
    );

    // Initial refresh
    await refreshAll();

    // Setup auto-refresh
    setupAutoRefresh();

    console.log('Aura extension activated');
}

// =====================
// RAG Functions
// =====================

async function indexWorkspace(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('No workspace folder open');
        return;
    }

    // Let user pick patterns
    const patterns = await vscode.window.showInputBox({
        prompt: 'File patterns to index (comma-separated)',
        value: '*.cs,*.ts,*.md',
        placeHolder: '*.cs,*.ts,*.py,*.md'
    });

    if (!patterns) return;

    const includePatterns = patterns.split(',').map(p => p.trim()).filter(p => p.length > 0);
    const workspacePath = workspaceFolders[0].uri.fsPath;

    await vscode.window.withProgress(
        {
            location: vscode.ProgressLocation.Notification,
            title: 'Indexing workspace...',
            cancellable: false
        },
        async (progress) => {
            try {
                progress.report({ message: 'Scanning files...' });

                const result = await auraApiService.indexDirectory(
                    workspacePath,
                    includePatterns,
                    ['**/node_modules/**', '**/bin/**', '**/obj/**', '**/.git/**'],
                    true
                );

                if (result.success) {
                    vscode.window.showInformationMessage(
                        `✓ Indexed ${result.filesIndexed} files from workspace`
                    );
                } else {
                    vscode.window.showErrorMessage('Indexing failed: ' + result.message);
                }

                // Refresh status to show new RAG stats
                statusTreeProvider.refresh();
            } catch (error) {
                const message = error instanceof Error ? error.message : 'Unknown error';
                vscode.window.showErrorMessage(`Failed to index workspace: ${message}`);
            }
        }
    );
}

async function showRagStats(): Promise<void> {
    try {
        const stats = await auraApiService.getRagStats();
        const health = await auraApiService.getRagHealth();

        const message = `RAG Index Status:
• Documents: ${stats.totalDocuments}
• Chunks: ${stats.totalChunks}
• Health: ${health.healthy ? 'Healthy' : 'Unhealthy'}`;

        vscode.window.showInformationMessage(message, 'Index Workspace', 'Clear Index')
            .then(action => {
                if (action === 'Index Workspace') {
                    vscode.commands.executeCommand('aura.indexWorkspace');
                } else if (action === 'Clear Index') {
                    vscode.commands.executeCommand('aura.clearRagIndex');
                }
            });
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to get RAG stats: ${message}`);
    }
}

async function clearRagIndex(): Promise<void> {
    const confirm = await vscode.window.showWarningMessage(
        'Clear the entire RAG index? This cannot be undone.',
        { modal: true },
        'Clear Index'
    );

    if (confirm !== 'Clear Index') return;

    try {
        await auraApiService.clearRagIndex();
        vscode.window.showInformationMessage('RAG index cleared');
        statusTreeProvider.refresh();
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to clear RAG index: ${message}`);
    }
}

// =====================
// Existing Functions
// =====================

async function refreshAll(): Promise<void> {
    updateStatusBar('checking');

    try {
        await healthCheckService.checkAll();
        statusTreeProvider.refresh();
        agentTreeProvider.refresh();

        const status = healthCheckService.getOverallStatus();
        updateStatusBar(status);
    } catch (error) {
        console.error('Error refreshing status:', error);
        updateStatusBar('error');
    }
}

function updateStatusBar(status: 'healthy' | 'degraded' | 'error' | 'checking'): void {
    const icons: Record<string, string> = {
        healthy: '$(check)',
        degraded: '$(warning)',
        error: '$(error)',
        checking: '$(sync~spin)'
    };

    const colors: Record<string, vscode.ThemeColor | undefined> = {
        healthy: undefined,
        degraded: new vscode.ThemeColor('statusBarItem.warningBackground'),
        error: new vscode.ThemeColor('statusBarItem.errorBackground'),
        checking: undefined
    };

    statusBarItem.text = `${icons[status]} Aura`;
    statusBarItem.backgroundColor = colors[status];
    statusBarItem.tooltip = `Aura Status: ${status}`;
}

function setupAutoRefresh(): void {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = undefined;
    }

    const config = vscode.workspace.getConfiguration('aura');
    const autoRefresh = config.get<boolean>('autoRefresh', true);

    if (autoRefresh) {
        refreshInterval = setInterval(refreshAll, 10000);
    }
}

async function openChatWindow(agent: AgentInfo): Promise<void> {
    await chatWindowProvider.openChatWindow(agent);
}

async function executeAgentById(agentId: string, agentName: string): Promise<void> {
    const prompt = await vscode.window.showInputBox({
        prompt: `Enter prompt for ${agentName}`,
        placeHolder: 'What would you like the agent to do?'
    });

    if (!prompt) return;

    const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

    await vscode.window.withProgress(
        {
            location: vscode.ProgressLocation.Notification,
            title: `Executing ${agentName}...`,
            cancellable: false
        },
        async () => {
            const result = await auraApiService.executeAgent(agentId, prompt, workspacePath);

            const doc = await vscode.workspace.openTextDocument({
                content: `# Agent: ${agentName}\n\n## Prompt\n${prompt}\n\n## Response\n${result}`,
                language: 'markdown'
            });
            await vscode.window.showTextDocument(doc);
        }
    );
}

function showAgentDetails(agent: AgentInfo): void {
    const capabilities = agent.capabilities?.join(', ') || 'none';
    const languages = agent.languages?.join(', ') || 'all';
    const priority = agent.priority ?? 'default';

    const details = `
# ${agent.name}

## Overview
- **ID:** ${agent.id}
- **Model:** ${agent.model}
- **Provider:** ${agent.provider || 'ollama'}
- **Priority:** ${priority}

## Capabilities
${capabilities}

## Language Support
${languages}
`;

    vscode.workspace.openTextDocument({
        content: details.trim(),
        language: 'markdown'
    }).then(doc => vscode.window.showTextDocument(doc));
}

async function executeAgent(item?: AgentItem): Promise<void> {
    try {
        let agentId: string;
        let agentName: string;

        if (item && item.agent.id !== 'offline') {
            agentId = item.agent.id;
            agentName = item.agent.name;
        } else {
            const agents = await auraApiService.getAgents();
            if (agents.length === 0) {
                vscode.window.showWarningMessage('No agents available');
                return;
            }

            interface AgentPickItem extends vscode.QuickPickItem {
                id: string;
            }

            const pickItems: AgentPickItem[] = agents.map((a: AgentInfo) => ({
                label: a.name,
                description: a.model,
                id: a.id
            }));

            const picked = await vscode.window.showQuickPick(pickItems, {
                placeHolder: 'Select an agent to execute'
            });

            if (!picked) return;

            agentId = picked.id;
            agentName = picked.label;
        }

        const prompt = await vscode.window.showInputBox({
            prompt: `Enter prompt for ${agentName}`,
            placeHolder: 'What would you like the agent to do?'
        });

        if (!prompt) return;

        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                title: `Executing ${agentName}...`,
                cancellable: false
            },
            async () => {
                const result = await auraApiService.executeAgent(agentId, prompt, workspacePath);

                const doc = await vscode.workspace.openTextDocument({
                    content: `# Agent: ${agentName}\n\n## Prompt\n${prompt}\n\n## Response\n${result}`,
                    language: 'markdown'
                });
                await vscode.window.showTextDocument(doc);
            }
        );

    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to execute agent: ${message}`);
    }
}

// =====================
// Workflow Functions
// =====================

async function createWorkflow(): Promise<void> {
    // Open the new workflow panel with a form
    await workflowPanelProvider.openNewWorkflowPanel((workflowId: string) => {
        // Refresh tree when workflow is created
        workflowTreeProvider.refresh();
    });
}

async function executeStep(workflowId: string, stepId: string): Promise<void> {
    try {
        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                title: 'Executing step...',
                cancellable: false
            },
            async () => {
                const step = await auraApiService.executeWorkflowStep(workflowId, stepId);
                workflowTreeProvider.refresh();

                if (step.status === 'Completed') {
                    vscode.window.showInformationMessage(`Step completed: ${step.name}`);
                } else if (step.status === 'Failed') {
                    vscode.window.showErrorMessage(`Step failed: ${step.error || 'Unknown error'}`);
                }
            }
        );
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to execute step: ${message}`);
        workflowTreeProvider.refresh();
    }
}

async function deleteWorkflow(item?: any): Promise<void> {
    // Get workflow ID from tree item
    let workflowId: string | undefined;
    let workflowTitle: string = 'this workflow';

    if (item?.workflow) {
        workflowId = item.workflow.id;
        workflowTitle = item.workflow.title;
    } else if (item?.workflowId) {
        workflowId = item.workflowId;
        workflowTitle = item.label || 'this workflow';
    }

    if (!workflowId) {
        vscode.window.showErrorMessage('No workflow selected');
        return;
    }

    // Confirm deletion
    const confirm = await vscode.window.showWarningMessage(
        `Delete "${workflowTitle}"?`,
        { modal: true },
        'Delete'
    );

    if (confirm !== 'Delete') {
        return;
    }

    try {
        await auraApiService.deleteWorkflow(workflowId);
        workflowTreeProvider.refresh();
        vscode.window.showInformationMessage(`Deleted: ${workflowTitle}`);
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to delete workflow: ${message}`);
    }
}

export function deactivate() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
    }
}
