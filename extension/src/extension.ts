import * as vscode from 'vscode';
import { StatusTreeProvider } from './providers/statusTreeProvider';
import { AgentTreeProvider, AgentItem, setExtensionPath } from './providers/agentTreeProvider';
import { ChatWindowProvider } from './providers/chatWindowProvider';
import { WorkflowTreeProvider } from './providers/workflowTreeProvider';
import { WorkflowPanelProvider } from './providers/workflowPanelProvider';
import { WelcomeViewProvider } from './providers/welcomeViewProvider';
import { HealthCheckService } from './services/healthCheckService';
import { AuraApiService, AgentInfo } from './services/auraApiService';
import { gitService } from './services/gitService';

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
let currentRefreshRate: number = 10000; // Default 10 second refresh
const FAST_REFRESH_RATE = 1000; // 1 second when indexing
const NORMAL_REFRESH_RATE = 10000; // 10 seconds normally

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

    // Create status bar item - clicking opens current story panel
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'aura.showCurrentStory';
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

    const clearAndReindexCommand = vscode.commands.registerCommand('aura.clearAndReindex', async () => {
        await clearAndReindex();
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

    // Code Graph query commands
    const findImplementationsCommand = vscode.commands.registerCommand('aura.findImplementations', async () => {
        await findImplementations();
    });

    const findCallersCommand = vscode.commands.registerCommand('aura.findCallers', async () => {
        await findCallers();
    });

    const showTypeMembersCommand = vscode.commands.registerCommand('aura.showTypeMembers', async () => {
        await showTypeMembers();
    });

    // Story/Issue integration commands
    const createStoryFromIssueCommand = vscode.commands.registerCommand('aura.createStoryFromIssue', async () => {
        await createStoryFromIssue();
    });

    const refreshFromIssueCommand = vscode.commands.registerCommand('aura.refreshFromIssue', async (workflowId?: string) => {
        await refreshFromIssue(workflowId);
    });

    const postUpdateToIssueCommand = vscode.commands.registerCommand('aura.postUpdateToIssue', async (workflowId?: string) => {
        await postUpdateToIssue(workflowId);
    });

    const openStoryWorktreeCommand = vscode.commands.registerCommand('aura.openStoryWorktree', async (worktreePath?: string) => {
        if (worktreePath) {
            await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(worktreePath), { forceNewWindow: true });
        }
    });

    const showCurrentStoryCommand = vscode.commands.registerCommand('aura.showCurrentStory', async () => {
        await showCurrentStory();
    });

    // Subscribe to configuration changes
    const configWatcher = vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('aura')) {
            setupAutoRefresh();
        }
    });

    // Register MCP (Model Context Protocol) server definition provider
    // This exposes Aura's RAG and Code Graph context to GitHub Copilot
    let mcpProvider: vscode.Disposable | undefined;
    try {
        // Check if the MCP API is available (VS Code 1.100+)
        if ('lm' in vscode && typeof (vscode as any).lm?.registerMcpServerDefinitionProvider === 'function') {
            const apiUrl = vscode.workspace.getConfiguration('aura').get<string>('apiUrl') || 'http://localhost:5300';
            
            mcpProvider = (vscode as any).lm.registerMcpServerDefinitionProvider('aura.context', {
                onDidChangeMcpServerDefinitions: new vscode.EventEmitter<void>().event,
                
                provideMcpServerDefinitions: async (_token: vscode.CancellationToken) => {
                    // Return Aura as an HTTP MCP server
                    return [
                        new (vscode as any).McpHttpServerDefinition(
                            'Aura Codebase Context',
                            vscode.Uri.parse(`${apiUrl}/mcp`),
                            {}, // headers
                            '1.2.0'
                        )
                    ];
                },
                
                resolveMcpServerDefinition: async (server: any, _token: vscode.CancellationToken) => server
            });
            
            console.log('Aura MCP server provider registered');
        } else {
            console.log('MCP API not available - GitHub Copilot integration disabled');
        }
    } catch (err) {
        console.warn('Failed to register MCP provider:', err);
    }

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
        clearAndReindexCommand,
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
        findImplementationsCommand,
        findCallersCommand,
        showTypeMembersCommand,
        createStoryFromIssueCommand,
        refreshFromIssueCommand,
        postUpdateToIssueCommand,
        openStoryWorktreeCommand,
        showCurrentStoryCommand,
        configWatcher
    );
    
    // Add MCP provider if available
    if (mcpProvider) {
        context.subscriptions.push(mcpProvider);
    }

    // Initial refresh
    await refreshAll();

    // Check workspace onboarding status and set context for view visibility
    await checkOnboardingStatus();

    // Check if this workspace is a workflow worktree and auto-open the panel
    await checkAndOpenWorkflowForWorktree();

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

    try {
        // Check if workspace is already onboarded
        const status = await auraApiService.getWorkspaceStatus(workspacePath);
        
        if (!status.isOnboarded) {
            // Not onboarded - use onboardWorkspace to create workspace record and start indexing
            const result = await auraApiService.onboardWorkspace(workspacePath, {
                excludePatterns: ['**/node_modules/**', '**/bin/**', '**/obj/**', '**/.git/**']
            });
            
            if (result.success && result.jobId) {
                vscode.window.showInformationMessage(`Indexing started (Job: ${result.jobId.slice(0, 8)}...)`);
                updateIndexingStatusBar(result.jobId);
                
                // Update context
                await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
                await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
            }
        } else {
            // Already onboarded - trigger a re-index through workspace API
            const result = await auraApiService.reindexWorkspace(workspacePath);
            vscode.window.showInformationMessage(`Re-indexing started (Job: ${result.jobId.slice(0, 8)}...)`);
            updateIndexingStatusBar(result.jobId);
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to start indexing: ${message}`);
    }
}

/**
 * Check if the current workspace is onboarded and set VS Code context accordingly.
 * This controls which views are shown (welcome vs workflows).
 * For worktrees, we check the parent repository's onboarding status.
 */
async function checkOnboardingStatus(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        // No workspace - show welcome to guide user
        console.log('[Aura] No workspace folders, setting not onboarded');
        await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
        await vscode.commands.executeCommand('setContext', 'aura.isWorktree', false);
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;
    console.log('[Aura] Checking onboarding status for:', workspacePath);

    // Check if this is a worktree and get the canonical repository path
    const repoInfo = await gitService.getRepositoryInfo(workspacePath);
    const canonicalPath = repoInfo?.canonicalPath ?? workspacePath;
    const isWorktree = repoInfo?.isWorktree ?? false;

    await vscode.commands.executeCommand('setContext', 'aura.isWorktree', isWorktree);

    if (isWorktree) {
        console.log('[Aura] Workspace is a worktree, using parent path:', canonicalPath);
    }

    try {
        const status = await auraApiService.getWorkspaceStatus(canonicalPath);
        console.log('[Aura] API returned status:', JSON.stringify(status));
        
        if (status.isOnboarded) {
            console.log('[Aura] Workspace IS onboarded, hiding welcome view');
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
            if (isWorktree) {
                console.log(`Worktree using parent index: ${canonicalPath} (${status.stats.files} files, ${status.stats.chunks} chunks)`);
            } else {
                console.log(`Workspace onboarded: ${canonicalPath} (${status.stats.files} files, ${status.stats.chunks} chunks)`);
            }
        } else {
            console.log('[Aura] Workspace NOT onboarded, showing welcome view');
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
            console.log(`Workspace not onboarded: ${canonicalPath}`);
        }
    } catch (error) {
        // If API call fails, assume not onboarded (will show welcome view)
        console.log('[Aura] Failed to check onboarding status:', error);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', false);
        await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', true);
    }
}

/**
 * Check if the current workspace is a workflow worktree and auto-open the workflow panel.
 * This provides the "continue existing story" experience.
 */
async function checkAndOpenWorkflowForWorktree(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;
    console.log('[Aura] Checking if workspace is a workflow worktree:', workspacePath);

    try {
        const workflow = await auraApiService.getWorkflowByPath(workspacePath);
        if (workflow) {
            console.log('[Aura] Found workflow for worktree:', workflow.id, workflow.title);
            
            // Find current/next step
            const steps = workflow.steps || [];
            const currentStep = steps.find((s: any) => s.status === 'Running' || s.status === 'Pending');
            const completedCount = steps.filter((s: any) => s.status === 'Completed').length;
            const totalCount = steps.length;
            
            // Build message with progress
            let message = `📖 ${workflow.title}`;
            if (totalCount > 0) {
                message += ` (${completedCount}/${totalCount} steps)`;
            }
            if (currentStep) {
                message += `\n→ Next: ${currentStep.name}`;
            }

            // Show notification with action buttons
            const action = await vscode.window.showInformationMessage(
                message,
                'Open Story Panel',
                'Dismiss'
            );

            if (action === 'Open Story Panel') {
                await workflowPanelProvider.openWorkflowPanel(workflow.id);
            }
        } else {
            console.log('[Aura] No workflow found for this path');
        }
    } catch (error) {
        // Silently fail - this is an enhancement, not critical
        console.log('[Aura] Failed to check for workflow worktree:', error);
    }
}

/**
 * Show the current story panel for this workspace.
 * Called via command palette or keyboard shortcut.
 */
async function showCurrentStory(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('No workspace folder open');
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;

    try {
        const workflow = await auraApiService.getWorkflowByPath(workspacePath);
        if (workflow) {
            await workflowPanelProvider.openWorkflowPanel(workflow.id);
        } else {
            // No workflow for this path - offer to pick from list or create new
            const items: vscode.QuickPickItem[] = [
                { label: '$(add) Create New Story', description: 'Start a new development story' },
                { label: '$(list-unordered) Browse All Stories', description: 'View existing stories' }
            ];

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'No active story for this workspace'
            });

            if (selected?.label.includes('Create New')) {
                await vscode.commands.executeCommand('aura.createWorkflow');
            } else if (selected?.label.includes('Browse All')) {
                // Focus the workflows sidebar
                await vscode.commands.executeCommand('aura.workflows.focus');
            }
        }
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to find current story: ${error}`);
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
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('No workspace folder open');
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;
    const workspaceName = workspaceFolders[0].name;

    const confirm = await vscode.window.showWarningMessage(
        `Clear all indexed data for "${workspaceName}"? This cannot be undone.`,
        { modal: true },
        'Clear Index'
    );

    if (confirm !== 'Clear Index') return;

    try {
        await auraApiService.removeWorkspace(workspacePath);
        vscode.window.showInformationMessage(`Index cleared for ${workspaceName}`);
        statusTreeProvider.refresh();
        await checkOnboardingStatus();
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to clear index: ${message}`);
    }
}

async function clearAndReindex(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('No workspace folder open');
        return;
    }

    const workspacePath = workspaceFolders[0].uri.fsPath;
    const workspaceName = workspaceFolders[0].name;

    const confirm = await vscode.window.showWarningMessage(
        `Clear and reindex "${workspaceName}"? This will delete all indexed data and rebuild from scratch.`,
        { modal: true },
        'Clear & Reindex'
    );

    if (confirm !== 'Clear & Reindex') return;

    try {
        // First, try to remove existing workspace (ignore if not found)
        try {
            await auraApiService.removeWorkspace(workspacePath);
        } catch {
            // Workspace may not exist yet, that's fine
        }

        // Now onboard fresh
        const result = await auraApiService.onboardWorkspace(workspacePath, {
            excludePatterns: ['**/node_modules/**', '**/bin/**', '**/obj/**', '**/.git/**']
        });

        if (result.success && result.jobId) {
            vscode.window.showInformationMessage(`Reindexing started (Job: ${result.jobId.slice(0, 8)}...)`);
            updateIndexingStatusBar(result.jobId);
            
            await vscode.commands.executeCommand('setContext', 'aura.workspaceOnboarded', true);
            await vscode.commands.executeCommand('setContext', 'aura.workspaceNotOnboarded', false);
        }
        
        statusTreeProvider.refresh();
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to clear and reindex: ${message}`);
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
        
        // Check if indexing is in progress and adjust refresh rate
        const ragStatus = healthCheckService.getStatuses().rag;
        const isIndexing = ragStatus?.isIndexing === true;
        adjustRefreshRate(isIndexing);
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
    statusBarItem.tooltip = `Aura: ${status} (click to show current story)`;
}

function setupAutoRefresh(): void {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = undefined;
    }

    const config = vscode.workspace.getConfiguration('aura');
    const autoRefresh = config.get<boolean>('autoRefresh', true);

    if (autoRefresh) {
        currentRefreshRate = NORMAL_REFRESH_RATE;
        refreshInterval = setInterval(refreshAll, currentRefreshRate);
    }
}

function adjustRefreshRate(isIndexing: boolean): void {
    const config = vscode.workspace.getConfiguration('aura');
    const autoRefresh = config.get<boolean>('autoRefresh', true);
    if (!autoRefresh) return;

    const targetRate = isIndexing ? FAST_REFRESH_RATE : NORMAL_REFRESH_RATE;
    
    // Only change if rate is different
    if (currentRefreshRate !== targetRate) {
        if (refreshInterval) {
            clearInterval(refreshInterval);
        }
        currentRefreshRate = targetRate;
        refreshInterval = setInterval(refreshAll, currentRefreshRate);
        console.log(`[Aura] Adjusted refresh rate to ${currentRefreshRate}ms (indexing: ${isIndexing})`);
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
// Story/Issue Functions
// =====================

async function createStoryFromIssue(): Promise<void> {
    // Prompt for GitHub issue URL
    const issueUrl = await vscode.window.showInputBox({
        prompt: 'Enter GitHub issue URL',
        placeHolder: 'https://github.com/owner/repo/issues/123',
        validateInput: (value) => {
            if (!value) {
                return 'Issue URL is required';
            }
            if (!value.match(/github\.com\/[^/]+\/[^/]+\/issues\/\d+/i)) {
                return 'Invalid GitHub issue URL format';
            }
            return null;
        }
    });

    if (!issueUrl) {
        return;
    }

    // Get current workspace path
    const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

    try {
        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                title: 'Creating story from issue...',
                cancellable: false
            },
            async () => {
                const workflow = await auraApiService.createStoryFromIssue(
                    issueUrl,
                    workspacePath
                );

                workflowTreeProvider.refresh();

                // Auto-open worktree in new VS Code window
                if (workflow.worktreePath) {
                    const action = await vscode.window.showInformationMessage(
                        `Created story: ${workflow.title}`,
                        'Open in New Window',
                        'Stay Here'
                    );
                    if (action === 'Open in New Window') {
                        await vscode.commands.executeCommand(
                            'vscode.openFolder',
                            vscode.Uri.file(workflow.worktreePath),
                            { forceNewWindow: true }
                        );
                    }
                } else {
                    vscode.window.showInformationMessage(`Created story: ${workflow.title}`);
                }
            }
        );
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to create story: ${message}`);
    }
}

async function refreshFromIssue(workflowId?: string): Promise<void> {
    if (!workflowId) {
        vscode.window.showErrorMessage('No story selected');
        return;
    }

    try {
        const result = await auraApiService.refreshFromIssue(workflowId);
        
        if (result.updated) {
            workflowTreeProvider.refresh();
            vscode.window.showInformationMessage(`Story updated: ${result.changes.join(', ')}`);
        } else {
            vscode.window.showInformationMessage('Story is already up to date');
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to refresh from issue: ${message}`);
    }
}

async function postUpdateToIssue(workflowId?: string): Promise<void> {
    if (!workflowId) {
        vscode.window.showErrorMessage('No story selected');
        return;
    }

    const message = await vscode.window.showInputBox({
        prompt: 'Enter update message to post to the issue',
        placeHolder: 'Progress update...'
    });

    if (!message) {
        return;
    }

    try {
        await auraApiService.postUpdateToIssue(workflowId, message);
        vscode.window.showInformationMessage('Update posted to issue');
    } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        vscode.window.showErrorMessage(`Failed to post update: ${errorMessage}`);
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

// =====================
// Code Graph Query Functions
// =====================

async function findImplementations(): Promise<void> {
    const typeName = await vscode.window.showInputBox({
        prompt: 'Enter interface or abstract class name',
        placeHolder: 'IWorkflowService'
    });

    if (!typeName) return;

    try {
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        const implementations = await auraApiService.findImplementations(typeName, workspacePath);

        if (implementations.length === 0) {
            vscode.window.showInformationMessage(`No implementations found for ${typeName}`);
            return;
        }

        const selected = await vscode.window.showQuickPick(
            implementations.map(impl => ({
                label: impl.name,
                description: impl.filePath ? impl.filePath.replace(/.*[\\/]/, '') : '',
                detail: impl.lineNumber ? `Line ${impl.lineNumber}` : impl.fullName,
                impl
            })),
            { placeHolder: `Implementations of ${typeName} (${implementations.length} found)` }
        );

        if (selected && selected.impl.filePath) {
            const doc = await vscode.workspace.openTextDocument(selected.impl.filePath);
            const line = (selected.impl.lineNumber || 1) - 1;
            await vscode.window.showTextDocument(doc, {
                selection: new vscode.Range(line, 0, line, 0)
            });
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to find implementations';
        vscode.window.showErrorMessage(message);
    }
}

async function findCallers(): Promise<void> {
    const methodName = await vscode.window.showInputBox({
        prompt: 'Enter method name',
        placeHolder: 'ExecuteAsync'
    });

    if (!methodName) return;

    const containingType = await vscode.window.showInputBox({
        prompt: 'Enter containing type (optional)',
        placeHolder: 'WorkflowService'
    });

    try {
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        const callers = await auraApiService.findCallers(methodName, containingType || undefined, workspacePath);

        if (callers.length === 0) {
            vscode.window.showInformationMessage(`No callers found for ${methodName}`);
            return;
        }

        const selected = await vscode.window.showQuickPick(
            callers.map(caller => ({
                label: caller.name,
                description: caller.filePath ? caller.filePath.replace(/.*[\\/]/, '') : '',
                detail: caller.signature || caller.fullName,
                caller
            })),
            { placeHolder: `Callers of ${methodName} (${callers.length} found)` }
        );

        if (selected && selected.caller.filePath) {
            const doc = await vscode.workspace.openTextDocument(selected.caller.filePath);
            const line = (selected.caller.lineNumber || 1) - 1;
            await vscode.window.showTextDocument(doc, {
                selection: new vscode.Range(line, 0, line, 0)
            });
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to find callers';
        vscode.window.showErrorMessage(message);
    }
}

async function showTypeMembers(): Promise<void> {
    const typeName = await vscode.window.showInputBox({
        prompt: 'Enter type name',
        placeHolder: 'WorkflowService'
    });

    if (!typeName) return;

    try {
        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        const members = await auraApiService.getTypeMembers(typeName, workspacePath);

        if (members.length === 0) {
            vscode.window.showInformationMessage(`No members found for ${typeName}`);
            return;
        }

        const selected = await vscode.window.showQuickPick(
            members.map(member => ({
                label: `$(${getSymbolIcon(member.nodeType)}) ${member.name}`,
                description: member.nodeType || '',
                detail: member.signature || member.fullName,
                member
            })),
            { placeHolder: `Members of ${typeName} (${members.length} found)` }
        );

        if (selected && selected.member.filePath) {
            const doc = await vscode.workspace.openTextDocument(selected.member.filePath);
            const line = (selected.member.lineNumber || 1) - 1;
            await vscode.window.showTextDocument(doc, {
                selection: new vscode.Range(line, 0, line, 0)
            });
        }
    } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to get type members';
        vscode.window.showErrorMessage(message);
    }
}

function getSymbolIcon(nodeType?: string): string {
    switch (nodeType?.toLowerCase()) {
        case 'method': return 'symbol-method';
        case 'property': return 'symbol-property';
        case 'field': return 'symbol-field';
        case 'constructor': return 'symbol-constructor';
        case 'event': return 'symbol-event';
        default: return 'symbol-misc';
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
