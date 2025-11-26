import * as vscode from 'vscode';
import { HealthCheckService, ServiceStatus } from '../services/healthCheckService';

export class StatusTreeProvider implements vscode.TreeDataProvider<StatusItem> {
    private _onDidChangeTreeData = new vscode.EventEmitter<StatusItem | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    constructor(private healthCheckService: HealthCheckService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: StatusItem): vscode.TreeItem {
        return element;
    }

    getChildren(element?: StatusItem): Thenable<StatusItem[]> {
        if (element) {
            // No children for status items
            return Promise.resolve([]);
        }

        const statuses = this.healthCheckService.getStatuses();
        return Promise.resolve([
            this.createStatusItem('Aura API', statuses.api),
            this.createStatusItem('Ollama', statuses.ollama),
            this.createStatusItem('Database', statuses.database),
            this.createStatusItem('RAG Index', statuses.rag)
        ]);
    }

    private createStatusItem(name: string, status: ServiceStatus): StatusItem {
        return new StatusItem(name, status);
    }
}

export class StatusItem extends vscode.TreeItem {
    constructor(
        public readonly name: string,
        public readonly status: ServiceStatus
    ) {
        super(name, vscode.TreeItemCollapsibleState.None);

        this.description = this.getDescription();
        this.iconPath = this.getIcon();
        this.tooltip = this.getTooltip();
    }

    private getDescription(): string {
        switch (this.status.status) {
            case 'healthy':
                return this.status.details || 'Connected';
            case 'unhealthy':
                return this.status.error || 'Not connected';
            case 'checking':
                return 'Checking...';
            case 'unknown':
            default:
                return 'Unknown';
        }
    }

    private getIcon(): vscode.ThemeIcon {
        switch (this.status.status) {
            case 'healthy':
                return new vscode.ThemeIcon('pass', new vscode.ThemeColor('testing.iconPassed'));
            case 'unhealthy':
                return new vscode.ThemeIcon('error', new vscode.ThemeColor('testing.iconFailed'));
            case 'checking':
                return new vscode.ThemeIcon('sync~spin');
            case 'unknown':
            default:
                return new vscode.ThemeIcon('question', new vscode.ThemeColor('testing.iconSkipped'));
        }
    }

    private getTooltip(): vscode.MarkdownString {
        const md = new vscode.MarkdownString();
        md.appendMarkdown(`### ${this.name}\n\n`);
        md.appendMarkdown(`**Status:** ${this.status.status}\n\n`);
        
        if (this.status.url) {
            md.appendMarkdown(`**URL:** ${this.status.url}\n\n`);
        }
        
        if (this.status.responseTime) {
            md.appendMarkdown(`**Response Time:** ${this.status.responseTime}ms\n\n`);
        }
        
        if (this.status.details) {
            md.appendMarkdown(`**Details:** ${this.status.details}\n\n`);
        }
        
        if (this.status.error) {
            md.appendMarkdown(`**Error:** ${this.status.error}\n\n`);
        }

        if (this.status.lastChecked) {
            md.appendMarkdown(`*Last checked: ${this.status.lastChecked.toLocaleTimeString()}*`);
        }

        return md;
    }
}
