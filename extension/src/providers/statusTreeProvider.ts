import * as vscode from 'vscode';
import { HealthCheckService, ServiceStatus, OllamaStatus, RagStatus, OllamaModelInfo } from '../services/healthCheckService';

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
        if (!element) {
            // Root level - show main status items
            const statuses = this.healthCheckService.getStatuses();
            return Promise.resolve([
                this.createStatusItem('Aura API', statuses.api),
                this.createOllamaItem(statuses.ollama),
                this.createStatusItem('Database', statuses.database),
                this.createRagItem(statuses.rag)
            ]);
        }

        // Children for expandable items
        if (element.itemType === 'ollama' && element.ollamaStatus) {
            return Promise.resolve(this.getOllamaChildren(element.ollamaStatus));
        }

        if (element.itemType === 'rag' && element.ragStatus) {
            return Promise.resolve(this.getRagChildren(element.ragStatus));
        }

        if (element.itemType === 'ollama-models' && element.ollamaStatus) {
            return Promise.resolve(this.getModelChildren(element.ollamaStatus));
        }

        return Promise.resolve([]);
    }

    private createStatusItem(name: string, status: ServiceStatus): StatusItem {
        return new StatusItem(name, status, 'service');
    }

    private createOllamaItem(status: OllamaStatus): StatusItem {
        const hasDetails = status.status === 'healthy' && (status.models?.length || 0) > 0;
        return new StatusItem(
            'Ollama',
            status,
            'ollama',
            hasDetails ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None,
            status
        );
    }

    private createRagItem(status: RagStatus): StatusItem {
        const hasDetails = status.status === 'healthy' && (status.totalDocuments || 0) > 0;
        const item = new StatusItem(
            'RAG Index',
            status,
            'rag',
            hasDetails ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None,
            undefined,
            status
        );
        item.contextValue = 'rag';
        return item;
    }

    private getOllamaChildren(status: OllamaStatus): StatusItem[] {
        const children: StatusItem[] = [];

        // Running models section
        if (status.runningModels && status.runningModels.length > 0) {
            const totalVram = status.runningModels.reduce((sum, m) => sum + m.sizeVram, 0);
            const runningItem = new StatusItem(
                `Loaded in VRAM`,
                { status: 'healthy', details: `${status.runningModels.length} model(s), ${this.formatBytes(totalVram)}` },
                'info'
            );
            runningItem.iconPath = new vscode.ThemeIcon('pulse', new vscode.ThemeColor('charts.green'));
            children.push(runningItem);

            // List each running model
            for (const model of status.runningModels) {
                const modelItem = new StatusItem(
                    model.name,
                    { status: 'healthy', details: this.formatBytes(model.sizeVram) },
                    'running-model'
                );
                modelItem.iconPath = new vscode.ThemeIcon('vm-running', new vscode.ThemeColor('charts.green'));
                children.push(modelItem);
            }
        } else {
            const noLoadedItem = new StatusItem(
                'No models loaded',
                { status: 'unknown', details: 'Models load on first use' },
                'info'
            );
            noLoadedItem.iconPath = new vscode.ThemeIcon('circle-outline');
            children.push(noLoadedItem);
        }

        // Available models section
        if (status.models && status.models.length > 0) {
            const totalSize = status.totalModelSize || 0;
            const modelsItem = new StatusItem(
                `Available Models`,
                { status: 'healthy', details: `${status.models.length} models, ${this.formatBytes(totalSize)} on disk` },
                'ollama-models',
                vscode.TreeItemCollapsibleState.Collapsed,
                status
            );
            modelsItem.iconPath = new vscode.ThemeIcon('database');
            children.push(modelsItem);
        }

        return children;
    }

    private getModelChildren(status: OllamaStatus): StatusItem[] {
        if (!status.models) return [];

        return status.models.map(model => {
            const item = new StatusItem(
                model.name,
                { 
                    status: 'healthy', 
                    details: `${model.parameterSize} • ${model.quantization} • ${this.formatBytes(model.size)}` 
                },
                'model'
            );
            item.iconPath = new vscode.ThemeIcon('symbol-misc');
            item.tooltip = new vscode.MarkdownString(
                `### ${model.name}\n\n` +
                `- **Parameters:** ${model.parameterSize}\n` +
                `- **Quantization:** ${model.quantization}\n` +
                `- **Family:** ${model.family}\n` +
                `- **Size:** ${this.formatBytes(model.size)}`
            );
            return item;
        });
    }

    private getRagChildren(status: RagStatus): StatusItem[] {
        const children: StatusItem[] = [];

        // Index Health indicator (freshness)
        if (status.indexHealth) {
            const healthItem = this.createHealthItem(status);
            children.push(healthItem);
        }

        // Code Graph stats (Roslyn semantic index)
        if (status.graphNodes !== undefined && status.graphNodes > 0) {
            const graphItem = new StatusItem(
                'Code Graph',
                { status: 'healthy', details: `${status.graphNodes} nodes, ${status.graphEdges || 0} edges` },
                'info'
            );
            graphItem.iconPath = new vscode.ThemeIcon('type-hierarchy', new vscode.ThemeColor('charts.blue'));
            children.push(graphItem);
        } else {
            const noGraphItem = new StatusItem(
                'Code Graph',
                { status: 'unknown', details: 'Not indexed' },
                'info'
            );
            noGraphItem.iconPath = new vscode.ThemeIcon('type-hierarchy');
            children.push(noGraphItem);
        }

        // Symbols count (each class, method, section is a "document" in RAG terms)
        const docItem = new StatusItem(
            'Symbols',
            { status: 'healthy', details: `${status.totalDocuments || 0}` },
            'info'
        );
        docItem.iconPath = new vscode.ThemeIcon('symbol-class');
        children.push(docItem);

        // Embeddings count (vector representations for similarity search)
        const chunkItem = new StatusItem(
            'Embeddings',
            { status: 'healthy', details: `${status.totalChunks || 0}` },
            'info'
        );
        chunkItem.iconPath = new vscode.ThemeIcon('symbol-array');
        children.push(chunkItem);

        // Breakdown by type
        if (status.chunksByType && Object.keys(status.chunksByType).length > 0) {
            for (const [type, count] of Object.entries(status.chunksByType)) {
                const typeItem = new StatusItem(
                    type,
                    { status: 'healthy', details: `${count} chunks` },
                    'info'
                );
                typeItem.iconPath = this.getIconForContentType(type);
                children.push(typeItem);
            }
        }

        return children;
    }

    private createHealthItem(status: RagStatus): StatusItem {
        const health = status.indexHealth || 'not-indexed';
        let label: string;
        let icon: vscode.ThemeIcon;
        let details: string;

        switch (health) {
            case 'fresh':
                label = 'Index Fresh';
                icon = new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
                details = 'Up to date with latest commit';
                break;
            case 'stale':
                label = 'Index Stale';
                icon = new vscode.ThemeIcon('warning', new vscode.ThemeColor('charts.yellow'));
                details = `${status.commitsBehind || 0} commit(s) behind`;
                break;
            case 'outdated':
                label = 'Index Outdated';
                icon = new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.orange'));
                details = `${status.commitsBehind || 0} commit(s) behind - consider reindexing`;
                break;
            default:
                label = 'Not Indexed';
                icon = new vscode.ThemeIcon('circle-outline');
                details = 'Run indexing to enable RAG';
        }

        const item = new StatusItem(label, { status: 'healthy', details }, 'health');
        item.iconPath = icon;
        item.contextValue = 'indexHealth';
        return item;
    }

    private getIconForContentType(type: string): vscode.ThemeIcon {
        const iconMap: Record<string, string> = {
            'Markdown': 'markdown',
            'CSharp': 'file-code',
            'TypeScript': 'file-code',
            'JavaScript': 'file-code',
            'Python': 'file-code',
            'PlainText': 'file-text',
            'Json': 'json',
            'Yaml': 'file-code',
        };
        return new vscode.ThemeIcon(iconMap[type] || 'file');
    }

    private formatBytes(bytes: number): string {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }
}

export type StatusItemType = 'service' | 'ollama' | 'rag' | 'ollama-models' | 'model' | 'running-model' | 'info' | 'health';

export class StatusItem extends vscode.TreeItem {
    constructor(
        public readonly name: string,
        public readonly status: ServiceStatus,
        public readonly itemType: StatusItemType = 'service',
        collapsibleState: vscode.TreeItemCollapsibleState = vscode.TreeItemCollapsibleState.None,
        public readonly ollamaStatus?: OllamaStatus,
        public readonly ragStatus?: RagStatus
    ) {
        super(name, collapsibleState);

        this.description = this.getDescription();
        if (!this.iconPath) {
            this.iconPath = this.getIcon();
        }
        if (!this.tooltip) {
            this.tooltip = this.getTooltip();
        }
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
                return this.status.details || 'Unknown';
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