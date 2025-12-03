import * as vscode from 'vscode';
import { AuraApiService, Workflow, WorkflowStep } from '../services/auraApiService';

export class WorkflowPanelProvider {
    private panels: Map<string, vscode.WebviewPanel> = new Map();
    private newWorkflowPanel: vscode.WebviewPanel | undefined;

    constructor(
        private extensionUri: vscode.Uri,
        private apiService: AuraApiService
    ) {}

    /**
     * Opens a panel to create a new workflow with a form UI
     */
    async openNewWorkflowPanel(onCreated: (workflowId: string) => void): Promise<void> {
        // Reuse existing panel if open
        if (this.newWorkflowPanel) {
            this.newWorkflowPanel.reveal();
            return;
        }

        const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

        // Create new panel
        const panel = vscode.window.createWebviewPanel(
            'auraNewWorkflow',
            '✨ New Workflow',
            vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: false,
                localResourceRoots: [this.extensionUri]
            }
        );

        this.newWorkflowPanel = panel;

        // Handle panel disposal
        panel.onDidDispose(() => {
            this.newWorkflowPanel = undefined;
        });

        // Handle messages from webview
        panel.webview.onDidReceiveMessage(async (message) => {
            switch (message.type) {
                case 'create':
                    await this.handleCreateWorkflow(message.title, message.description, workspacePath, panel, onCreated);
                    break;
                case 'cancel':
                    panel.dispose();
                    break;
            }
        });

        // Set initial content
        panel.webview.html = this.getNewWorkflowHtml(workspacePath);
    }

    private async handleCreateWorkflow(
        title: string,
        description: string | undefined,
        workspacePath: string | undefined,
        panel: vscode.WebviewPanel,
        onCreated: (workflowId: string) => void
    ): Promise<void> {
        panel.webview.postMessage({ type: 'loading', message: 'Creating workflow...' });

        try {
            const workflow = await this.apiService.createWorkflow(title, description, workspacePath);

            // Close the creation panel
            panel.dispose();

            // Notify caller
            onCreated(workflow.id);

            // Open the workflow management panel
            await this.openWorkflowPanel(workflow.id);

            vscode.window.showInformationMessage(
                `Workflow created! Branch: ${workflow.gitBranch || 'N/A'}`
            );
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Unknown error';
            panel.webview.postMessage({ type: 'error', message: `Failed to create workflow: ${message}` });
        }
    }

    private getNewWorkflowHtml(workspacePath: string | undefined): string {
        const repoName = workspacePath ? workspacePath.split(/[/\\]/).pop() || 'repo' : 'repo';

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>New Workflow</title>
    <style>
        :root {
            --vscode-font-family: var(--vscode-editor-font-family, 'Segoe UI', sans-serif);
        }
        body {
            font-family: var(--vscode-font-family);
            padding: 24px;
            color: var(--vscode-foreground);
            background: var(--vscode-editor-background);
            line-height: 1.5;
            max-width: 600px;
            margin: 0 auto;
        }
        .header {
            margin-bottom: 24px;
            text-align: center;
        }
        .header h1 {
            font-size: 1.5em;
            margin: 0 0 8px 0;
        }
        .header p {
            color: var(--vscode-descriptionForeground);
            margin: 0;
        }
        .form-group {
            margin-bottom: 20px;
        }
        .form-group label {
            display: block;
            margin-bottom: 6px;
            font-weight: 500;
        }
        .form-group .hint {
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
            margin-top: 4px;
        }
        input, textarea {
            width: 100%;
            padding: 10px 12px;
            border: 1px solid var(--vscode-input-border);
            border-radius: 4px;
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            font-size: 14px;
            font-family: inherit;
            box-sizing: border-box;
        }
        input:focus, textarea:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }
        textarea {
            min-height: 100px;
            resize: vertical;
        }
        .preview-box {
            background: var(--vscode-textBlockQuote-background);
            border-left: 3px solid var(--vscode-textBlockQuote-border);
            padding: 12px 16px;
            border-radius: 4px;
            margin-bottom: 24px;
        }
        .preview-box h4 {
            margin: 0 0 8px 0;
            font-size: 0.9em;
            color: var(--vscode-descriptionForeground);
        }
        .preview-item {
            display: flex;
            align-items: center;
            gap: 8px;
            margin: 4px 0;
            font-size: 0.95em;
        }
        .preview-item .icon {
            opacity: 0.8;
        }
        .preview-item .label {
            color: var(--vscode-descriptionForeground);
            min-width: 80px;
        }
        .preview-item .value {
            font-family: var(--vscode-editor-font-family);
            color: var(--vscode-foreground);
        }
        .button-row {
            display: flex;
            gap: 12px;
            justify-content: flex-end;
            margin-top: 24px;
        }
        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }
        .btn:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
        .btn-primary {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
        }
        .btn-primary:hover:not(:disabled) {
            background: var(--vscode-button-hoverBackground);
        }
        .btn-secondary {
            background: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
        }
        .btn-info {
            background: #0e639c;
            color: white;
            border: 1px solid #1177bb;
        }
        .btn-info:hover {
            background: #1177bb;
        }
        .loading {
            display: none;
            align-items: center;
            justify-content: center;
            gap: 12px;
            padding: 20px;
            color: var(--vscode-foreground);
        }
        .loading.active {
            display: flex;
        }
        .spinner {
            width: 20px;
            height: 20px;
            border: 2px solid var(--vscode-badge-background);
            border-top-color: transparent;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }
        @keyframes spin {
            to { transform: rotate(360deg); }
        }
        .error-message {
            display: none;
            background: var(--vscode-inputValidation-errorBackground);
            border: 1px solid var(--vscode-inputValidation-errorBorder);
            color: var(--vscode-inputValidation-errorForeground);
            padding: 12px 16px;
            border-radius: 4px;
            margin-top: 16px;
        }
        .error-message.active {
            display: block;
        }
        .form-content.hidden {
            display: none;
        }
        .workspace-info {
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 8px 12px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 4px;
            margin-bottom: 20px;
            font-size: 0.9em;
        }
        .workspace-info .icon {
            opacity: 0.8;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>✨ Create New Workflow</h1>
        <p>Describe what you want to build or fix</p>
    </div>

    <div id="formContent" class="form-content">
        ${workspacePath ? `
        <div class="workspace-info">
            <span class="icon">📁</span>
            <span>Repository: <strong>${this.escapeHtml(repoName)}</strong></span>
        </div>
        ` : `
        <div class="workspace-info" style="border-left: 3px solid var(--vscode-editorWarning-foreground);">
            <span class="icon">⚠️</span>
            <span>No workspace folder open. Open a repository folder first.</span>
        </div>
        `}

        <div class="form-group">
            <label for="title">What do you want to build?</label>
            <input type="text" id="title" placeholder="e.g., Add user authentication" autofocus>
            <div class="hint">A short, descriptive title for this workflow</div>
        </div>

        <div class="form-group">
            <label for="description">Describe the requirements (optional)</label>
            <textarea id="description" placeholder="Add more details about the feature, bug, or task..."></textarea>
            <div class="hint">The more context you provide, the better the AI can help</div>
        </div>

        <div class="preview-box">
            <h4>Preview</h4>
            <div class="preview-item">
                <span class="icon">🌿</span>
                <span class="label">Branch:</span>
                <span class="value" id="branchPreview">aura/workflow-...</span>
            </div>
            <div class="preview-item">
                <span class="icon">📂</span>
                <span class="label">Worktree:</span>
                <span class="value" id="worktreePreview">Auto-generated in .worktrees/</span>
            </div>
        </div>

        <div class="button-row">
            <button class="btn btn-secondary" onclick="cancel()">Cancel</button>
            <button class="btn btn-primary" id="createBtn" onclick="create()" ${workspacePath ? '' : 'disabled'}>
                ✨ Create Workflow
            </button>
        </div>

        <div id="errorMessage" class="error-message"></div>
    </div>

    <div id="loadingState" class="loading">
        <div class="spinner"></div>
        <span id="loadingText">Creating workflow...</span>
    </div>

    <script>
        const vscode = acquireVsCodeApi();

        // Update branch preview as user types
        const titleInput = document.getElementById('title');
        const branchPreview = document.getElementById('branchPreview');

        titleInput.addEventListener('input', () => {
            const title = titleInput.value.trim();
            if (title) {
                const slug = title
                    .toLowerCase()
                    .replace(/[^a-z0-9]+/g, '-')
                    .replace(/^-+|-+$/g, '')
                    .substring(0, 30);
                branchPreview.textContent = slug + '-...';
            } else {
                branchPreview.textContent = '<prefix>/<title>-<id>';
            }
        });

        // Enter key submits
        titleInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && titleInput.value.trim()) {
                create();
            }
        });

        function create() {
            const title = document.getElementById('title').value.trim();
            const description = document.getElementById('description').value.trim();

            if (!title) {
                showError('Please enter a title');
                return;
            }

            vscode.postMessage({
                type: 'create',
                title: title,
                description: description || undefined
            });
        }

        function cancel() {
            vscode.postMessage({ type: 'cancel' });
        }

        function showError(message) {
            const errorEl = document.getElementById('errorMessage');
            errorEl.textContent = message;
            errorEl.classList.add('active');
        }

        window.addEventListener('message', (event) => {
            const message = event.data;

            switch (message.type) {
                case 'loading':
                    document.getElementById('formContent').classList.add('hidden');
                    document.getElementById('loadingState').classList.add('active');
                    document.getElementById('loadingText').textContent = message.message || 'Creating workflow...';
                    break;
                case 'error':
                    document.getElementById('formContent').classList.remove('hidden');
                    document.getElementById('loadingState').classList.remove('active');
                    showError(message.message);
                    break;
            }
        });
    </script>
</body>
</html>`;
    }

    async openWorkflowPanel(workflowId: string): Promise<void> {
        // Check if panel already exists
        const existingPanel = this.panels.get(workflowId);
        if (existingPanel) {
            existingPanel.reveal();
            await this.refreshPanel(workflowId);
            return;
        }

        // Fetch workflow data
        let workflow: Workflow;
        try {
            workflow = await this.apiService.getWorkflow(workflowId);
        } catch (error) {
            vscode.window.showErrorMessage('Failed to load workflow');
            return;
        }

        // Create new panel
        const panel = vscode.window.createWebviewPanel(
            'auraWorkflow',
            `📋 ${workflow.title}`,
            vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [this.extensionUri]
            }
        );

        this.panels.set(workflowId, panel);

        // Handle panel disposal
        panel.onDidDispose(() => {
            this.panels.delete(workflowId);
        });

        // Handle messages from webview
        panel.webview.onDidReceiveMessage(async (message) => {
            await this.handleMessage(workflowId, message, panel);
        });

        // Set initial content
        panel.webview.html = this.getHtml(workflow, panel.webview);
    }

    private async refreshPanel(workflowId: string): Promise<void> {
        const panel = this.panels.get(workflowId);
        if (!panel) return;

        try {
            const workflow = await this.apiService.getWorkflow(workflowId);
            // Re-render the entire HTML to update the panel
            panel.webview.html = this.getHtml(workflow, panel.webview);
        } catch (error) {
            console.error('Failed to refresh workflow panel:', error);
        }
    }

    private async handleMessage(workflowId: string, message: any, panel: vscode.WebviewPanel): Promise<void> {
        switch (message.type) {
            case 'analyze':
            case 'enrich':
                await this.handleEnrich(workflowId, panel);
                break;
            case 'indexCodebase':
                await this.handleIndexCodebase(workflowId, panel);
                break;
            case 'indexAndEnrich':
                await this.handleIndexAndEnrich(workflowId, panel);
                break;
            case 'plan':
                await this.handlePlan(workflowId, panel);
                break;
            case 'executeStep':
                await this.handleExecuteStep(workflowId, message.stepId, panel);
                break;
            case 'executeAllPending':
                await this.handleExecuteAllPending(workflowId, panel);
                break;
            case 'chat':
                await this.handleChat(workflowId, message.text, panel);
                break;
            case 'complete':
                await this.handleComplete(workflowId, panel);
                break;
            case 'cancel':
                await this.handleCancel(workflowId, panel);
                break;
            case 'refresh':
                await this.refreshPanel(workflowId);
                break;
            case 'openWorkspace':
                await this.handleOpenWorkspace(message.workspacePath, message.gitBranch);
                break;
        }
    }

    private async handleEnrich(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            // Get workflow to check repository path
            const workflow = await this.apiService.getWorkflow(workflowId);
            const repoPath = workflow.repositoryPath || workflow.workspacePath;

            if (!repoPath) {
                panel.webview.postMessage({ type: 'error', message: 'No repository path associated with this workflow' });
                return;
            }

            // Check if codebase is indexed
            const indexStatus = await this.apiService.getDirectoryIndexStatus(repoPath);

            if (!indexStatus.isIndexed) {
                // Send message to show confirmation dialog
                panel.webview.postMessage({
                    type: 'confirmIndexAndEnrich',
                    message: 'Codebase not indexed. Index now and enrich?'
                });
                return;
            }

            // Codebase is indexed, proceed with enrichment
            panel.webview.postMessage({ type: 'loading', action: 'enrich' });
            await this.apiService.analyzeWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Workflow enriched successfully' });
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to enrich workflow';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleIndexCodebase(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            const workflow = await this.apiService.getWorkflow(workflowId);
            const repoPath = workflow.repositoryPath || workflow.workspacePath;

            if (!repoPath) {
                panel.webview.postMessage({ type: 'error', message: 'No repository path associated with this workflow' });
                return;
            }

            panel.webview.postMessage({ type: 'loading', action: 'index' });

            // Use vscode progress for status bar
            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: 'Indexing codebase...',
                    cancellable: false
                },
                async (progress) => {
                    progress.report({ message: `Indexing ${repoPath}...` });
                    await this.apiService.indexDirectory(repoPath);
                }
            );

            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Codebase indexed successfully' });
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to index codebase';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleIndexAndEnrich(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            const workflow = await this.apiService.getWorkflow(workflowId);
            const repoPath = workflow.repositoryPath || workflow.workspacePath;

            if (!repoPath) {
                panel.webview.postMessage({ type: 'error', message: 'No repository path associated with this workflow' });
                return;
            }

            // First index
            panel.webview.postMessage({ type: 'loading', action: 'index' });
            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: 'Indexing codebase...',
                    cancellable: false
                },
                async (progress) => {
                    progress.report({ message: `Indexing ${repoPath}...` });
                    await this.apiService.indexDirectory(repoPath);
                }
            );

            // Then enrich
            panel.webview.postMessage({ type: 'loading', action: 'enrich' });
            await this.apiService.analyzeWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Codebase indexed and workflow enriched successfully' });
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to index and enrich';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handlePlan(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        panel.webview.postMessage({ type: 'loading', action: 'plan' });
        try {
            await this.apiService.planWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Plan created successfully' });
        } catch (error) {
            panel.webview.postMessage({ type: 'error', message: 'Failed to create plan' });
        }
    }

    private async handleExecuteStep(workflowId: string, stepId: string, panel: vscode.WebviewPanel): Promise<void> {
        panel.webview.postMessage({ type: 'loading', action: 'execute', stepId });
        try {
            await this.apiService.executeWorkflowStep(workflowId, stepId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Step executed successfully' });
        } catch (error) {
            panel.webview.postMessage({ type: 'error', message: 'Step execution failed' });
            await this.refreshPanel(workflowId);
        }
    }

    private async handleExecuteAllPending(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            // Get fresh workflow to find pending steps
            const workflow = await this.apiService.getWorkflow(workflowId);
            const pendingSteps = (workflow.steps || []).filter(s => s.status === 'Pending').sort((a, b) => a.order - b.order);

            if (pendingSteps.length === 0) {
                panel.webview.postMessage({ type: 'error', message: 'No pending steps to execute' });
                return;
            }

            // Execute each step sequentially
            for (let i = 0; i < pendingSteps.length; i++) {
                const step = pendingSteps[i];
                panel.webview.postMessage({
                    type: 'loading',
                    action: 'execute',
                    stepId: step.id,
                    message: `Executing step ${i + 1}/${pendingSteps.length}: ${step.name}`
                });

                try {
                    await this.apiService.executeWorkflowStep(workflowId, step.id);
                    await this.refreshPanel(workflowId);
                } catch (stepError) {
                    // Stop on first failure
                    panel.webview.postMessage({
                        type: 'error',
                        message: `Step "${step.name}" failed. Stopping execution.`
                    });
                    await this.refreshPanel(workflowId);
                    return;
                }
            }

            panel.webview.postMessage({ type: 'success', message: `All ${pendingSteps.length} steps completed!` });
            vscode.window.showInformationMessage(`All ${pendingSteps.length} workflow steps completed!`);
        } catch (error) {
            panel.webview.postMessage({ type: 'error', message: 'Failed to execute steps' });
        }
    }

    private async handleChat(workflowId: string, text: string, panel: vscode.WebviewPanel): Promise<void> {
        panel.webview.postMessage({ type: 'chatLoading' });
        try {
            const response = await this.apiService.sendWorkflowChat(workflowId, text);
            panel.webview.postMessage({
                type: 'chatResponse',
                response: response.response,
                planModified: response.planModified,
                analysisUpdated: response.analysisUpdated
            });
            if (response.planModified || response.analysisUpdated) {
                await this.refreshPanel(workflowId);
            }
        } catch (error) {
            panel.webview.postMessage({ type: 'chatError', message: 'Failed to send message' });
        }
    }

    private async handleComplete(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            await this.apiService.completeWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            vscode.window.showInformationMessage('Workflow completed!');
        } catch (error) {
            vscode.window.showErrorMessage('Failed to complete workflow');
        }
    }

    private async handleCancel(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        const confirm = await vscode.window.showWarningMessage(
            'Cancel this workflow?',
            { modal: true },
            'Cancel Workflow'
        );
        if (confirm) {
            try {
                await this.apiService.cancelWorkflow(workflowId);
                await this.refreshPanel(workflowId);
            } catch (error) {
                vscode.window.showErrorMessage('Failed to cancel workflow');
            }
        }
    }

    private async handleOpenWorkspace(workspacePath: string, gitBranch: string): Promise<void> {
        if (!workspacePath) {
            vscode.window.showErrorMessage('No workspace path available for this workflow');
            return;
        }

        const uri = vscode.Uri.file(workspacePath);
        
        // Check if the folder exists
        try {
            await vscode.workspace.fs.stat(uri);
        } catch {
            vscode.window.showErrorMessage(`Workspace folder not found: ${workspacePath}`);
            return;
        }

        // Open the folder - this adds it to the workspace
        const choice = await vscode.window.showInformationMessage(
            `Open workspace for branch "${gitBranch}"?`,
            { modal: false },
            'Add to Workspace',
            'Open in New Window'
        );

        if (choice === 'Add to Workspace') {
            // Add folder to current workspace
            vscode.workspace.updateWorkspaceFolders(
                vscode.workspace.workspaceFolders?.length || 0,
                0,
                { uri, name: `🔧 ${gitBranch}` }
            );
            vscode.window.showInformationMessage(`Added ${gitBranch} to workspace`);
        } else if (choice === 'Open in New Window') {
            // Open in a new VS Code window
            await vscode.commands.executeCommand('vscode.openFolder', uri, true);
        }
    }

    private getHtml(workflow: Workflow, webview: vscode.Webview): string {
        const stepsHtml = this.getStepsHtml(workflow.steps || []);
        const statusClass = workflow.status.toLowerCase();

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${workflow.title}</title>
    <style>
        :root {
            --vscode-font-family: var(--vscode-editor-font-family, 'Segoe UI', sans-serif);
        }
        body {
            font-family: var(--vscode-font-family);
            padding: 16px;
            color: var(--vscode-foreground);
            background: var(--vscode-editor-background);
            line-height: 1.5;
        }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 16px;
            padding-bottom: 12px;
            border-bottom: 1px solid var(--vscode-panel-border);
        }
        .title {
            font-size: 1.4em;
            font-weight: 600;
            margin: 0;
        }
        .status {
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 0.85em;
            font-weight: 500;
        }
        .status.created, .status.open { background: var(--vscode-badge-background); color: var(--vscode-badge-foreground); }
        .status.analyzing, .status.planning, .status.executing { background: #0078d4; color: white; }
        .status.analyzed, .status.planned { background: #107c10; color: white; }
        .status.completed { background: #107c10; color: white; }
        .status.failed { background: #d13438; color: white; }
        .status.cancelled { background: #666; color: white; }

        .meta {
            display: flex;
            gap: 16px;
            margin-bottom: 16px;
            font-size: 0.9em;
            color: var(--vscode-descriptionForeground);
        }
        .meta-item { display: flex; align-items: center; gap: 4px; }

        .chat-section {
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            border-radius: 6px;
            padding: 12px;
            margin-bottom: 20px;
        }
        .chat-input-container {
            display: flex;
            gap: 8px;
        }
        .chat-input {
            flex: 1;
            padding: 8px 12px;
            border: 1px solid var(--vscode-input-border);
            border-radius: 4px;
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            font-size: 14px;
        }
        .chat-input:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }
        .chat-response {
            margin-top: 12px;
            padding: 12px;
            background: var(--vscode-textBlockQuote-background);
            border-radius: 4px;
            white-space: pre-wrap;
        }

        .timeline {
            position: relative;
        }
        .timeline::before {
            content: '';
            position: absolute;
            left: 12px;
            top: 0;
            bottom: 0;
            width: 2px;
            background: var(--vscode-panel-border);
        }

        .timeline-item {
            position: relative;
            margin-left: 32px;
            margin-bottom: 16px;
            padding: 12px 16px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 6px;
            border: 1px solid var(--vscode-panel-border);
        }
        .timeline-item::before {
            content: '';
            position: absolute;
            left: -24px;
            top: 16px;
            width: 12px;
            height: 12px;
            border-radius: 50%;
            background: var(--vscode-badge-background);
            border: 2px solid var(--vscode-editor-background);
        }
        .timeline-item.completed::before { background: #107c10; }
        .timeline-item.running::before { background: #0078d4; animation: pulse 1s infinite; }
        .timeline-item.failed::before { background: #d13438; }
        .timeline-item.pending::before { background: var(--vscode-badge-background); }

        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        .step-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .step-name {
            font-weight: 600;
            font-size: 1em;
        }
        .step-capability {
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
            background: var(--vscode-badge-background);
            padding: 2px 8px;
            border-radius: 10px;
        }
        .step-description {
            margin-top: 8px;
            font-size: 0.9em;
            color: var(--vscode-descriptionForeground);
        }
        .step-actions {
            margin-top: 12px;
            display: flex;
            gap: 8px;
        }

        .step-output {
            margin-top: 12px;
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            overflow: hidden;
        }
        .output-header {
            padding: 8px 12px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            cursor: pointer;
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
        }
        .output-header:hover {
            background: var(--vscode-list-hoverBackground);
        }
        .output-content {
            padding: 12px;
            background: var(--vscode-textCodeBlock-background);
            max-height: 400px;
            overflow-y: auto;
        }
        .output-content pre {
            margin: 0;
            white-space: pre-wrap;
            word-break: break-word;
            font-family: var(--vscode-editor-font-family);
            font-size: 12px;
            line-height: 1.4;
        }

        .btn {
            padding: 6px 14px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 6px;
        }
        .btn:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
        .btn-primary {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
        }
        .btn-primary:hover:not(:disabled) {
            background: var(--vscode-button-hoverBackground);
        }
        .btn-secondary {
            background: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
        }
        .btn-info {
            background: #0e639c;
            color: white;
            border: 1px solid #1177bb;
        }
        .btn-info:hover {
            background: #1177bb;
        }
        .btn-danger {
            background: #d13438;
            color: white;
        }

        .phase-section {
            margin-bottom: 16px;
            padding: 12px 16px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 6px;
            border-left: 3px solid var(--vscode-badge-background);
        }
        .phase-section.completed {
            border-left-color: #107c10;
        }
        .phase-title {
            font-weight: 600;
            margin-bottom: 8px;
        }
        .analysis-content {
            font-size: 0.9em;
            max-height: 400px;
            overflow-y: auto;
        }
        .analysis-text {
            color: var(--vscode-foreground);
            line-height: 1.6;
        }
        .analysis-text h4 {
            margin: 16px 0 8px 0;
            color: var(--vscode-foreground);
            font-size: 1em;
            border-bottom: 1px solid var(--vscode-panel-border);
            padding-bottom: 4px;
        }
        .analysis-text ul {
            margin: 8px 0;
            padding-left: 20px;
        }
        .analysis-text li {
            margin: 4px 0;
        }
        .analysis-text p {
            margin: 8px 0;
        }

        .original-request {
            margin-top: 24px;
            padding: 16px;
            background: var(--vscode-textBlockQuote-background);
            border-left: 3px solid var(--vscode-textBlockQuote-border);
            border-radius: 4px;
        }
        .original-request h4 {
            margin: 0 0 8px 0;
            font-size: 0.9em;
            color: var(--vscode-descriptionForeground);
        }

        .loading {
            display: none;
            align-items: center;
            gap: 8px;
            color: var(--vscode-descriptionForeground);
        }
        .loading.active { display: flex; }
        .spinner {
            width: 16px;
            height: 16px;
            border: 2px solid var(--vscode-badge-background);
            border-top-color: transparent;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }
        @keyframes spin {
            to { transform: rotate(360deg); }
        }
        .loading-overlay {
            display: none;
            padding: 16px;
            background: var(--vscode-editor-background);
            border: 1px solid var(--vscode-badge-background);
            border-radius: 4px;
            margin-top: 8px;
        }
        .loading-overlay.active {
            display: block;
        }
        .loading-content {
            display: flex;
            align-items: center;
            gap: 12px;
            color: var(--vscode-foreground);
        }
        .loading-content .spinner {
            width: 20px;
            height: 20px;
        }
        button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

        .action-bar {
            display: flex;
            gap: 8px;
            margin-bottom: 20px;
            padding: 12px;
            background: var(--vscode-toolbar-hoverBackground);
            border-radius: 6px;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1 class="title">📋 ${this.escapeHtml(workflow.title)}</h1>
        <span class="status ${statusClass}" id="status">${workflow.status}</span>
    </div>

    <div class="meta">
        ${workflow.gitBranch ? `<div class="meta-item">🌿 ${workflow.gitBranch}</div>` : ''}
        ${workflow.workspacePath ? `<div class="meta-item">📁 ${workflow.workspacePath}</div>` : ''}
    </div>

    <div class="action-bar" id="actionBar">
        ${this.getActionButtons(workflow)}
    </div>
    <div id="loadingOverlay" class="loading-overlay">
        <div class="loading-content">
            <div class="spinner"></div>
            <span id="loadingText">Processing...</span>
        </div>
    </div>

    <div class="chat-section">
        <div class="chat-input-container">
            <input type="text" class="chat-input" id="chatInput"
                   placeholder="${this.getChatPlaceholder(workflow)}"
                   ${workflow.status === 'Completed' || workflow.status === 'Cancelled' ? 'disabled' : ''}>
            <button class="btn btn-primary" id="chatSend" onclick="sendChat()"
                    ${workflow.status === 'Completed' || workflow.status === 'Cancelled' ? 'disabled' : ''}>
                Send
            </button>
        </div>
        <div id="chatLoading" class="loading">
            <div class="spinner"></div>
            <span>Thinking...</span>
        </div>
        <div id="chatResponse" class="chat-response" style="display: none;"></div>
    </div>

    <h3>Timeline</h3>
    <div class="timeline" id="timeline">
        ${stepsHtml}
    </div>

    ${workflow.analyzedContext ? `
    <div class="phase-section completed">
        <div class="phase-title">✓ Analyzed</div>
        <div class="analysis-content">
            ${this.formatAnalyzedContext(workflow.analyzedContext)}
        </div>
    </div>
    ` : ''}

    <div class="original-request">
        <h4>Original Request</h4>
        <div>${this.escapeHtml(workflow.description || 'No description provided')}</div>
    </div>

    <script>
        const vscode = acquireVsCodeApi();
        const workflowId = '${workflow.id}';
        let workflow = ${JSON.stringify(workflow)};

        function sendChat() {
            const input = document.getElementById('chatInput');
            const text = input.value.trim();
            if (!text) return;
            
            vscode.postMessage({ type: 'chat', text });
            input.value = '';
        }

        document.getElementById('chatInput').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') sendChat();
        });

        function setLoading(action, isLoading, stepId = null) {
            const actionBar = document.getElementById('actionBar');
            const loadingOverlay = document.getElementById('loadingOverlay');
            const loadingText = document.getElementById('loadingText');
            
            if (isLoading) {
                // Disable all buttons
                actionBar.querySelectorAll('button').forEach(btn => btn.disabled = true);
                // Show loading overlay with appropriate message
                const messages = {
                    'enrich': '🔍 Enriching workflow with codebase context...',
                    'index': '📚 Indexing codebase for RAG...',
                    'plan': '📋 Creating execution plan...',
                    'execute': '▶ Executing step...',
                    'executeAll': '▶▶ Executing all pending steps...',
                    'complete': '✓ Completing workflow...',
                    'cancel': '🛑 Cancelling...'
                };
                loadingText.textContent = message.message || messages[action] || 'Processing...';
                loadingOverlay.classList.add('active');
            } else {
                loadingOverlay.classList.remove('active');
                // Re-enable buttons (they'll be refreshed anyway)
                actionBar.querySelectorAll('button').forEach(btn => btn.disabled = false);
            }
        }

        function executeStep(stepId) {
            setLoading('execute', true, stepId);
            vscode.postMessage({ type: 'executeStep', stepId });
        }

        function enrich() {
            setLoading('enrich', true);
            vscode.postMessage({ type: 'enrich' });
        }

        function indexCodebase() {
            setLoading('index', true);
            vscode.postMessage({ type: 'indexCodebase' });
        }

        function indexAndEnrich() {
            setLoading('index', true);
            vscode.postMessage({ type: 'indexAndEnrich' });
        }

        function plan() {
            setLoading('plan', true);
            vscode.postMessage({ type: 'plan' });
        }

        function complete() {
            setLoading('complete', true);
            vscode.postMessage({ type: 'complete' });
        }

        function cancel() {
            setLoading('cancel', true);
            vscode.postMessage({ type: 'cancel' });
        }

        function executeAllPending() {
            setLoading('executeAll', true);
            vscode.postMessage({ type: 'executeAllPending' });
        }

        function refresh() {
            vscode.postMessage({ type: 'refresh' });
        }

        function openWorkspace() {
            vscode.postMessage({
                type: 'openWorkspace',
                workspacePath: workflow.workspacePath,
                gitBranch: workflow.gitBranch
            });
        }        function toggleOutput(stepId) {
            const el = document.getElementById('output-' + stepId);
            if (el) {
                el.style.display = el.style.display === 'none' ? 'block' : 'none';
            }
        }

        window.addEventListener('message', (event) => {
            const message = event.data;
            
            switch (message.type) {
                case 'refresh':
                    workflow = message.workflow;
                    location.reload(); // Simple refresh for now
                    break;
                case 'chatLoading':
                    document.getElementById('chatLoading').classList.add('active');
                    document.getElementById('chatResponse').style.display = 'none';
                    break;
                case 'chatResponse':
                    document.getElementById('chatLoading').classList.remove('active');
                    const responseDiv = document.getElementById('chatResponse');
                    responseDiv.textContent = message.response;
                    responseDiv.style.display = 'block';
                    if (message.analysisUpdated) {
                        responseDiv.innerHTML += '<br><br><em>✨ Analysis updated with new context. Refreshing...</em>';
                    } else if (message.planModified) {
                        responseDiv.innerHTML += '<br><br><em>Plan was modified. Refreshing...</em>';
                    }
                    break;
                case 'chatError':
                    document.getElementById('chatLoading').classList.remove('active');
                    document.getElementById('chatResponse').textContent = 'Error: ' + message.message;
                    document.getElementById('chatResponse').style.display = 'block';
                    break;
                case 'loading':
                    setLoading(message.action, true, message.stepId);
                    break;
                case 'loadingDone':
                    setLoading(null, false);
                    break;
                case 'success':
                    setLoading(null, false);
                    // Show brief success then refresh
                    setTimeout(() => vscode.postMessage({ type: 'refresh' }), 500);
                    break;
                case 'error':
                    setLoading(null, false);
                    alert('Error: ' + message.message);
                    break;
                case 'confirmIndexAndEnrich':
                    setLoading(null, false);
                    if (confirm(message.message)) {
                        indexAndEnrich();
                    }
                    break;
            }
        });
    </script>
</body>
</html>`;
    }

    private getStepsHtml(steps: WorkflowStep[]): string {
        if (steps.length === 0) {
            return '<div class="timeline-item pending"><em>No steps yet. Run Plan to create steps.</em></div>';
        }

        // Show in order (oldest first for timeline)
        return steps.map(step => {
            const statusClass = step.status.toLowerCase();
            const canExecute = step.status === 'Pending';
            
            // Parse output if it's JSON
            let outputContent = '';
            if (step.output) {
                try {
                    const parsed = JSON.parse(step.output);
                    if (parsed.content) {
                        // Truncate long content for display
                        const content = parsed.content.length > 500 
                            ? parsed.content.substring(0, 500) + '...' 
                            : parsed.content;
                        outputContent = `
                        <div class="step-output">
                            <div class="output-header" onclick="toggleOutput('${step.id}')">
                                📄 Output (click to expand)
                                ${parsed.tokensUsed ? ` • ${parsed.tokensUsed} tokens` : ''}
                                ${parsed.durationMs ? ` • ${(parsed.durationMs / 1000).toFixed(1)}s` : ''}
                            </div>
                            <div class="output-content" id="output-${step.id}" style="display: none;">
                                <pre>${this.escapeHtml(parsed.content)}</pre>
                            </div>
                        </div>`;
                    }
                } catch {
                    // Not JSON, show raw
                    outputContent = `<div class="step-output"><pre>${this.escapeHtml(step.output)}</pre></div>`;
                }
            }

            return `
            <div class="timeline-item ${statusClass}">
                <div class="step-header">
                    <span class="step-name">${step.order}. ${this.escapeHtml(step.name)}</span>
                    <span class="step-capability">${step.capability}</span>
                </div>
                ${step.description ? `<div class="step-description">${this.escapeHtml(step.description)}</div>` : ''}
                <div style="margin-top: 8px; font-size: 0.85em;">
                    Status: <strong>${step.status}</strong>
                    ${step.assignedAgentId ? ` • Agent: ${step.assignedAgentId}` : ''}
                    ${step.attempts > 0 ? ` • Attempts: ${step.attempts}` : ''}
                    ${step.startedAt ? ` • Started: ${new Date(step.startedAt).toLocaleTimeString()}` : ''}
                    ${step.completedAt ? ` • Completed: ${new Date(step.completedAt).toLocaleTimeString()}` : ''}
                </div>
                ${step.error ? `<div style="color: #d13438; margin-top: 8px;">Error: ${this.escapeHtml(step.error)}</div>` : ''}
                ${outputContent}
                ${canExecute ? `
                <div class="step-actions">
                    <button class="btn btn-primary" onclick="executeStep('${step.id}')">▶ Execute</button>
                </div>
                ` : ''}
            </div>
            `;
        }).join('');
    }

    private getChatPlaceholder(workflow: Workflow): string {
        switch (workflow.status) {
            case 'Created':
                return 'Chat about the workflow before analysis...';
            case 'Analyzed':
                return 'Add context to refine analysis, or proceed to Create Plan...';
            case 'Planned':
            case 'Executing':
                return 'Modify the plan... (e.g., "Add a step for logging")';
            default:
                return 'Workflow is complete';
        }
    }

    private getActionButtons(workflow: Workflow): string {
        const buttons: string[] = [];
        const pendingSteps = (workflow.steps || []).filter(s => s.status === 'Pending');
        const hasPendingSteps = pendingSteps.length > 0;

        switch (workflow.status) {
            case 'Created':
                buttons.push('<button class="btn btn-info" onclick="indexCodebase()">📚 Index Codebase</button>');
                buttons.push('<button class="btn btn-primary" onclick="enrich()">🔍 Enrich Workflow Issue</button>');
                buttons.push('<button class="btn btn-danger" onclick="cancel()">Cancel</button>');
                break;
            case 'Analyzed':
                buttons.push('<button class="btn btn-primary" onclick="plan()">📋 Create Plan</button>');
                buttons.push('<button class="btn btn-danger" onclick="cancel()">Cancel</button>');
                break;
            case 'Planned':
            case 'Executing':
                if (hasPendingSteps) {
                    buttons.push(`<button class="btn btn-primary" onclick="executeAllPending()">▶▶ Execute All (${pendingSteps.length})</button>`);
                }
                buttons.push('<button class="btn btn-primary" onclick="complete()">✓ Complete</button>');
                buttons.push('<button class="btn btn-danger" onclick="cancel()">Cancel</button>');
                break;
            case 'Completed':
                buttons.push('<span style="color: #107c10;">✓ Workflow Completed</span>');
                break;
            case 'Cancelled':
            case 'Failed':
                buttons.push(`<span style="color: #d13438;">${workflow.status}</span>`);
                break;
        }

        if (workflow.workspacePath) {
            buttons.push('<button class="btn btn-secondary" onclick="openWorkspace()">📂 Open Workspace</button>');
        }
        buttons.push('<button class="btn btn-secondary" onclick="refresh()">🔄 Refresh</button>');

        return buttons.join('');
    }

    private escapeHtml(text: string): string {
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    private formatAnalyzedContext(analyzedContext: string): string {
        try {
            const parsed = JSON.parse(analyzedContext);
            if (parsed.analysis) {
                // Convert markdown-style headers and lists to HTML
                let html = this.escapeHtml(parsed.analysis)
                    .replace(/\r\n/g, '\n')
                    .replace(/## (.+)/g, '<h4>$1</h4>')
                    .replace(/- (.+)/g, '<li>$1</li>')
                    .replace(/\n\n/g, '</p><p>')
                    .replace(/\n/g, '<br>');
                
                // Wrap lists
                html = html.replace(/(<li>.*<\/li>)+/g, '<ul>$&</ul>');
                
                return `<div class="analysis-text"><p>${html}</p></div>`;
            }
            return '<div class="analysis-text">Context extracted and indexed</div>';
        } catch {
            return '<div class="analysis-text">Context extracted and indexed</div>';
        }
    }
}
