import * as vscode from 'vscode';
import { AuraApiService } from '../services/auraApiService';

export class WelcomeViewProvider implements vscode.TreeDataProvider<WelcomeItem> {
    private _onDidChangeTreeData = new vscode.EventEmitter<WelcomeItem | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: WelcomeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: WelcomeItem): Promise<WelcomeItem[]> {
        if (element) {
            return [];
        }

        // Root level - show welcome content
        const items: WelcomeItem[] = [];

        // Welcome header
        const header = new WelcomeItem(
            'Welcome to Aura',
            vscode.TreeItemCollapsibleState.None
        );
        header.iconPath = new vscode.ThemeIcon('sparkle');
        header.description = '';
        items.push(header);

        // Description
        const desc1 = new WelcomeItem(
            'Aura indexes your code locally for AI-assisted development',
            vscode.TreeItemCollapsibleState.None
        );
        items.push(desc1);

        // What happens section - features with proper icons
        const features = [
            { icon: 'code', text: 'Code and docs are indexed' },
            { icon: 'database', text: 'Embeddings stored locally' },
            { icon: 'symbol-structure', text: 'Code graph built for navigation' },
            { icon: 'lock', text: 'All data stays on your machine' }
        ];

        for (const feature of features) {
            const item = new WelcomeItem(
                feature.text,
                vscode.TreeItemCollapsibleState.None
            );
            item.iconPath = new vscode.ThemeIcon(feature.icon);
            items.push(item);
        }

        // Enable button (as a clickable item)
        const enableItem = new WelcomeItem(
            'Enable Aura for this Workspace',
            vscode.TreeItemCollapsibleState.None
        );
        enableItem.command = {
            command: 'aura.onboardWorkspace',
            title: 'Enable Aura for this Workspace'
        };
        enableItem.contextValue = 'enableButton';
        enableItem.iconPath = new vscode.ThemeIcon('rocket', new vscode.ThemeColor('charts.green'));
        items.push(enableItem);

        return items;
    }
}

export class WelcomeItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState
    ) {
        super(label, collapsibleState);
    }
}
