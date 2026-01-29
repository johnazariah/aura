import * as vscode from 'vscode';
import { AuraApiService, Story } from '../services/auraApiService';

export class StoryTreeProvider implements vscode.TreeDataProvider<StoryTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<StoryTreeItem | undefined | null | void> = new vscode.EventEmitter<StoryTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<StoryTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    constructor(private apiService: AuraApiService) {}

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: StoryTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: StoryTreeItem): Promise<StoryTreeItem[]> {
        try {
            if (!element) {
                // Root level: show stories
                return this.getRootItems();
            }

            if (element.contextValue === 'story' && element.story) {
                // Show steps under story
                return this.getStoryChildren(element.story);
            }

            return [];
        } catch (error) {
            console.error('Error getting story tree children:', error);
            return [new StoryTreeItem('Error loading data', vscode.TreeItemCollapsibleState.None, 'error')];
        }
    }

    private async getRootItems(): Promise<StoryTreeItem[]> {
        const items: StoryTreeItem[] = [];

        try {
            // Get all stories
            const baseUrl = this.apiService.getBaseUrl();
            console.log(`[Aura] Fetching stories from ${baseUrl}`);

            // Filter stories by current workspace
            const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            const stories = await this.apiService.getStories(undefined, workspacePath);            if (stories.length === 0) {
                items.push(new StoryTreeItem(
                    'No stories yet',
                    vscode.TreeItemCollapsibleState.None,
                    'empty'
                ));
                items[0].description = 'Create one with Aura: Create Story';
            } else {
                for (const story of stories) {
                    const item = this.createStoryItem(story);
                    items.push(item);
                }
            }
        } catch (error) {
            const baseUrl = this.apiService.getBaseUrl();
            console.error(`[Aura] Failed to fetch stories from ${baseUrl}:`, error);
            
            const errorMessage = error instanceof Error ? error.message : String(error);
            const item = new StoryTreeItem(
                'Unable to connect to Aura API',
                vscode.TreeItemCollapsibleState.None,
                'offline'
            );
            item.tooltip = `Failed to connect to ${baseUrl}\n\nError: ${errorMessage}\n\nCheck that Aura is running and the aura.apiUrl setting is correct.`;
            items.push(item);
        }

        return items;
    }

    private createStoryItem(story: Story): StoryTreeItem {
        const hasSteps = story.steps && story.steps.length > 0;
        const collapsible = hasSteps
            ? vscode.TreeItemCollapsibleState.Expanded
            : vscode.TreeItemCollapsibleState.Collapsed;

        const item = new StoryTreeItem(
            story.title,
            collapsible,
            'story'
        );

        item.story = story;
        item.storyId = story.id;

        // Show wave progress in description when executing
        if (story.status === 'Executing' && story.waveCount > 0) {
            item.description = `Wave ${story.currentWave}/${story.waveCount}`;
        } else if (story.status === 'GatePending') {
            item.description = `Gate pending (Wave ${story.currentWave})`;
        } else if (story.status === 'GateFailed') {
            item.description = `Gate failed (Wave ${story.currentWave})`;
        } else {
            item.description = story.status;
        }

        item.iconPath = this.getStatusIcon(story.status);
        item.tooltip = this.getStoryTooltip(story);

        // Click opens worktree in new window
        item.command = {
            command: 'aura.openStoryWorktree',
            title: 'Open Worktree',
            arguments: [story.worktreePath]
        };

        return item;
    }

    private getStoryChildren(story: Story): StoryTreeItem[] {
        const items: StoryTreeItem[] = [];

        // Add issue link if exists
        if (story.issueUrl && story.issueNumber) {
            const issueItem = new StoryTreeItem(
                `Issue #${story.issueNumber}`,
                vscode.TreeItemCollapsibleState.None,
                'issue'
            );
            issueItem.iconPath = new vscode.ThemeIcon('github');
            issueItem.tooltip = story.issueUrl;
            issueItem.command = {
                command: 'vscode.open',
                title: 'Open Issue',
                arguments: [vscode.Uri.parse(story.issueUrl)]
            };
            items.push(issueItem);
        }

        // Add branch if exists
        if (story.gitBranch) {
            const branchItem = new StoryTreeItem(
                `Branch: ${story.gitBranch}`,
                vscode.TreeItemCollapsibleState.None,
                'info'
            );
            branchItem.iconPath = new vscode.ThemeIcon('git-branch');
            items.push(branchItem);
        }

        // Add worktree path if exists (with action to open)
        if (story.worktreePath) {
            const worktreeItem = new StoryTreeItem(
                `Worktree`,
                vscode.TreeItemCollapsibleState.None,
                'worktree'
            );
            worktreeItem.iconPath = new vscode.ThemeIcon('folder-opened');
            worktreeItem.tooltip = `Open in new window: ${story.worktreePath}`;
            worktreeItem.storyId = story.id;
            worktreeItem.command = {
                command: 'aura.openStoryWorktree',
                title: 'Open Worktree',
                arguments: [story.worktreePath]
            };
            items.push(worktreeItem);
        }

        // Add steps grouped by wave
        if (story.steps && story.steps.length > 0) {
            // Group steps by wave
            const waveGroups = new Map<number, typeof story.steps>();
            for (const step of story.steps) {
                const wave = step.wave || 1;
                if (!waveGroups.has(wave)) {
                    waveGroups.set(wave, []);
                }
                waveGroups.get(wave)!.push(step);
            }

            // Sort waves and render each
            const sortedWaves = Array.from(waveGroups.keys()).sort((a, b) => a - b);
            for (const wave of sortedWaves) {
                const waveSteps = waveGroups.get(wave)!;

                // Add wave header if there are multiple waves
                if (sortedWaves.length > 1) {
                    const waveStatus = this.getWaveStatus(waveSteps, wave, story.currentWave);
                    const waveItem = new StoryTreeItem(
                        `Wave ${wave}`,
                        vscode.TreeItemCollapsibleState.None,
                        'wave'
                    );
                    waveItem.description = waveStatus;
                    waveItem.iconPath = this.getWaveIcon(waveSteps, wave, story.currentWave);
                    items.push(waveItem);
                }

                // Add steps in this wave
                for (const step of waveSteps.sort((a, b) => a.order - b.order)) {
                    const stepItem = new StoryTreeItem(
                        `${step.order}. ${step.name}`,
                        vscode.TreeItemCollapsibleState.None,
                        'step'
                    );
                    stepItem.step = step;
                    stepItem.storyId = story.id;
                    stepItem.description = step.capability;
                    stepItem.iconPath = this.getStepIcon(step.status);
                    stepItem.tooltip = `${step.name}\nWave: ${step.wave}\nCapability: ${step.capability}\nStatus: ${step.status}`;

                    // Allow executing pending steps
                    if (step.status === 'Pending') {
                        stepItem.command = {
                            command: 'aura.executeStep',
                            title: 'Execute Step',
                            arguments: [story.id, step.id]
                        };
                    }

                    items.push(stepItem);
                }
            }
        }

        return items;
    }

    private getWaveStatus(steps: { status: string }[], wave: number, currentWave: number): string {
        const completed = steps.filter(s => s.status === 'Completed').length;
        const failed = steps.filter(s => s.status === 'Failed').length;
        const running = steps.filter(s => s.status === 'Running').length;

        if (failed > 0) {
            return `${failed} failed`;
        }
        if (running > 0) {
            return `Running ${running}/${steps.length}`;
        }
        if (completed === steps.length) {
            return 'Complete';
        }
        if (wave < currentWave) {
            return 'Complete';
        }
        return 'Pending';
    }

    private getWaveIcon(steps: { status: string }[], wave: number, currentWave: number): vscode.ThemeIcon {
        const completed = steps.filter(s => s.status === 'Completed').length;
        const failed = steps.filter(s => s.status === 'Failed').length;
        const running = steps.filter(s => s.status === 'Running').length;

        if (failed > 0) {
            return new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
        }
        if (running > 0) {
            return new vscode.ThemeIcon('sync~spin');
        }
        if (completed === steps.length) {
            return new vscode.ThemeIcon('check', new vscode.ThemeColor('charts.green'));
        }
        return new vscode.ThemeIcon('circle-outline');
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
            case 'GatePending':
                return new vscode.ThemeIcon('debug-pause', new vscode.ThemeColor('charts.yellow'));
            case 'GateFailed':
                return new vscode.ThemeIcon('warning', new vscode.ThemeColor('charts.orange'));
            case 'ReadyToComplete':
                return new vscode.ThemeIcon('check-all', new vscode.ThemeColor('charts.green'));
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

    private getStoryTooltip(story: Story): string {
        let tooltip = `${story.title}\n`;
        if (story.description) {
            tooltip += `\n${story.description}\n`;
        }
        tooltip += `\nStatus: ${story.status}`;
        if (story.issueUrl) {
            tooltip += `\nIssue: ${story.issueUrl}`;
        }
        if (story.repositoryPath) {
            tooltip += `\nRepository: ${story.repositoryPath}`;
        }
        if (story.gitBranch) {
            tooltip += `\nBranch: ${story.gitBranch}`;
        }
        if (story.worktreePath) {
            tooltip += `\nWorktree: ${story.worktreePath}`;
        }
        return tooltip;
    }
}

export class StoryTreeItem extends vscode.TreeItem {
    story?: Story;
    step?: { id: string; order: number; wave: number; name: string; capability: string; status: string };
    storyId?: string;

    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        public readonly contextValue: string
    ) {
        super(label, collapsibleState);
    }
}
