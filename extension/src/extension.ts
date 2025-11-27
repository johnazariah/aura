import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider, AgentItem } from './providers/agentTreeProvider';
import { HealthCheckService } from './services/healthCheckService';
import { AuraApiService, AgentInfo } from './services/auraApiService';

let auraApiService: AuraApiService;
let healthCheckService: HealthCheckService;
let statusTreeProvider: StatusTreeProvider;
let agentTreeProvider: AgentTreeProvider;
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

    // Register tree views
    const statusView = vscode.window.createTreeView('aura.status', {
        treeDataProvider: statusTreeProvider,
        showCollapseAll: false
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
        // Store selected agent and show quick action menu
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
                await executeAgentById(agent.id, agent.name);
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

    // Subscribe to configuration changes
    const configWatcher = vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('aura')) {
            setupAutoRefresh();
        }
    });

    // Add disposables
    context.subscriptions.push(
        statusView,
        agentView,
        statusBarItem,
        refreshCommand,
        startCommand,
        stopCommand,
        executeAgentCommand,
        quickExecuteCommand,
        selectAgentCommand,
        configWatcher
    );

    // Initial refresh
    await refreshAll();

    // Setup auto-refresh
    setupAutoRefresh();

    console.log('Aura extension activated');
}

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
    // Clear existing interval
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = undefined;
    }

    const config = vscode.workspace.getConfiguration('aura');
    const autoRefresh = config.get<boolean>('autoRefresh', true);

    if (autoRefresh) {
        refreshInterval = setInterval(refreshAll, 10000); // 10 seconds
    }
}

async function executeAgentById(agentId: string, agentName: string): Promise<void> {
    const prompt = await vscode.window.showInputBox({
        prompt: `Enter prompt for ${agentName}`,
        placeHolder: 'What would you like the agent to do?'
    });

    if (!prompt) {
        return;
    }

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
        // Get agent - either from context menu item or prompt user to select
        let agentId: string;
        let agentName: string;

        if (item && item.agent.id !== 'offline') {
            agentId = item.agent.id;
            agentName = item.agent.name;
        } else {
            // Fetch agents and let user pick
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

            if (!picked) {
                return;
            }

            agentId = picked.id;
            agentName = picked.label;
        }

        // Get the prompt from user
        const prompt = await vscode.window.showInputBox({
            prompt: `Enter prompt for ${agentName}`,
            placeHolder: 'What would you like the agent to do?'
        });

        if (!prompt) {
            return;
        }

        // Get workspace path if available
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

        // Show progress
        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                title: `Executing ${agentName}...`,
                cancellable: false
            },
            async () => {
                const result = await auraApiService.executeAgent(agentId, prompt, workspacePath);
                
                // Show result in a new document
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

export function deactivate() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
    }
}
