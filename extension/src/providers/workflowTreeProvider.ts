import * as vscode from 'vscode';
import { AuraApiService, Issue, Workflow } from '../services/auraApiService';

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
                // Root level: show issues and workflows
                return this.getRootItems();
            }

            if (element.contextValue === 'issue' && element.issue) {
                // Show workflow info under issue
                return this.getIssueChildren(element.issue);
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
            // Get all issues
            const issues = await this.apiService.getIssues();

            if (issues.length === 0) {
                items.push(new WorkflowTreeItem(
                    'No issues yet',
                    vscode.TreeItemCollapsibleState.None,
                    'empty'
                ));
                items[0].description = 'Create one with Aura: Create Issue';
            } else {
                for (const issue of issues) {
                    const item = this.createIssueItem(issue);
                    items.push(item);
                }
            }
        } catch {
            items.push(new WorkflowTreeItem(
                'Unable to connect to Aura API',
                vscode.TreeItemCollapsibleState.None,
                'offline'
            ));
        }

        return items;
    }

    private createIssueItem(issue: Issue): WorkflowTreeItem {
        const hasWorkflow = issue.hasWorkflow;
        const collapsible = hasWorkflow
            ? vscode.TreeItemCollapsibleState.Expanded
            : vscode.TreeItemCollapsibleState.None;

        const item = new WorkflowTreeItem(
            issue.title,
            collapsible,
            'issue'
        );

        item.issue = issue;
        item.description = this.getStatusDescription(issue.status, issue.workflowStatus);
        item.iconPath = this.getStatusIcon(issue.status, issue.workflowStatus);
        item.tooltip = this.getIssueTooltip(issue);

        // Command to open workflow panel when clicked
        if (hasWorkflow && issue.workflowId) {
            item.command = {
                command: 'aura.openWorkflow',
                title: 'Open Workflow',
                arguments: [issue.workflowId]
            };
        }

        return item;
    }

    private async getIssueChildren(issue: Issue): Promise<WorkflowTreeItem[]> {
        if (!issue.workflowId) {
            return [];
        }

        try {
            const workflow = await this.apiService.getWorkflow(issue.workflowId);
            return this.getWorkflowChildren(workflow);
        } catch {
            return [];
        }
    }

    private getWorkflowChildren(workflow: Workflow): WorkflowTreeItem[] {
        const items: WorkflowTreeItem[] = [];

        // Add workflow status
        const statusItem = new WorkflowTreeItem(
            `Status: ${workflow.status}`,
            vscode.TreeItemCollapsibleState.None,
            'info'
        );
        statusItem.iconPath = new vscode.ThemeIcon('info');
        items.push(statusItem);

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

    private getStatusDescription(issueStatus: string, workflowStatus?: string): string {
        if (workflowStatus) {
            return workflowStatus;
        }
        return issueStatus;
    }

    private getStatusIcon(issueStatus: string, workflowStatus?: string): vscode.ThemeIcon {
        const status = workflowStatus || issueStatus;

        switch (status) {
            case 'Open':
                return new vscode.ThemeIcon('circle-outline');
            case 'InProgress':
            case 'Created':
            case 'Digesting':
            case 'Planning':
                return new vscode.ThemeIcon('sync~spin');
            case 'Digested':
            case 'Planned':
                return new vscode.ThemeIcon('checklist');
            case 'Executing':
                return new vscode.ThemeIcon('play-circle');
            case 'Completed':
                return new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
            case 'Failed':
                return new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
            case 'Cancelled':
            case 'Closed':
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

    private getIssueTooltip(issue: Issue): string {
        let tooltip = `${issue.title}\n`;
        if (issue.description) {
            tooltip += `\n${issue.description}\n`;
        }
        tooltip += `\nStatus: ${issue.status}`;
        if (issue.workflowStatus) {
            tooltip += `\nWorkflow: ${issue.workflowStatus}`;
        }
        if (issue.repositoryPath) {
            tooltip += `\nRepository: ${issue.repositoryPath}`;
        }
        return tooltip;
    }
}

export class WorkflowTreeItem extends vscode.TreeItem {
    issue?: Issue;
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
