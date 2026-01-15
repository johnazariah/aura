import * as vscode from 'vscode';
import { AuraApiService, Workflow } from '../services/auraApiService';

export class WorkflowTreeProvider implements vscode.TreeDataProvider<WorkflowTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<WorkflowTreeItem | undefined | null | void> = new vscode.EventEmitter<WorkflowTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<WorkflowTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: WorkflowTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: WorkflowTreeItem): Promise<WorkflowTreeItem[]> {
        try {
            if (!element) {
                // Root level: show workflows
                return this.getRootItems();
            }

            if (element.contextValue === 'workflow' && element.workflow) {
                // Show steps under workflow
                return this.getWorkflowChildren(element.workflow);
            }

            return [];
        } catch (error) {
            console.error('Error getting workflow tree children:', error);
            return [new WorkflowTreeItem('Error loading data', vscode.TreeItemCollapsibleState.None, 'error')];
        }
    }

    private async getRootItems(): Promise<WorkflowTreeItem[]> {
        const items: WorkflowTreeItem[] = [];

        try {
            // Get all workflows
            const baseUrl = this.apiService.getBaseUrl();
            console.log(`[Aura] Fetching workflows from ${baseUrl}`);

            // Filter workflows by current workspace
            const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            const workflows = await this.apiService.getWorkflows(undefined, workspacePath);            if (workflows.length === 0) {
                items.push(new WorkflowTreeItem(
                    'No workflows yet',
                    vscode.TreeItemCollapsibleState.None,
                    'empty'
                ));
                items[0].description = 'Create one with Aura: Create Workflow';
            } else {
                for (const workflow of workflows) {
                    const item = this.createWorkflowItem(workflow);
                    items.push(item);
                }
            }
        } catch (error) {
            const baseUrl = this.apiService.getBaseUrl();
            console.error(`[Aura] Failed to fetch workflows from ${baseUrl}:`, error);
            
            const errorMessage = error instanceof Error ? error.message : String(error);
            const item = new WorkflowTreeItem(
                'Unable to connect to Aura API',
                vscode.TreeItemCollapsibleState.None,
                'offline'
            );
            item.tooltip = `Failed to connect to ${baseUrl}\n\nError: ${errorMessage}\n\nCheck that Aura is running and the aura.apiUrl setting is correct.`;
            items.push(item);
        }

        return items;
    }

    private createWorkflowItem(workflow: Workflow): WorkflowTreeItem {
        const hasSteps = workflow.steps && workflow.steps.length > 0;
        const collapsible = hasSteps
            ? vscode.TreeItemCollapsibleState.Expanded
            : vscode.TreeItemCollapsibleState.Collapsed;

        const item = new WorkflowTreeItem(
            workflow.title,
            collapsible,
            'workflow'
        );

        item.workflow = workflow;
        item.workflowId = workflow.id;
        item.description = workflow.status;
        item.iconPath = this.getStatusIcon(workflow.status);
        item.tooltip = this.getWorkflowTooltip(workflow);

        // Click opens worktree in new window
        item.command = {
            command: 'aura.openStoryWorktree',
            title: 'Open Worktree',
            arguments: [workflow.worktreePath]
        };

        return item;
    }

    private getWorkflowChildren(workflow: Workflow): WorkflowTreeItem[] {
        const items: WorkflowTreeItem[] = [];

        // Add mode indicator
        if (workflow.mode) {
            const modeIcon = workflow.mode === 'Conversational' ? 'comment-discussion' : 'list-ordered';
            const modeItem = new WorkflowTreeItem(
                `Mode: ${workflow.mode}`,
                vscode.TreeItemCollapsibleState.None,
                'info'
            );
            modeItem.iconPath = new vscode.ThemeIcon(modeIcon);
            items.push(modeItem);
        }

        // Add issue link if exists
        if (workflow.issueUrl && workflow.issueNumber) {
            const issueItem = new WorkflowTreeItem(
                `Issue #${workflow.issueNumber}`,
                vscode.TreeItemCollapsibleState.None,
                'issue'
            );
            issueItem.iconPath = new vscode.ThemeIcon('github');
            issueItem.tooltip = workflow.issueUrl;
            issueItem.command = {
                command: 'vscode.open',
                title: 'Open Issue',
                arguments: [vscode.Uri.parse(workflow.issueUrl)]
            };
            items.push(issueItem);
        }

        // Add branch if exists
        if (workflow.gitBranch) {
            const branchItem = new WorkflowTreeItem(
                `Branch: ${workflow.gitBranch}`,
                vscode.TreeItemCollapsibleState.None,
                'info'
            );
            branchItem.iconPath = new vscode.ThemeIcon('git-branch');
            items.push(branchItem);
        }

        // Add worktree path if exists (with action to open)
        if (workflow.worktreePath) {
            const worktreeItem = new WorkflowTreeItem(
                `Worktree`,
                vscode.TreeItemCollapsibleState.None,
                'worktree'
            );
            worktreeItem.iconPath = new vscode.ThemeIcon('folder-opened');
            worktreeItem.tooltip = `Open in new window: ${workflow.worktreePath}`;
            worktreeItem.workflowId = workflow.id;
            worktreeItem.command = {
                command: 'aura.openStoryWorktree',
                title: 'Open Worktree',
                arguments: [workflow.worktreePath]
            };
            items.push(worktreeItem);
        }

        // Add steps
        if (workflow.steps && workflow.steps.length > 0) {
            for (const step of workflow.steps) {
                const stepItem = new WorkflowTreeItem(
                    `${step.order}. ${step.name}`,
                    vscode.TreeItemCollapsibleState.None,
                    'step'
                );
                stepItem.step = step;
                stepItem.workflowId = workflow.id;
                stepItem.description = step.capability;
                stepItem.iconPath = this.getStepIcon(step.status);
                stepItem.tooltip = `${step.name}\nCapability: ${step.capability}\nStatus: ${step.status}`;

                // Allow executing pending steps
                if (step.status === 'Pending') {
                    stepItem.command = {
                        command: 'aura.executeStep',
                        title: 'Execute Step',
                        arguments: [workflow.id, step.id]
                    };
                }

                items.push(stepItem);
            }
        }

        return items;
    }

    private getStatusIcon(status: string): vscode.ThemeIcon {
        switch (status) {
            case 'Created':
                return new vscode.ThemeIcon('circle-outline');
            case 'Analyzing':
            case 'Planning':
                return new vscode.ThemeIcon('sync~spin');
            case 'Analyzed':
            case 'Planned':
                return new vscode.ThemeIcon('checklist');
            case 'Executing':
                return new vscode.ThemeIcon('play-circle');
            case 'Completed':
                return new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
            case 'Failed':
                return new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
            case 'Cancelled':
                return new vscode.ThemeIcon('circle-slash');
            default:
                return new vscode.ThemeIcon('circle-outline');
        }
    }

    private getStepIcon(status: string): vscode.ThemeIcon {
        switch (status) {
            case 'Pending':
                return new vscode.ThemeIcon('circle-outline');
            case 'Running':
                return new vscode.ThemeIcon('sync~spin');
            case 'Completed':
                return new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
            case 'Failed':
                return new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
            case 'Skipped':
                return new vscode.ThemeIcon('debug-step-over');
            default:
                return new vscode.ThemeIcon('circle-outline');
        }
    }

    private getWorkflowTooltip(workflow: Workflow): string {
        let tooltip = `${workflow.title}\n`;
        if (workflow.description) {
            tooltip += `\n${workflow.description}\n`;
        }
        tooltip += `\nStatus: ${workflow.status}`;
        if (workflow.mode) {
            tooltip += `\nMode: ${workflow.mode}`;
        }
        if (workflow.issueUrl) {
            tooltip += `\nIssue: ${workflow.issueUrl}`;
        }
        if (workflow.repositoryPath) {
            tooltip += `\nRepository: ${workflow.repositoryPath}`;
        }
        if (workflow.gitBranch) {
            tooltip += `\nBranch: ${workflow.gitBranch}`;
        }
        if (workflow.worktreePath) {
            tooltip += `\nWorktree: ${workflow.worktreePath}`;
        }
        return tooltip;
    }
}

export class WorkflowTreeItem extends vscode.TreeItem {
    workflow?: Workflow;
    step?: { id: string; order: number; name: string; capability: string; status: string };
    workflowId?: string;

    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        public readonly contextValue: string
    ) {
        super(label, collapsibleState);
    }
}
