import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider, AgentItem, setExtensionPath } from './providers/agentTreeProvider';
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

    // Set extension path for resource loading
    setExtensionPath(context.extensionPath);

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

    const showIndexingProgressCommand = vscode.commands.registerCommand('aura.showIndexingProgress', async () => {
        await showIndexingProgress();
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
        showIndexingProgressCommand,
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

    const workspacePath = workspaceFolders[0].uri.fsPath;
    const excludePatterns = ['**/node_modules/**', '**/bin/**', '**/obj/**', '**/.git/**'];

    try {
        // Start background indexing
        const job = await auraApiService.startBackgroundIndex(
            workspacePath,
            true,
            undefined,
            excludePatterns
        );

        vscode.window.showInformationMessage(`Indexing started in background (Job: ${job.jobId.slice(0, 8)}...)`);

        // Update status bar to show indexing
        updateIndexingStatusBar(job.jobId);

    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to start indexing: ${message}`);
    }
}

let indexingStatusBarItem: vscode.StatusBarItem | undefined;
let indexingPollInterval: NodeJS.Timeout | undefined;

function updateIndexingStatusBar(jobId: string): void {
    // Create or show indexing status bar item
    if (!indexingStatusBarItem) {
        indexingStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 99);
    }

    indexingStatusBarItem.text = '$(sync~spin) Indexing...';
    indexingStatusBarItem.tooltip = 'Background indexing in progress';
    indexingStatusBarItem.command = 'aura.showIndexingProgress';
    indexingStatusBarItem.show();

    // Poll for progress
    indexingPollInterval = setInterval(async () => {
        try {
            const status = await auraApiService.getBackgroundJobStatus(jobId);
            
            if (status.state === 'Processing') {
                const percent = status.totalItems > 0 
                    ? Math.round((status.processedItems / status.totalItems) * 100)
                    : 0;
                indexingStatusBarItem!.text = `$(sync~spin) Indexing ${percent}%`;
                indexingStatusBarItem!.tooltip = `Indexing: ${status.processedItems}/${status.totalItems} files`;
            } else if (status.state === 'Completed') {
                clearInterval(indexingPollInterval);
                indexingPollInterval = undefined;
                
                indexingStatusBarItem!.text = '$(check) Indexed';
                indexingStatusBarItem!.tooltip = `Completed: ${status.processedItems} files indexed`;
                
                // Hide after 5 seconds
                setTimeout(() => {
                    indexingStatusBarItem?.hide();
                }, 5000);

                // Refresh status tree
                statusTreeProvider.refresh();
                vscode.window.showInformationMessage(`✓ Indexed ${status.processedItems} files`);
            } else if (status.state === 'Failed') {
                clearInterval(indexingPollInterval);
                indexingPollInterval = undefined;
                
                indexingStatusBarItem!.text = '$(error) Index Failed';
                indexingStatusBarItem!.tooltip = status.error || 'Indexing failed';
                indexingStatusBarItem!.backgroundColor = new vscode.ThemeColor('statusBarItem.errorBackground');
                
                vscode.window.showErrorMessage(`Indexing failed: ${status.error}`);
            }
        } catch (error) {
            console.error('Failed to poll indexing status:', error);
        }
    }, 1000);
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

async function showIndexingProgress(): Promise<void> {
    // Show current indexing status in an info message
    try {
        const stats = await auraApiService.getRagStats();
        vscode.window.showInformationMessage(
            `RAG Index: ${stats.totalDocuments} symbols, ${stats.totalChunks} embeddings`,
            'Index Workspace'
        ).then(action => {
            if (action === 'Index Workspace') {
                vscode.commands.executeCommand('aura.indexWorkspace');
            }
        });
    } catch {
        vscode.window.showInformationMessage('Click the status bar icon while indexing to see progress');
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
    if (indexingPollInterval) {
        clearInterval(indexingPollInterval);
    }
    indexingStatusBarItem?.dispose();
}
