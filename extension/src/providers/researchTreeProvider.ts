import * as vscode from 'vscode';
import { AuraApiService, ResearchSource, ResearchConcept } from '../services/auraApiService';

export class ResearchTreeProvider implements vscode.TreeDataProvider<ResearchTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ResearchTreeItem | undefined | null | void> = new vscode.EventEmitter<ResearchTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ResearchTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: ResearchTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: ResearchTreeItem): Promise<ResearchTreeItem[]> {
        try {
            if (!element) {
                return this.getRootItems();
            }

            if (element.contextValue === 'sourcesGroup') {
                return this.getSourceItems();
            }

            if (element.contextValue === 'conceptsGroup') {
                return this.getConceptItems();
            }

            if (element.contextValue === 'source' && element.source) {
                return this.getSourceDetails(element.source);
            }

            return [];
        } catch (error) {
            console.error('Error getting research tree children:', error);
            return [new ResearchTreeItem('Error loading data', vscode.TreeItemCollapsibleState.None, 'error')];
        }
    }

    private async getRootItems(): Promise<ResearchTreeItem[]> {
        const items: ResearchTreeItem[] = [];

        // Sources group
        const sourcesItem = new ResearchTreeItem(
            'Library',
            vscode.TreeItemCollapsibleState.Expanded,
            'sourcesGroup'
        );
        sourcesItem.iconPath = new vscode.ThemeIcon('library');
        items.push(sourcesItem);

        // Concepts group
        const conceptsItem = new ResearchTreeItem(
            'Concepts',
            vscode.TreeItemCollapsibleState.Collapsed,
            'conceptsGroup'
        );
        conceptsItem.iconPath = new vscode.ThemeIcon('symbol-class');
        items.push(conceptsItem);

        return items;
    }

    private async getSourceItems(): Promise<ResearchTreeItem[]> {
        const items: ResearchTreeItem[] = [];

        try {
            const sources = await this.apiService.getResearchSources();

            if (sources.length === 0) {
                const emptyItem = new ResearchTreeItem(
                    'No sources yet',
                    vscode.TreeItemCollapsibleState.None,
                    'empty'
                );
                emptyItem.description = 'Import papers with Aura: Import Paper';
                return [emptyItem];
            }

            for (const source of sources) {
                const item = new ResearchTreeItem(
                    source.title,
                    vscode.TreeItemCollapsibleState.Collapsed,
                    'source'
                );
                item.source = source;
                item.description = source.authors;
                item.iconPath = this.getSourceIcon(source.type);
                item.tooltip = this.getSourceTooltip(source);
                items.push(item);
            }
        } catch (error) {
            const errorItem = new ResearchTreeItem(
                'Unable to load sources',
                vscode.TreeItemCollapsibleState.None,
                'error'
            );
            errorItem.iconPath = new vscode.ThemeIcon('warning');
            items.push(errorItem);
        }

        return items;
    }

    private async getConceptItems(): Promise<ResearchTreeItem[]> {
        const items: ResearchTreeItem[] = [];

        try {
            const concepts = await this.apiService.getResearchConcepts();

            if (concepts.length === 0) {
                const emptyItem = new ResearchTreeItem(
                    'No concepts yet',
                    vscode.TreeItemCollapsibleState.None,
                    'empty'
                );
                emptyItem.description = 'Extract concepts from sources';
                return [emptyItem];
            }

            for (const concept of concepts) {
                const item = new ResearchTreeItem(
                    concept.name,
                    vscode.TreeItemCollapsibleState.None,
                    'concept'
                );
                item.concept = concept;
                item.description = `${concept.excerptCount} excerpts`;
                item.iconPath = new vscode.ThemeIcon('symbol-keyword');
                item.tooltip = concept.definition || concept.name;
                items.push(item);
            }
        } catch (error) {
            const errorItem = new ResearchTreeItem(
                'Unable to load concepts',
                vscode.TreeItemCollapsibleState.None,
                'error'
            );
            errorItem.iconPath = new vscode.ThemeIcon('warning');
            items.push(errorItem);
        }

        return items;
    }

    private getSourceDetails(source: ResearchSource): ResearchTreeItem[] {
        const items: ResearchTreeItem[] = [];

        // Status
        const statusItem = new ResearchTreeItem(
            `Status: ${source.status}`,
            vscode.TreeItemCollapsibleState.None,
            'info'
        );
        statusItem.iconPath = this.getStatusIcon(source.status);
        items.push(statusItem);

        // Source URL
        if (source.sourceUrl) {
            const urlItem = new ResearchTreeItem(
                'Open Source',
                vscode.TreeItemCollapsibleState.None,
                'link'
            );
            urlItem.iconPath = new vscode.ThemeIcon('link-external');
            urlItem.command = {
                command: 'vscode.open',
                title: 'Open URL',
                arguments: [vscode.Uri.parse(source.sourceUrl)]
            };
            items.push(urlItem);
        }

        // Local path
        if (source.localPath) {
            const pathItem = new ResearchTreeItem(
                'Open Local File',
                vscode.TreeItemCollapsibleState.None,
                'file'
            );
            pathItem.iconPath = new vscode.ThemeIcon('file-pdf');
            pathItem.command = {
                command: 'vscode.open',
                title: 'Open File',
                arguments: [vscode.Uri.file(source.localPath)]
            };
            items.push(pathItem);
        }

        return items;
    }

    private getSourceIcon(type: string): vscode.ThemeIcon {
        switch (type) {
            case 'arxiv':
                return new vscode.ThemeIcon('book');
            case 'semanticScholar':
                return new vscode.ThemeIcon('mortar-board');
            case 'webpage':
                return new vscode.ThemeIcon('globe');
            case 'pdf':
                return new vscode.ThemeIcon('file-pdf');
            default:
                return new vscode.ThemeIcon('file-text');
        }
    }

    private getStatusIcon(status: string): vscode.ThemeIcon {
        switch (status) {
            case 'ready':
                return new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
            case 'fetching':
                return new vscode.ThemeIcon('sync~spin');
            case 'error':
                return new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
            default:
                return new vscode.ThemeIcon('clock');
        }
    }

    private getSourceTooltip(source: ResearchSource): string {
        const lines: string[] = [source.title];
        if (source.authors) {
            lines.push(`Authors: ${source.authors}`);
        }
        if (source.abstract) {
            lines.push('', source.abstract.substring(0, 200) + '...');
        }
        return lines.join('\n');
    }
}

export class ResearchTreeItem extends vscode.TreeItem {
    source?: ResearchSource;
    concept?: ResearchConcept;

    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        public readonly contextValue: string
    ) {
        super(label, collapsibleState);
    }
}
