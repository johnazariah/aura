import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider, AgentItem, setExtensionPath } from './providers/agentTreeProvider';
import { ChatWindowProvider } from './providers/chatWindowProvider';
import { WorkflowTreeProvider } from './providers/workflowTreeProvider';
import { WorkflowPanelProvider } from './providers/workflowPanelProvider';
import { WelcomeViewProvider } from './providers/welcomeViewProvider';
import { HealthCheckService } from './services/healthCheckService';
import { AuraApiService, AgentInfo } from './services/auraApiService';

let auraApiService: AuraApiService;
let healthCheckService: HealthCheckService;
let statusTreeProvider: StatusTreeProvider;
let agentTreeProvider: AgentTreeProvider;
let workflowTreeProvider: WorkflowTreeProvider;
let welcomeViewProvider: WelcomeViewProvider;
let chatWindowProvider: ChatWindowProvider;
let workflowPanelProvider: WorkflowPanelProvider;
let statusBarItem: vscode.StatusBarItem;
let refreshInterval: NodeJS.Timeout | undefined;

export async function activate(context: vscode.ExtensionContext) {
    console.log('Aura extension activating...');

    // Set default context values immediately - assume not onboarded until we check
    // This ensures views with when clauses behave correctly from the start
    await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
    await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);

    // Set extension path for resource loading
    setExtensionPath(context.extensionPath);

    // Initialize services
    auraApiService = new AuraApiService();
    healthCheckService = new HealthCheckService(auraApiService);

    // Initialize tree providers
    statusTreeProvider = new StatusTreeProvider(healthCheckService);
    agentTreeProvider = new AgentTreeProvider(auraApiService);
    workflowTreeProvider = new WorkflowTreeProvider(auraApiService);
    welcomeViewProvider = new WelcomeViewProvider(auraApiService);
    chatWindowProvider = new ChatWindowProvider(context.extensionUri, auraApiService);
    workflowPanelProvider = new WorkflowPanelProvider(context.extensionUri, auraApiService);

    // Register tree views
    const statusView = vscode.window.createTreeView('aura.status', {
        treeDataProvider: statusTreeProvider,
        showCollapseAll: false
    });

    const welcomeView = vscode.window.createTreeView('aura.welcome', {
        treeDataProvider: welcomeViewProvider,
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

    // Workspace onboarding command
    const onboardWorkspaceCommand = vscode.commands.registerCommand('aura.onboardWorkspace', async () => {
        await onboardWorkspace();
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

    // Help commands
    const showHelpCommand = vscode.commands.registerCommand('aura.showHelp', async () => {
        const items: vscode.QuickPickItem[] = [
            { label: '$(rocket) Getting Started', description: 'Open the Aura walkthrough' },
            { label: '$(book) Documentation', description: 'Open full documentation' },
            { label: '$(note) Quick Reference', description: 'Show keyboard shortcuts and tips' },
            { label: '$(comment-discussion) Use Cases', description: 'See practical examples' },
        ];
        const selected = await vscode.window.showQuickPick(items, {
            placeHolder: 'How can we help?'
        });
        if (selected) {
            if (selected.label.includes('Getting Started')) {
                vscode.commands.executeCommand('workbench.action.openWalkthrough', 'aura.aura#aura.gettingStarted');
            } else if (selected.label.includes('Documentation')) {
                vscode.commands.executeCommand('aura.openDocs');
            } else if (selected.label.includes('Quick Reference')) {
                vscode.commands.executeCommand('aura.showCheatSheet');
            } else if (selected.label.includes('Use Cases')) {
                openDocFile('user-guide/use-cases.md');
            }
        }
    });

    const openDocsCommand = vscode.commands.registerCommand('aura.openDocs', async () => {
        openDocFile('README.md');
    });

    const showCheatSheetCommand = vscode.commands.registerCommand('aura.showCheatSheet', async () => {
        showCheatSheet();
    });

    const openGettingStartedCommand = vscode.commands.registerCommand('aura.openGettingStarted', async () => {
        vscode.commands.executeCommand('workbench.action.openWalkthrough', 'aura.aura#aura.gettingStarted');
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
        welcomeView,
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
        onboardWorkspaceCommand,
        createWorkflowCommand,
        openWorkflowCommand,
        executeStepCommand,
        refreshWorkflowsCommand,
        deleteWorkflowCommand,
        showHelpCommand,
        openDocsCommand,
        showCheatSheetCommand,
        openGettingStartedCommand,
        configWatcher
    );

    // Initial refresh
    await refreshAll();

    // Check workspace onboarding status and set context for view visibility
    await checkOnboardingStatus();

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

/**
 * Check if the current workspace is onboarded and set VS Code context accordingly.
 * This controls which views are shown (welcome vs workflows).
 */
async function checkOnboardingStatus(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        // No workspace - show welcome to guide user
        console.log('[Aura] No workspace folders, setting not onboarded');
        await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;
    console.log('[Aura] Checking onboarding status for:', workspacePath);

    try {
        const status = await auraApiService.getWorkspaceStatus(workspacePath);
        console.log('[Aura] API returned status:', JSON.stringify(status));
        
        if (status.isOnboarded) {
            console.log('[Aura] Workspace IS onboarded, hiding welcome view');
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
            console.log(`Workspace onboarded: ${workspacePath} (${status.stats.files} files, ${status.stats.chunks} chunks)`);
        } else {
            console.log('[Aura] Workspace NOT onboarded, showing welcome view');
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
            console.log(`Workspace not onboarded: ${workspacePath}`);
        }
    } catch (error) {
        // If API call fails, assume not onboarded (will show welcome view)
        console.log('[Aura] Failed to check onboarding status:', error);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
    }
}

async function onboardWorkspace(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('No workspace folder open');
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;

    try {
        vscode.window.showInformationMessage('🚀 Enabling Aura for this workspace...');

        // Call the onboard API
        const result = await auraApiService.onboardWorkspace(workspacePath);

        if (result.success) {
            // Update context to show onboarded views
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);

            // Show progress for indexing
            if (result.jobId) {
                updateIndexingStatusBar(result.jobId);
            }

            // Show setup actions
            const actions = result.setupActions?.join(', ') || 'Indexing started';
            vscode.window.showInformationMessage(`✓ Aura enabled: ${actions}`);

            // Refresh views
            statusTreeProvider.refresh();
            workflowTreeProvider.refresh();
            welcomeViewProvider.refresh();
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to enable Aura: ${message}`);
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
                
                // Refresh status tree to keep panel in sync
                statusTreeProvider.refresh();
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

// =====================
// Help Functions
// =====================

function openDocFile(relativePath: string): void {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (workspaceFolders && workspaceFolders.length > 0) {
        // Try to find docs in the Aura project itself
        const docsPath = vscode.Uri.joinPath(workspaceFolders[0].uri, 'docs', relativePath);
        vscode.workspace.fs.stat(docsPath).then(
            () => {
                vscode.commands.executeCommand('markdown.showPreview', docsPath);
            },
            () => {
                // File not found in workspace, open GitHub docs
                vscode.env.openExternal(vscode.Uri.parse('https://github.com/johnazariah/aura/tree/main/docs/' + relativePath));
            }
        );
    } else {
        // No workspace, open GitHub docs
        vscode.env.openExternal(vscode.Uri.parse('https://github.com/johnazariah/aura/tree/main/docs/' + relativePath));
    }
}

function showCheatSheet(): void {
    const panel = vscode.window.createWebviewPanel(
        'auraCheatSheet',
        'Aura Quick Reference',
        vscode.ViewColumn.One,
        {}
    );

    panel.webview.html = getCheatSheetHtml();
}

function getCheatSheetHtml(): string {
    return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Aura Quick Reference</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            padding: 20px;
            color: var(--vscode-foreground);
            background: var(--vscode-editor-background);
        }
        h1 { color: var(--vscode-textLink-foreground); }
        h2 { 
            color: var(--vscode-textLink-activeForeground); 
            border-bottom: 1px solid var(--vscode-textSeparator-foreground);
            padding-bottom: 5px;
        }
        table { 
            border-collapse: collapse; 
            width: 100%; 
            margin-bottom: 20px;
        }
        th, td { 
            border: 1px solid var(--vscode-textSeparator-foreground); 
            padding: 8px 12px; 
            text-align: left; 
        }
        th { background: var(--vscode-editor-selectionBackground); }
        code { 
            background: var(--vscode-textCodeBlock-background); 
            padding: 2px 6px; 
            border-radius: 3px;
        }
        .shortcut { font-family: monospace; font-weight: bold; }
    </style>
</head>
<body>
    <h1>⚡ Aura Quick Reference</h1>

    <h2>Keyboard Shortcuts</h2>
    <table>
        <tr><th>Action</th><th>Windows/Linux</th><th>macOS</th></tr>
        <tr><td>New Workflow</td><td class="shortcut">Ctrl+Shift+W</td><td class="shortcut">Cmd+Shift+W</td></tr>
        <tr><td>Open Chat</td><td class="shortcut">Ctrl+Shift+A</td><td class="shortcut">Cmd+Shift+A</td></tr>
        <tr><td>Execute Agent</td><td class="shortcut">Ctrl+Shift+E</td><td class="shortcut">Cmd+Shift+E</td></tr>
        <tr><td>Index Workspace</td><td class="shortcut">Ctrl+Alt+I</td><td class="shortcut">Cmd+Alt+I</td></tr>
        <tr><td>Show Help</td><td class="shortcut">Ctrl+Shift+/</td><td class="shortcut">Cmd+Shift+/</td></tr>
    </table>

    <h2>Workflow Tips</h2>
    <table>
        <tr><th>Task</th><th>Example Prompt</th></tr>
        <tr><td>New endpoint</td><td><code>Create POST /api/products endpoint following OrderController patterns</code></td></tr>
        <tr><td>Write tests</td><td><code>Add unit tests for OrderService using xUnit</code></td></tr>
        <tr><td>Refactor</td><td><code>Extract email logic from UserService into EmailNotificationService</code></td></tr>
        <tr><td>Documentation</td><td><code>Add XML docs to all public methods in PaymentController</code></td></tr>
    </table>

    <h2>Chat Examples</h2>
    <table>
        <tr><th>Goal</th><th>Question</th></tr>
        <tr><td>Understand code</td><td><code>How does authentication work in this project?</code></td></tr>
        <tr><td>Find code</td><td><code>Where is payment processing implemented?</code></td></tr>
        <tr><td>Trace flow</td><td><code>What happens when a user places an order?</code></td></tr>
        <tr><td>Review code</td><td><code>Are there SQL injection risks in UserRepository?</code></td></tr>
    </table>

    <h2>Step Actions</h2>
    <table>
        <tr><th>Action</th><th>When to Use</th></tr>
        <tr><td>✅ Approve</td><td>Output looks correct</td></tr>
        <tr><td>❌ Reject</td><td>Output is wrong, let agent retry</td></tr>
        <tr><td>⏭️ Skip</td><td>Step not needed</td></tr>
        <tr><td>💬 Chat</td><td>Ask agent to modify or explain</td></tr>
        <tr><td>🔄 Reassign</td><td>Use a different agent</td></tr>
    </table>

    <p><em>Press <strong>Ctrl+Shift+P</strong> and type "Aura" to see all commands.</em></p>
</body>
</html>`;
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
