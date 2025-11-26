import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider } from './providers/agentTreeProvider';
import { HealthCheckService } from './services/healthCheckService';
import { AuraApiService } from './services/auraApiService';

let healthCheckService: HealthCheckService;
let statusTreeProvider: StatusTreeProvider;
let agentTreeProvider: AgentTreeProvider;
let statusBarItem: vscode.StatusBarItem;
let refreshInterval: NodeJS.Timeout | undefined;

export async function activate(context: vscode.ExtensionContext) {
    console.log('Aura extension activating...');

    // Initialize services
    const auraApiService = new AuraApiService();
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

export function deactivate() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
    }
}
