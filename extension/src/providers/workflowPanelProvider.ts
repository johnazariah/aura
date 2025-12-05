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
        const startTime = Date.now();
        console.log(`[WorkflowPanel] Opening panel for ${workflowId}`);
        
        // Check if panel already exists
        const existingPanel = this.panels.get(workflowId);
        if (existingPanel) {
            console.log(`[WorkflowPanel] Revealing existing panel (+${Date.now() - startTime}ms)`);
            existingPanel.reveal();
            await this.refreshPanel(workflowId);
            return;
        }

        // Fetch workflow data
        let workflow: Workflow;
        try {
            console.log(`[WorkflowPanel] Fetching workflow data... (+${Date.now() - startTime}ms)`);
            workflow = await this.apiService.getWorkflow(workflowId);
            console.log(`[WorkflowPanel] Got workflow data (+${Date.now() - startTime}ms)`);
        } catch (error) {
            vscode.window.showErrorMessage('Failed to load workflow');
            return;
        }

        // Create new panel
        console.log(`[WorkflowPanel] Creating webview panel... (+${Date.now() - startTime}ms)`);
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
        console.log(`[WorkflowPanel] Webview panel created (+${Date.now() - startTime}ms)`);

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
        console.log(`[WorkflowPanel] Generating HTML... (+${Date.now() - startTime}ms)`);
        const html = this.getHtml(workflow, panel.webview);
        console.log(`[WorkflowPanel] Setting HTML (${html.length} chars)... (+${Date.now() - startTime}ms)`);
        panel.webview.html = html;
        console.log(`[WorkflowPanel] Panel ready (+${Date.now() - startTime}ms)`);
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
            case 'skipStep':
                await this.handleSkipStep(workflowId, message.stepId, panel);
                break;
            case 'stepChat':
                await this.handleStepChat(workflowId, message.stepId, message.message, panel);
                break;
            case 'approveStepOutput':
                await this.handleApproveStepOutput(workflowId, message.stepId, panel);
                break;
            case 'rejectStepOutput':
                await this.handleRejectStepOutput(workflowId, message.stepId, message.reason, panel);
                break;
            case 'viewStepContext':
                await this.handleViewStepContext(workflowId, message.stepId, panel);
                break;
            case 'reassignStep':
                await this.handleReassignStep(workflowId, message.stepId, message.agentId, panel);
                break;
        }
    }

    private async handleEnrich(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        console.log(`[Enrich] Starting enrichment for workflow ${workflowId}`);
        try {
            // Get workflow to check repository path
            const workflow = await this.apiService.getWorkflow(workflowId);
            const repoPath = workflow.repositoryPath || workflow.workspacePath;
            console.log(`[Enrich] Repository path: ${repoPath}`);

            if (!repoPath) {
                console.log('[Enrich] No repository path - showing error');
                panel.webview.postMessage({ type: 'error', message: 'No repository path associated with this workflow' });
                return;
            }

            // Check if codebase is indexed
            console.log(`[Enrich] Checking index status for ${repoPath}`);
            const indexStatus = await this.apiService.getDirectoryIndexStatus(repoPath);
            console.log(`[Enrich] Index status: isIndexed=${indexStatus.isIndexed}`);

            if (!indexStatus.isIndexed) {
                // Send message to show confirmation dialog
                console.log('[Enrich] Codebase not indexed - sending confirmIndexAndEnrich message');
                panel.webview.postMessage({
                    type: 'confirmIndexAndEnrich',
                    message: 'Codebase not indexed. Index now and enrich?'
                });
                return;
            }

            // Codebase is indexed, proceed with enrichment
            console.log('[Enrich] Codebase is indexed - proceeding with enrichment');
            panel.webview.postMessage({ type: 'loading', action: 'enrich' });
            await this.apiService.analyzeWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Workflow enriched successfully' });
        } catch (error) {
            console.error('[Enrich] Error:', error);
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

            // Use vscode progress in status bar
            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Window,
                    title: '$(sync~spin) Indexing codebase...'
                },
                async (progress) => {
                    progress.report({ message: repoPath });
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
                    location: vscode.ProgressLocation.Window,
                    title: '$(sync~spin) Indexing codebase...'
                },
                async (progress) => {
                    progress.report({ message: repoPath });
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

    private async handleSkipStep(workflowId: string, stepId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            panel.webview.postMessage({ type: 'loading', action: 'skip', stepId });
            await this.apiService.skipStep(workflowId, stepId);
            vscode.window.showInformationMessage('Step skipped ⏭');
            panel.webview.postMessage({ type: 'loadingDone' });
            await this.refreshPanel(workflowId);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to skip step';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleStepChat(workflowId: string, stepId: string, message: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            const response = await this.apiService.chatWithStep(workflowId, stepId, message);
            panel.webview.postMessage({
                type: 'stepChatResponse',
                stepId,
                response: response.response,
                updatedDescription: response.updatedDescription
            });
        } catch (error) {
            const errMessage = error instanceof Error ? error.message : 'Failed to send message';
            panel.webview.postMessage({ type: 'error', message: errMessage });
        }
    }

    private async handleApproveStepOutput(workflowId: string, stepId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            panel.webview.postMessage({ type: 'loading', action: 'approve', stepId });
            await this.apiService.approveStepOutput(workflowId, stepId);
            vscode.window.showInformationMessage('Output approved ✓');
            panel.webview.postMessage({ type: 'loadingDone' });
            await this.refreshPanel(workflowId);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to approve output';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleRejectStepOutput(workflowId: string, stepId: string, reason: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            panel.webview.postMessage({ type: 'loading', action: 'reject', stepId });
            await this.apiService.rejectStepOutput(workflowId, stepId, reason);
            vscode.window.showInformationMessage(`Output rejected - step reset to pending for re-execution`);
            panel.webview.postMessage({ type: 'loadingDone' });
            await this.refreshPanel(workflowId);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to reject output';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleViewStepContext(workflowId: string, stepId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            // Get step details and show in a new panel or message
            const workflow = await this.apiService.getWorkflow(workflowId);
            const step = (workflow.steps || []).find(s => s.id === stepId);
            if (step) {
                // Show step context in a quick pick or information message
                vscode.window.showInformationMessage(`Step Context: ${step.name}\n\nCapability: ${step.capability}\nAgent: ${step.assignedAgentId || 'Not assigned'}\nStatus: ${step.status}`);
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to view step context';
            panel.webview.postMessage({ type: 'error', message });
        }
    }

    private async handleReassignStep(workflowId: string, stepId: string, agentId: string, panel: vscode.WebviewPanel): Promise<void> {
        try {
            panel.webview.postMessage({ type: 'loading', action: 'reassign', stepId });
            // TODO: Add reassign step API when implemented
            vscode.window.showInformationMessage(`Step will be reassigned to agent: ${agentId} (coming soon)`);
            panel.webview.postMessage({ type: 'loadingDone' });
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Failed to reassign step';
            panel.webview.postMessage({ type: 'error', message });
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

        /* Step card compact design */
        .step-compact {
            padding: 10px 14px;
        }
        .step-status-icon {
            font-size: 0.9em;
            margin-right: 6px;
        }
        .step-status-icon.completed { color: #107c10; }
        .step-status-icon.running { color: #0078d4; }
        .step-status-icon.pending { color: var(--vscode-descriptionForeground); }
        .step-status-icon.failed { color: #d13438; }

        /* Collapsible step sections */
        .step-section {
            margin-top: 10px;
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            overflow: hidden;
        }
        .step-section.collapsed .section-content {
            display: none;
        }
        .section-header {
            padding: 8px 12px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            cursor: pointer;
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
            display: flex;
            justify-content: space-between;
            align-items: center;
            user-select: none;
        }
        .section-header:hover {
            background: var(--vscode-list-hoverBackground);
        }
        .section-toggle {
            font-size: 0.75em;
            opacity: 0.7;
        }
        .section-content {
            padding: 12px;
            background: var(--vscode-textCodeBlock-background);
            max-height: 400px;
            overflow-y: auto;
        }

        /* Step toolbar */
        .step-toolbar {
            margin-top: 10px;
            display: flex;
            gap: 6px;
            flex-wrap: wrap;
        }
        .toolbar-btn {
            padding: 4px 10px;
            border: 1px solid var(--vscode-button-secondaryBackground);
            border-radius: 3px;
            background: transparent;
            color: var(--vscode-foreground);
            font-size: 12px;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }
        .toolbar-btn:hover:not(:disabled) {
            background: var(--vscode-button-secondaryBackground);
        }
        .toolbar-btn:disabled {
            opacity: 0.4;
            cursor: not-allowed;
        }
        .toolbar-btn.primary {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border-color: var(--vscode-button-background);
        }
        .toolbar-btn.primary:hover:not(:disabled) {
            background: var(--vscode-button-hoverBackground);
        }

        /* Step chat section */
        .step-chat {
            margin-top: 10px;
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            padding: 12px;
            background: var(--vscode-editor-inactiveSelectionBackground);
        }
        .step-chat.hidden {
            display: none;
        }
        .chat-messages {
            max-height: 200px;
            overflow-y: auto;
            margin-bottom: 10px;
        }
        .chat-message {
            margin-bottom: 8px;
            padding: 8px 10px;
            border-radius: 4px;
            font-size: 0.9em;
        }
        .chat-message.user {
            background: var(--vscode-button-secondaryBackground);
            margin-left: 20%;
        }
        .chat-message.assistant {
            background: var(--vscode-textCodeBlock-background);
            margin-right: 20%;
        }
        .chat-input-row {
            display: flex;
            gap: 8px;
        }
        .chat-input-row input {
            flex: 1;
            padding: 6px 10px;
            border: 1px solid var(--vscode-input-border);
            border-radius: 3px;
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            font-size: 13px;
        }
        .chat-input-row input:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }

        /* Approve/reject buttons */
        .output-actions {
            display: flex;
            gap: 8px;
            margin-top: 10px;
            padding-top: 10px;
            border-top: 1px solid var(--vscode-panel-border);
        }
        .btn-approve {
            background: #107c10;
            color: white;
            border: none;
        }
        .btn-approve:hover {
            background: #0e6b0e;
        }
        .btn-reject {
            background: transparent;
            color: #d13438;
            border: 1px solid #d13438;
        }
        .btn-reject:hover {
            background: rgba(209, 52, 56, 0.1);
        }

        /* Step card layout */
        .step-card {
            margin-bottom: 12px;
            padding: 12px 16px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 6px;
            border: 1px solid var(--vscode-panel-border);
            transition: border-color 0.2s;
        }
        .step-card:hover {
            border-color: var(--vscode-focusBorder);
        }
        .step-card.completed {
            border-left: 3px solid #107c10;
        }
        .step-card.running {
            border-left: 3px solid #0078d4;
            animation: running-pulse 1.5s ease-in-out infinite;
        }
        .step-card.failed {
            border-left: 3px solid #d13438;
        }
        .step-card.pending {
            border-left: 3px solid var(--vscode-descriptionForeground);
        }
        .step-card.blocked {
            opacity: 0.6;
        }

        @keyframes running-pulse {
            0%, 100% { background: var(--vscode-editor-inactiveSelectionBackground); }
            50% { background: rgba(0, 120, 212, 0.1); }
        }

        .step-header {
            display: flex;
            align-items: flex-start;
            gap: 10px;
        }
        .step-status {
            font-size: 1.1em;
            flex-shrink: 0;
            width: 20px;
            text-align: center;
        }
        .step-card.completed .step-status { color: #107c10; }
        .step-card.running .step-status { color: #0078d4; }
        .step-card.failed .step-status { color: #d13438; }
        .step-card.pending .step-status { color: var(--vscode-descriptionForeground); }

        .step-info {
            flex: 1;
            min-width: 0;
        }
        .step-title {
            display: flex;
            align-items: center;
            gap: 10px;
            flex-wrap: wrap;
        }
        .step-name {
            font-weight: 600;
            font-size: 0.95em;
        }
        .step-agent {
            font-size: 0.8em;
            color: var(--vscode-descriptionForeground);
            background: var(--vscode-badge-background);
            padding: 2px 8px;
            border-radius: 10px;
        }
        .step-description {
            margin-top: 4px;
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
            line-height: 1.4;
        }

        .step-actions {
            display: flex;
            gap: 4px;
            flex-shrink: 0;
        }
        .btn-icon {
            width: 28px;
            height: 28px;
            border: none;
            border-radius: 4px;
            background: transparent;
            color: var(--vscode-foreground);
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 14px;
        }
        .btn-icon:hover {
            background: var(--vscode-button-secondaryBackground);
        }
        .btn-icon.primary {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
        }
        .btn-icon.primary:hover {
            background: var(--vscode-button-hoverBackground);
        }

        .step-menu {
            position: absolute;
            top: 100%;
            right: 0;
            background: var(--vscode-menu-background);
            border: 1px solid var(--vscode-menu-border);
            border-radius: 4px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.3);
            z-index: 100;
            min-width: 150px;
        }
        .step-menu button {
            display: block;
            width: 100%;
            padding: 8px 12px;
            border: none;
            background: transparent;
            color: var(--vscode-menu-foreground);
            text-align: left;
            cursor: pointer;
            font-size: 13px;
        }
        .step-menu button:hover {
            background: var(--vscode-menu-selectionBackground);
        }

        .step-progress {
            margin-top: 10px;
            height: 3px;
            background: var(--vscode-progressBar-background);
            border-radius: 2px;
            overflow: hidden;
        }
        .progress-bar {
            height: 100%;
            width: 30%;
            background: #0078d4;
            animation: progress-move 1.5s ease-in-out infinite;
        }
        @keyframes progress-move {
            0% { transform: translateX(-100%); }
            100% { transform: translateX(400%); }
        }

        .step-error {
            margin-top: 8px;
            padding: 8px 12px;
            background: rgba(209, 52, 56, 0.1);
            border: 1px solid #d13438;
            border-radius: 4px;
            color: #d13438;
            font-size: 0.85em;
        }

        .step-section {
            margin-top: 10px;
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            overflow: hidden;
        }
        .section-header {
            padding: 8px 12px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            font-size: 0.85em;
            color: var(--vscode-descriptionForeground);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .section-content {
            padding: 12px;
            background: var(--vscode-textCodeBlock-background);
            max-height: 300px;
            overflow-y: auto;
        }
        .section-content pre {
            margin: 0;
            white-space: pre-wrap;
            word-break: break-word;
            font-family: var(--vscode-editor-font-family);
            font-size: 12px;
            line-height: 1.4;
        }

        .approval-buttons {
            display: flex;
            gap: 4px;
        }
        .approval-buttons .btn-icon {
            width: 24px;
            height: 24px;
            font-size: 12px;
        }
        .approval-buttons .approve { color: #107c10; }
        .approval-buttons .reject { color: #d13438; }

        .chat-messages {
            max-height: 150px;
            overflow-y: auto;
            margin-bottom: 10px;
        }
        .chat-message {
            margin-bottom: 8px;
            padding: 8px 10px;
            border-radius: 4px;
            font-size: 0.85em;
            line-height: 1.4;
        }
        .chat-message.user {
            background: var(--vscode-button-secondaryBackground);
            margin-left: 20%;
        }
        .chat-message.assistant {
            background: var(--vscode-editor-inactiveSelectionBackground);
            margin-right: 20%;
        }
        .chat-input-row {
            display: flex;
            gap: 8px;
        }
        .chat-input {
            flex: 1;
            padding: 6px 10px;
            border: 1px solid var(--vscode-input-border);
            border-radius: 3px;
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            font-size: 12px;
        }
        .chat-input:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }
        .btn.btn-small {
            padding: 4px 10px;
            font-size: 12px;
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

        /* Modal Dialog Styles */
        .modal-overlay {
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.6);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
        }
        .modal-dialog {
            background: var(--vscode-editor-background);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 8px;
            padding: 24px;
            max-width: 400px;
            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
        }
        .modal-content {
            text-align: center;
        }
        .modal-icon {
            font-size: 2.5em;
            margin-bottom: 16px;
        }
        .modal-message {
            margin-bottom: 20px;
            color: var(--vscode-foreground);
            line-height: 1.5;
        }
        .modal-buttons {
            display: flex;
            gap: 12px;
            justify-content: center;
        }
        .modal-buttons .btn {
            min-width: 100px;
        }

        .action-bar {
            display: flex;
            gap: 8px;
            margin-bottom: 20px;
            padding: 12px;
            background: var(--vscode-toolbar-hoverBackground);
            border-radius: 6px;
            align-items: center;
        }
        .action-group {
            display: flex;
            gap: 8px;
            align-items: center;
        }
        .action-spacer {
            flex: 1;
        }
        .status-text {
            font-weight: 500;
            padding: 6px 12px;
        }
        .status-text.success {
            color: #107c10;
        }
        .status-text.error {
            color: #d13438;
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

    <!-- Custom Modal Dialog (replaces confirm/alert which are blocked in sandboxed webviews) -->
    <div id="modalOverlay" class="modal-overlay" style="display: none;">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-icon" id="modalIcon">⚠️</div>
                <div class="modal-message" id="modalMessage"></div>
                <div class="modal-buttons" id="modalButtons"></div>
            </div>
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
                loadingText.textContent = messages[action] || 'Processing...';
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

        // Toggle collapsible sections (Output/Chat)
        function toggleStepSection(stepId, section) {
            const sectionEl = document.getElementById('step-' + section + '-' + stepId);
            if (sectionEl) {
                sectionEl.classList.toggle('collapsed');
            }
        }

        // Open step chat (show chat panel)
        function openStepChat(stepId) {
            const chatEl = document.getElementById('step-chat-' + stepId);
            if (chatEl) {
                chatEl.classList.toggle('hidden');
                // Focus the input if now visible
                if (!chatEl.classList.contains('hidden')) {
                    const input = chatEl.querySelector('input');
                    if (input) input.focus();
                }
            }
        }

        // Skip a step
        function skipStep(stepId) {
            if (confirm('Are you sure you want to skip this step?')) {
                vscode.postMessage({ type: 'skipStep', stepId });
            }
        }

        // Open step menu (for additional options)
        function openStepMenu(stepId) {
            // For now, show a simple alert - in future this could be a dropdown
            alert('Additional options coming soon: Edit prompt, View history, Retry with modifications');
        }

        // Approve step output
        function approveOutput(stepId) {
            vscode.postMessage({ type: 'approveStepOutput', stepId });
        }

        // Reject step output (request regeneration)
        function rejectOutput(stepId) {
            const reason = prompt('Why should this output be regenerated? (optional)');
            vscode.postMessage({ type: 'rejectStepOutput', stepId, reason: reason || '' });
        }

        // Send chat message for a step
        function sendStepChat(stepId) {
            const input = document.getElementById('chat-input-' + stepId);
            if (input && input.value.trim()) {
                const message = input.value.trim();
                input.value = '';
                
                // Add user message to chat immediately
                const messagesEl = document.querySelector('#step-chat-' + stepId + ' .chat-messages');
                if (messagesEl) {
                    messagesEl.innerHTML += '<div class="chat-message user">' + escapeHtml(message) + '</div>';
                    messagesEl.scrollTop = messagesEl.scrollHeight;
                }
                
                vscode.postMessage({ type: 'stepChat', stepId, message });
            }
        }

        // Handle Enter key in chat input
        function handleChatKeypress(event, stepId) {
            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                sendStepChat(stepId);
            }
        }

        // Helper to escape HTML
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
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

        // Modal dialog functions (replace confirm/alert which are blocked in sandboxed webviews)
        function showModal(icon, message, buttons) {
            document.getElementById('modalIcon').textContent = icon;
            document.getElementById('modalMessage').textContent = message;
            const buttonsDiv = document.getElementById('modalButtons');
            buttonsDiv.innerHTML = '';
            buttons.forEach(btn => {
                const button = document.createElement('button');
                button.className = 'btn ' + (btn.primary ? 'btn-primary' : 'btn-secondary');
                button.textContent = btn.text;
                button.onclick = btn.action;
                buttonsDiv.appendChild(button);
            });
            document.getElementById('modalOverlay').style.display = 'flex';
        }

        function hideModal() {
            document.getElementById('modalOverlay').style.display = 'none';
        }

        function openWorkspace() {
            vscode.postMessage({
                type: 'openWorkspace',
                workspacePath: workflow.workspacePath,
                gitBranch: workflow.gitBranch
            });
        }

        function toggleOutput(stepId) {
            const el = document.getElementById('output-section-' + stepId);
            if (el) {
                el.style.display = el.style.display === 'none' ? 'block' : 'none';
            }
        }

        function toggleChat(stepId) {
            const el = document.getElementById('chat-section-' + stepId);
            if (el) {
                el.style.display = el.style.display === 'none' ? 'block' : 'none';
                // Focus the input if now visible
                if (el.style.display !== 'none') {
                    const input = document.getElementById('chat-input-' + stepId);
                    if (input) input.focus();
                }
            }
        }

        function toggleStepMenu(stepId) {
            const el = document.getElementById('menu-' + stepId);
            if (el) {
                el.style.display = el.style.display === 'none' ? 'block' : 'none';
            }
        }

        function skipStep(stepId) {
            if (confirm('Are you sure you want to skip this step?')) {
                vscode.postMessage({ type: 'skipStep', stepId });
            }
        }

        function reassignStep(stepId) {
            const agent = prompt('Enter new agent ID:');
            if (agent) {
                vscode.postMessage({ type: 'reassignStep', stepId, agentId: agent });
            }
        }

        function viewContext(stepId) {
            vscode.postMessage({ type: 'viewStepContext', stepId });
        }

        function approveStep(stepId) {
            vscode.postMessage({ type: 'approveStepOutput', stepId });
        }

        function rejectStep(stepId) {
            const reason = prompt('Why should this output be regenerated? (optional)');
            vscode.postMessage({ type: 'rejectStepOutput', stepId, reason: reason || '' });
        }

        function sendStepChat(stepId) {
            const input = document.getElementById('chat-input-' + stepId);
            if (input && input.value.trim()) {
                const message = input.value.trim();
                input.value = '';
                
                // Add user message to chat immediately
                const messagesEl = document.getElementById('chat-messages-' + stepId);
                if (messagesEl) {
                    const msgDiv = document.createElement('div');
                    msgDiv.className = 'chat-message user';
                    msgDiv.textContent = message;
                    messagesEl.appendChild(msgDiv);
                    messagesEl.scrollTop = messagesEl.scrollHeight;
                }
                
                vscode.postMessage({ type: 'stepChat', stepId, message });
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
                    showModal('❌', 'Error: ' + message.message, [
                        { text: 'OK', primary: true, action: hideModal }
                    ]);
                    break;
                case 'confirmIndexAndEnrich':
                    setLoading(null, false);
                    showModal('📚', message.message, [
                        { text: 'Index & Enrich', primary: true, action: () => { hideModal(); indexAndEnrich(); } },
                        { text: 'Cancel', primary: false, action: hideModal }
                    ]);
                    break;
                case 'stepChatResponse':
                    // Add assistant response to step chat
                    const chatMessagesEl = document.getElementById('chat-messages-' + message.stepId);
                    if (chatMessagesEl) {
                        const msgDiv = document.createElement('div');
                        msgDiv.className = 'chat-message assistant';
                        msgDiv.textContent = message.response;
                        chatMessagesEl.appendChild(msgDiv);
                        chatMessagesEl.scrollTop = chatMessagesEl.scrollHeight;
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
            return '<div class="step-card pending"><em>No steps yet. Run Plan to create steps.</em></div>';
        }

        // Determine which steps can execute (only if all previous steps are completed)
        const canExecuteStep = (index: number): boolean => {
            if (steps[index].status !== 'Pending') return false;
            // Check all previous steps are completed
            for (let i = 0; i < index; i++) {
                if (steps[i].status !== 'Completed' && steps[i].status !== 'Skipped') {
                    return false;
                }
            }
            return true;
        };

        // Show in order
        return steps.map((step, index) => {
            const statusClass = step.status.toLowerCase();
            const canExecute = canExecuteStep(index);
            const isBlocked = step.status === 'Pending' && !canExecute;
            const canRetry = step.status === 'Completed' || step.status === 'Failed';
            const hasOutput = !!step.output;

            // Status icon
            const statusIcon = {
                'pending': isBlocked ? '◑' : '○',
                'running': '◐',
                'completed': '●',
                'failed': '✗',
                'skipped': '⊘'
            }[statusClass] || '○';

            // Parse output if available
            let outputHtml = '';
            let tokenInfo = '';
            if (step.output) {
                try {
                    const parsed = JSON.parse(step.output);
                    if (parsed.content) {
                        tokenInfo = parsed.tokensUsed ? `${parsed.tokensUsed} tokens` : '';
                        if (parsed.durationMs) {
                            tokenInfo += tokenInfo ? ` • ${(parsed.durationMs / 1000).toFixed(1)}s` : `${(parsed.durationMs / 1000).toFixed(1)}s`;
                        }
                        outputHtml = `
                        <div class="step-section output-section" id="output-section-${step.id}" style="display: none;">
                            <div class="section-header">
                                <span>Output</span>
                                <div class="approval-buttons">
                                    <button class="btn-icon approve" onclick="approveStep('${step.id}')" title="Approve">✓</button>
                                    <button class="btn-icon reject" onclick="rejectStep('${step.id}')" title="Request Changes">✗</button>
                                </div>
                            </div>
                            <div class="section-content">
                                <pre>${this.escapeHtml(parsed.content)}</pre>
                            </div>
                        </div>`;
                    }
                } catch {
                    outputHtml = `
                    <div class="step-section output-section" id="output-section-${step.id}" style="display: none;">
                        <div class="section-header"><span>Output</span></div>
                        <div class="section-content"><pre>${this.escapeHtml(step.output)}</pre></div>
                    </div>`;
                }
            }

            // Chat section (always available)
            const chatHtml = `
            <div class="step-section chat-section" id="chat-section-${step.id}" style="display: none;">
                <div class="section-header"><span>Chat with ${step.assignedAgentId || 'agent'}</span></div>
                <div class="section-content">
                    <div class="chat-messages" id="chat-messages-${step.id}"></div>
                    <div class="chat-input-row">
                        <input type="text" class="chat-input" id="chat-input-${step.id}" 
                            placeholder="Refine this step..." 
                            onkeypress="if(event.key==='Enter')sendStepChat('${step.id}')">
                        <button class="btn btn-small" onclick="sendStepChat('${step.id}')">Send</button>
                    </div>
                </div>
            </div>`;

            // Action buttons (right side of header)
            const actionButtons = [];
            actionButtons.push(`<button class="btn-icon" onclick="toggleChat('${step.id}')" title="Chat with agent">💬</button>`);
            
            if (hasOutput) {
                actionButtons.push(`<button class="btn-icon" onclick="toggleOutput('${step.id}')" title="View output">👁</button>`);
            }
            
            if (canExecute) {
                actionButtons.push(`<button class="btn-icon primary" onclick="executeStep('${step.id}')" title="Execute step">▶</button>`);
            } else if (canRetry) {
                actionButtons.push(`<button class="btn-icon" onclick="executeStep('${step.id}')" title="Retry step">🔄</button>`);
            }
            
            actionButtons.push(`<button class="btn-icon" onclick="toggleStepMenu('${step.id}')" title="More options">⋮</button>`);

            // Step menu (hidden by default)
            const menuHtml = `
            <div class="step-menu" id="menu-${step.id}" style="display: none;">
                <button onclick="skipStep('${step.id}')">⏭ Skip step</button>
                <button onclick="reassignStep('${step.id}')">🔄 Reassign agent</button>
                <button onclick="viewContext('${step.id}')">🔍 View context</button>
            </div>`;

            return `
            <div class="step-card ${statusClass}${isBlocked ? ' blocked' : ''}" data-step-id="${step.id}">
                <div class="step-header">
                    <div class="step-status">${statusIcon}</div>
                    <div class="step-info">
                        <div class="step-title">
                            <span class="step-name">${step.order}. ${this.escapeHtml(step.name)}</span>
                            <span class="step-agent">${step.assignedAgentId || step.capability}</span>
                        </div>
                        <div class="step-description">${step.description ? this.escapeHtml(step.description) : ''}</div>
                    </div>
                    <div class="step-actions">
                        ${actionButtons.join('')}
                    </div>
                    ${menuHtml}
                </div>
                ${step.status === 'Running' ? '<div class="step-progress"><div class="progress-bar"></div></div>' : ''}
                ${step.error ? `<div class="step-error">Error: ${this.escapeHtml(step.error)}</div>` : ''}
                ${outputHtml}
                ${chatHtml}
            </div>
            `;
        }).join('');
    }    private getChatPlaceholder(workflow: Workflow): string {
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
        const leftButtons: string[] = [];
        const rightButtons: string[] = [];
        const pendingSteps = (workflow.steps || []).filter(s => s.status === 'Pending');
        const hasPendingSteps = pendingSteps.length > 0;

        // Status-specific primary actions (left side)
        switch (workflow.status) {
            case 'Created':
                leftButtons.push('<button class="btn btn-primary" onclick="enrich()">🔍 Enrich Issue</button>');
                leftButtons.push('<button class="btn btn-primary" onclick="indexCodebase()">📚 Index Codebase</button>');
                rightButtons.push('<button class="btn btn-danger" onclick="cancel()">Cancel Workflow</button>');
                break;
            case 'Analyzed':
                leftButtons.push('<button class="btn btn-primary" onclick="plan()">📋 Create Plan</button>');
                rightButtons.push('<button class="btn btn-danger" onclick="cancel()">Cancel Workflow</button>');
                break;
            case 'Planned':
            case 'Executing':
                if (hasPendingSteps) {
                    leftButtons.push(`<button class="btn btn-primary" onclick="executeAllPending()">▶ Execute All (${pendingSteps.length})</button>`);
                }
                leftButtons.push('<button class="btn btn-primary" onclick="complete()">✓ Mark Complete</button>');
                rightButtons.push('<button class="btn btn-danger" onclick="cancel()">Cancel Workflow</button>');
                break;
            case 'Completed':
                leftButtons.push('<span class="status-text success">✓ Workflow Completed</span>');
                break;
            case 'Cancelled':
            case 'Failed':
                leftButtons.push(`<span class="status-text error">✗ ${workflow.status}</span>`);
                break;
        }

        // Utility buttons (left side, after primary actions)
        if (workflow.workspacePath && workflow.status !== 'Completed' && workflow.status !== 'Cancelled' && workflow.status !== 'Failed') {
            leftButtons.push('<button class="btn btn-primary" onclick="openWorkspace()">📂 Open Workspace</button>');
        }
        leftButtons.push('<button class="btn btn-primary" onclick="refresh()">🔄 Refresh</button>');

        // Build the layout: [left actions] [spacer] [right actions]
        let html = '<div class="action-group">' + leftButtons.join('') + '</div>';
        if (rightButtons.length > 0) {
            html += '<div class="action-spacer"></div>';
            html += '<div class="action-group">' + rightButtons.join('') + '</div>';
        }
        return html;
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
