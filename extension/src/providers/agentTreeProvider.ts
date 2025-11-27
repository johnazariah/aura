import * as vscode from 'vscode';
import { AuraApiService, AgentInfo } from '../services/auraApiService';

type TreeNode = CapabilityGroup | AgentItem | PlaceholderItem;

export class AgentTreeProvider implements vscode.TreeDataProvider<TreeNode> {
    private _onDidChangeTreeData = new vscode.EventEmitter<TreeNode | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private agents: AgentInfo[] = [];

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: TreeNode): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: TreeNode): Promise<TreeNode[]> {
        if (!element) {
            // Root level: fetch agents and group by capability
            return this.getRootChildren();
        }

        if (element instanceof CapabilityGroup) {
            // Children of a capability group: agents with that capability
            return element.agents
                .sort((a, b) => a.priority - b.priority)  // Lower priority number = higher rank
                .map(agent => new AgentItem(agent));
        }

        // Agent items have no children
        return [];
    }

    private async getRootChildren(): Promise<TreeNode[]> {
        try {
            this.agents = await this.apiService.getAgents();
            
            if (this.agents.length === 0) {
                return [new PlaceholderItem('No agents loaded', 'warning')];
            }

            // Group agents by capability
            const capabilityMap = new Map<string, AgentInfo[]>();
            
            for (const agent of this.agents) {
                const capabilities = agent.capabilities || [];
                if (capabilities.length === 0) {
                    // Agents without capabilities go under "general"
                    const list = capabilityMap.get('general') || [];
                    list.push(agent);
                    capabilityMap.set('general', list);
                } else {
                    for (const cap of capabilities) {
                        const list = capabilityMap.get(cap) || [];
                        list.push(agent);
                        capabilityMap.set(cap, list);
                    }
                }
            }

            // Sort capabilities, but put common ones first
            const priorityOrder = ['chat', 'coding', 'analysis', 'digestion', 'fixing', 'review', 'documentation'];
            const sortedCapabilities = Array.from(capabilityMap.keys()).sort((a, b) => {
                const aIndex = priorityOrder.indexOf(a);
                const bIndex = priorityOrder.indexOf(b);
                if (aIndex !== -1 && bIndex !== -1) return aIndex - bIndex;
                if (aIndex !== -1) return -1;
                if (bIndex !== -1) return 1;
                return a.localeCompare(b);
            });

            return sortedCapabilities.map(cap => 
                new CapabilityGroup(cap, capabilityMap.get(cap)!)
            );
        } catch (error) {
            return [new PlaceholderItem('API Offline - Start Aura API to see agents', 'cloud-offline')];
        }
    }
}

export class CapabilityGroup extends vscode.TreeItem {
    constructor(
        public readonly capability: string,
        public readonly agents: AgentInfo[]
    ) {
        super(
            CapabilityGroup.formatCapabilityName(capability),
            vscode.TreeItemCollapsibleState.Expanded
        );

        const agentCount = agents.length;
        const bestAgent = agents.sort((a, b) => a.priority - b.priority)[0];
        
        this.description = `${agentCount} agent${agentCount !== 1 ? 's' : ''}`;
        this.iconPath = CapabilityGroup.getIcon(capability);
        this.tooltip = new vscode.MarkdownString(
            `### ${this.label}\n\n` +
            `**${agentCount}** agent${agentCount !== 1 ? 's' : ''} available\n\n` +
            `**Best:** ${bestAgent.name} (priority ${bestAgent.priority})`
        );
        this.contextValue = 'capabilityGroup';
    }

    private static formatCapabilityName(cap: string): string {
        // Convert "csharp-coding" to "C# Coding", "chat" to "Chat", etc.
        return cap
            .split('-')
            .map(word => {
                if (word === 'csharp') return 'C#';
                if (word === 'typescript') return 'TypeScript';
                if (word === 'javascript') return 'JavaScript';
                return word.charAt(0).toUpperCase() + word.slice(1);
            })
            .join(' ');
    }

    private static getIcon(capability: string): vscode.ThemeIcon {
        const iconMap: Record<string, [string, string]> = {
            'chat': ['comment-discussion', 'charts.blue'],
            'coding': ['code', 'charts.green'],
            'analysis': ['graph', 'charts.purple'],
            'digestion': ['file-text', 'charts.orange'],
            'fixing': ['wrench', 'charts.red'],
            'review': ['checklist', 'charts.yellow'],
            'documentation': ['book', 'charts.orange'],
            'testing': ['beaker', 'charts.green'],
            'general': ['robot', 'charts.foreground'],
        };

        const [icon, color] = iconMap[capability] || ['symbol-misc', 'charts.foreground'];
        return new vscode.ThemeIcon(icon, new vscode.ThemeColor(color));
    }
}

export class AgentItem extends vscode.TreeItem {
    constructor(public readonly agent: AgentInfo) {
        super(agent.name, vscode.TreeItemCollapsibleState.None);

        const priorityLabel = AgentItem.getPriorityLabel(agent.priority);
        const languageInfo = agent.languages?.length ? ` [${agent.languages.join(', ')}]` : '';
        
        this.description = `${priorityLabel}${languageInfo}`;
        this.iconPath = this.getIcon();
        this.tooltip = this.getTooltip();
        this.contextValue = 'agent';
        
        // Command to execute when clicking the agent
        this.command = {
            command: 'aura.selectAgent',
            title: 'Select Agent',
            arguments: [agent]
        };
    }

    private static getPriorityLabel(priority: number): string {
        if (priority <= 30) return '★★★';  // Specialist
        if (priority <= 50) return '★★';   // Standard
        if (priority <= 70) return '★';    // General
        return '○';                         // Fallback
    }

    private getIcon(): vscode.ThemeIcon {
        // Show different icon if agent has language specialization
        if (this.agent.languages && this.agent.languages.length > 0) {
            const lang = this.agent.languages[0];
            if (lang === 'csharp') return new vscode.ThemeIcon('symbol-class', new vscode.ThemeColor('charts.blue'));
            if (lang === 'python') return new vscode.ThemeIcon('symbol-method', new vscode.ThemeColor('charts.yellow'));
            if (lang === 'typescript' || lang === 'javascript') return new vscode.ThemeIcon('symbol-variable', new vscode.ThemeColor('charts.orange'));
        }
        
        return new vscode.ThemeIcon('robot', new vscode.ThemeColor('charts.foreground'));
    }

    private getTooltip(): vscode.MarkdownString {
        const md = new vscode.MarkdownString();
        md.appendMarkdown(`### ${this.agent.name}\n\n`);
        
        if (this.agent.description) {
            md.appendMarkdown(`${this.agent.description}\n\n`);
        }

        md.appendMarkdown(`---\n\n`);
        md.appendMarkdown(`| Property | Value |\n`);
        md.appendMarkdown(`|----------|-------|\n`);
        md.appendMarkdown(`| **Priority** | ${this.agent.priority} |\n`);
        md.appendMarkdown(`| **Provider** | ${this.agent.provider || 'ollama'} |\n`);
        md.appendMarkdown(`| **Model** | ${this.agent.model || 'default'} |\n`);
        
        if (this.agent.capabilities?.length) {
            md.appendMarkdown(`| **Capabilities** | ${this.agent.capabilities.join(', ')} |\n`);
        }
        
        if (this.agent.languages?.length) {
            md.appendMarkdown(`| **Languages** | ${this.agent.languages.join(', ')} |\n`);
        }

        if (this.agent.tags?.length) {
            md.appendMarkdown(`\n**Tags:** ${this.agent.tags.map(t => `\`${t}\``).join(' ')}\n`);
        }

        return md;
    }
}

export class PlaceholderItem extends vscode.TreeItem {
    constructor(message: string, icon: string) {
        super(message, vscode.TreeItemCollapsibleState.None);
        this.iconPath = new vscode.ThemeIcon(icon);
        this.contextValue = 'placeholder';
    }
}
