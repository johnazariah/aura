import * as vscode from 'vscode';
import { AuraApiService, AgentInfo } from '../services/auraApiService';

export class AgentTreeProvider implements vscode.TreeDataProvider<AgentItem> {
    private _onDidChangeTreeData = new vscode.EventEmitter<AgentItem | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: AgentItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: AgentItem): Promise<AgentItem[]> {
        if (element) {
            // No children for agent items (for now)
            return [];
        }

        try {
            const agents = await this.apiService.getAgents();
            return agents.map(agent => new AgentItem(agent));
        } catch (error) {
            // Return a placeholder when API is not available
            return [
                new AgentItem({
                    id: 'offline',
                    name: 'API Offline',
                    description: 'Connect to Aura API to see agents',
                    provider: '',
                    model: '',
                    tags: []
                }, true)
            ];
        }
    }
}

export class AgentItem extends vscode.TreeItem {
    constructor(
        public readonly agent: AgentInfo,
        isPlaceholder: boolean = false
    ) {
        super(agent.name, vscode.TreeItemCollapsibleState.None);

        if (isPlaceholder) {
            this.iconPath = new vscode.ThemeIcon('cloud-offline');
            this.description = '';
        } else {
            this.description = agent.model || agent.provider;
            this.iconPath = this.getIcon();
            this.tooltip = this.getTooltip();
            this.contextValue = 'agent';
        }
    }

    private getIcon(): vscode.ThemeIcon {
        // Choose icon based on agent tags
        const tags = this.agent.tags || [];
        
        if (tags.includes('coding') || tags.includes('code-generation')) {
            return new vscode.ThemeIcon('code', new vscode.ThemeColor('charts.blue'));
        }
        if (tags.includes('testing')) {
            return new vscode.ThemeIcon('beaker', new vscode.ThemeColor('charts.green'));
        }
        if (tags.includes('documentation')) {
            return new vscode.ThemeIcon('book', new vscode.ThemeColor('charts.orange'));
        }
        if (tags.includes('analysis') || tags.includes('business-analyst')) {
            return new vscode.ThemeIcon('graph', new vscode.ThemeColor('charts.purple'));
        }
        if (tags.includes('orchestration')) {
            return new vscode.ThemeIcon('organization', new vscode.ThemeColor('charts.yellow'));
        }
        
        return new vscode.ThemeIcon('robot', new vscode.ThemeColor('charts.foreground'));
    }

    private getTooltip(): vscode.MarkdownString {
        const md = new vscode.MarkdownString();
        md.appendMarkdown(`### ${this.agent.name}\n\n`);
        
        if (this.agent.description) {
            md.appendMarkdown(`${this.agent.description}\n\n`);
        }

        md.appendMarkdown(`**Provider:** ${this.agent.provider || 'ollama'}\n\n`);
        md.appendMarkdown(`**Model:** ${this.agent.model || 'qwen2.5-coder:7b'}\n\n`);
        
        if (this.agent.tags && this.agent.tags.length > 0) {
            md.appendMarkdown(`**Capabilities:** ${this.agent.tags.join(', ')}\n\n`);
        }

        return md;
    }
}
