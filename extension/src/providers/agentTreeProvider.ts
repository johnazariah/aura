import * as vscode from 'vscode';
import * as path from 'path';
import { AuraApiService, AgentInfo } from '../services/auraApiService';

// Extension context for resource paths
let extensionPath: string = '';

export function setExtensionPath(extPath: string): void {
    extensionPath = extPath;
}

type TreeNode = CapabilityGroup | SubCapabilityGroup | AgentItem | PlaceholderItem;

// Define parent-child capability relationships
const CAPABILITY_HIERARCHY: Record<string, string[]> = {
    'coding': ['csharp-coding', 'fsharp-coding', 'python-coding', 'typescript-coding', 'javascript-coding', 'go-coding', 'rust-coding'],
    'ingest': [
        'ingest:cs', 'ingest:csx',      // C#
        'ingest:fs', 'ingest:fsx',      // F#
        'ingest:py',                     // Python
        'ingest:ts', 'ingest:js',       // TypeScript/JavaScript
        'ingest:go',                     // Go
        'ingest:rs',                     // Rust
        'ingest:txt', 'ingest:md', 'ingest:rst', 'ingest:adoc', 'ingest:log',  // Text
        'ingest:*',                      // Fallback
    ],
};

// Reverse lookup: child -> parent
const CHILD_TO_PARENT: Record<string, string> = {};
for (const [parent, children] of Object.entries(CAPABILITY_HIERARCHY)) {
    for (const child of children) {
        CHILD_TO_PARENT[child] = parent;
    }
}

export class AgentTreeProvider implements vscode.TreeDataProvider<TreeNode> {
    private _onDidChangeTreeData = new vscode.EventEmitter<TreeNode | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private agents: AgentInfo[] = [];
    private capabilityGroups: Map<string, CapabilityGroup> = new Map();

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: TreeNode): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: TreeNode): Promise<TreeNode[]> {
        if (!element) {
            return this.getRootChildren();
        }

        if (element instanceof CapabilityGroup) {
            return element.getChildren();
        }

        if (element instanceof SubCapabilityGroup) {
            return element.agents
                .sort((a, b) => a.priority - b.priority)
                .map(agent => new AgentItem(agent));
        }

        return [];
    }

    private async getRootChildren(): Promise<TreeNode[]> {
        try {
            this.agents = await this.apiService.getAgents();

            if (this.agents.length === 0) {
                return [new PlaceholderItem('No agents loaded', 'warning')];
            }

            // Filter out only echo agents - keep ingesters and fallback visible
            const userAgents = this.agents.filter(agent =>
                !agent.id.includes('echo')
            );

            // Build hierarchical structure
            // Top-level: parent capabilities and standalone capabilities
            // Children: language/file-specific sub-capabilities

            const topLevelMap = new Map<string, AgentInfo[]>();
            const subCapabilityMap = new Map<string, Map<string, AgentInfo[]>>();

            for (const agent of userAgents) {
                const capabilities = agent.capabilities || [];
                if (capabilities.length === 0) {
                    const list = topLevelMap.get('general') || [];
                    list.push(agent);
                    topLevelMap.set('general', list);
                    continue;
                }

                const primaryCap = capabilities[0];
                const parentCap = CHILD_TO_PARENT[primaryCap];

                if (parentCap) {
                    // This is a sub-capability - nest it
                    if (!subCapabilityMap.has(parentCap)) {
                        subCapabilityMap.set(parentCap, new Map());
                    }
                    const subMap = subCapabilityMap.get(parentCap)!;
                    const list = subMap.get(primaryCap) || [];
                    list.push(agent);
                    subMap.set(primaryCap, list);

                    // Ensure parent exists in top-level
                    if (!topLevelMap.has(parentCap)) {
                        topLevelMap.set(parentCap, []);
                    }
                } else {
                    // Top-level capability
                    const list = topLevelMap.get(primaryCap) || [];
                    list.push(agent);
                    topLevelMap.set(primaryCap, list);
                }
            }

            // Sort capabilities
            const priorityOrder = ['chat', 'coding', 'ingest', 'analysis', 'enrichment', 'fixing', 'review', 'documentation'];
            const sortedCapabilities = Array.from(topLevelMap.keys()).sort((a, b) => {
                const aIndex = priorityOrder.indexOf(a);
                const bIndex = priorityOrder.indexOf(b);
                if (aIndex !== -1 && bIndex !== -1) return aIndex - bIndex;
                if (aIndex !== -1) return -1;
                if (bIndex !== -1) return 1;
                return a.localeCompare(b);
            });

            this.capabilityGroups.clear();
            return sortedCapabilities.map(cap => {
                const group = new CapabilityGroup(
                    cap,
                    topLevelMap.get(cap) || [],
                    subCapabilityMap.get(cap) || new Map()
                );
                this.capabilityGroups.set(cap, group);
                return group;
            });
        } catch (error) {
            return [new PlaceholderItem('API Offline - Start Aura API to see agents', 'cloud-offline')];
        }
    }
}export class CapabilityGroup extends vscode.TreeItem {
    constructor(
        public readonly capability: string,
        public readonly agents: AgentInfo[],
        public readonly subCapabilities: Map<string, AgentInfo[]>
    ) {
        super(
            CapabilityGroup.formatCapabilityName(capability),
            vscode.TreeItemCollapsibleState.Expanded
        );

        const directAgentCount = agents.length;
        const subCapCount = subCapabilities.size;
        const totalAgents = directAgentCount + Array.from(subCapabilities.values()).reduce((sum, arr) => sum + arr.length, 0);

        if (subCapCount > 0) {
            this.description = `${subCapCount} specializations, ${totalAgents} agents`;
        } else {
            this.description = `${directAgentCount} agent${directAgentCount !== 1 ? 's' : ''}`;
        }

        this.iconPath = CapabilityGroup.getIcon(capability);
        this.tooltip = new vscode.MarkdownString(
            `### ${this.label}\n\n` +
            `**${totalAgents}** total agent${totalAgents !== 1 ? 's' : ''}\n\n` +
            (subCapCount > 0 ? `**${subCapCount}** language specializations` : '')
        );
        this.contextValue = 'capabilityGroup';
    }

    getChildren(): TreeNode[] {
        const children: TreeNode[] = [];

        // Add sub-capability groups first (sorted)
        const sortedSubCaps = Array.from(this.subCapabilities.keys()).sort();
        for (const subCap of sortedSubCaps) {
            const agents = this.subCapabilities.get(subCap)!;
            children.push(new SubCapabilityGroup(subCap, agents, this.capability));
        }

        // Then add direct agents (generic ones)
        const sortedAgents = [...this.agents].sort((a, b) => a.priority - b.priority);
        for (const agent of sortedAgents) {
            children.push(new AgentItem(agent));
        }

        return children;
    }

    private static formatCapabilityName(cap: string): string {
        if (cap === 'ingest') return 'Ingesting';
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

    private static getIcon(capability: string): vscode.ThemeIcon | { light: vscode.Uri; dark: vscode.Uri } {
        // Fallback to theme icons for non-language categories
        const iconMap: Record<string, [string, string]> = {
            'chat': ['comment-discussion', 'charts.blue'],
            'coding': ['code', 'charts.green'],
            'ingest': ['file-code', 'charts.purple'],
            'analysis': ['graph', 'charts.purple'],
            'enrichment': ['sparkle', 'charts.orange'],
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

export class SubCapabilityGroup extends vscode.TreeItem {
    constructor(
        public readonly subCapability: string,
        public readonly agents: AgentInfo[],
        public readonly parentCapability: string
    ) {
        super(
            SubCapabilityGroup.formatSubCapabilityName(subCapability, parentCapability),
            vscode.TreeItemCollapsibleState.Collapsed
        );

        const agentCount = agents.length;
        this.description = `${agentCount} agent${agentCount !== 1 ? 's' : ''}`;
        this.iconPath = SubCapabilityGroup.getIcon(subCapability);
        this.tooltip = new vscode.MarkdownString(
            `### ${this.label}\n\n` +
            `**${agentCount}** agent${agentCount !== 1 ? 's' : ''} available`
        );
        this.contextValue = 'subCapabilityGroup';
    }

    private static formatSubCapabilityName(subCap: string, parentCap: string): string {
        // For ingest capabilities like "ingest:cs" -> "C#"
        if (subCap.startsWith('ingest:')) {
            const ext = subCap.substring(7);
            const extMap: Record<string, string> = {
                'cs': 'C#', 'csx': 'C# Script',
                'fs': 'F#', 'fsx': 'F# Script',
                'py': 'Python',
                'ts': 'TypeScript', 'js': 'JavaScript',
                'go': 'Go',
                'rs': 'Rust',
                '*': 'Fallback',
            };
            return extMap[ext] || ext.toUpperCase();
        }

        // For coding capabilities like "csharp-coding" -> "C#"
        const lang = subCap.replace('-coding', '').replace('-', '');
        const langMap: Record<string, string> = {
            'csharp': 'C#',
            'fsharp': 'F#',
            'python': 'Python',
            'typescript': 'TypeScript',
            'javascript': 'JavaScript',
            'go': 'Go',
            'rust': 'Rust',
        };
        return langMap[lang] || lang.charAt(0).toUpperCase() + lang.slice(1);
    }

    private static getIcon(subCap: string): vscode.ThemeIcon | { light: vscode.Uri; dark: vscode.Uri } {
        // Map sub-capability to language for icon
        let lang: string | null = null;

        if (subCap.startsWith('ingest:')) {
            const extMap: Record<string, string> = {
                'cs': 'csharp', 'csx': 'csharp',
                'fs': 'fsharp', 'fsx': 'fsharp',
                'py': 'python',
                'ts': 'typescript', 'js': 'typescript',
                'go': 'go',
                'rs': 'rust',
            };
            lang = extMap[subCap.substring(7)];
        } else if (subCap.endsWith('-coding')) {
            lang = subCap.replace('-coding', '');
        }

        if (lang && extensionPath) {
            const iconName = lang;
            const iconFilePath = path.join(extensionPath, 'resources', 'languages', `${iconName}.svg`);
            return {
                light: vscode.Uri.file(iconFilePath),
                dark: vscode.Uri.file(iconFilePath)
            };
        }

        return new vscode.ThemeIcon('symbol-misc', new vscode.ThemeColor('charts.foreground'));
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
        // All agents get the robot icon
        return new vscode.ThemeIcon('robot', new vscode.ThemeColor('charts.foreground'));
    }    private getTooltip(): vscode.MarkdownString {
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
