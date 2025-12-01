import * as vscode from 'vscode';
import { AuraApiService, Workflow, WorkflowStep } from '../services/auraApiService';

export class WorkflowPanelProvider {
    private panels: Map<string, vscode.WebviewPanel> = new Map();

    constructor(
        private extensionUri: vscode.Uri,
        private apiService: AuraApiService
    ) {}

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
            panel.webview.postMessage({ type: 'refresh', workflow });
        } catch (error) {
            console.error('Failed to refresh workflow panel:', error);
        }
    }

    private async handleMessage(workflowId: string, message: any, panel: vscode.WebviewPanel): Promise<void> {
        switch (message.type) {
            case 'analyze':
                await this.handleAnalyze(workflowId, panel);
                break;
            case 'plan':
                await this.handlePlan(workflowId, panel);
                break;
            case 'executeStep':
                await this.handleExecuteStep(workflowId, message.stepId, panel);
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
        }
    }

    private async handleAnalyze(workflowId: string, panel: vscode.WebviewPanel): Promise<void> {
        panel.webview.postMessage({ type: 'loading', action: 'analyze' });
        try {
            await this.apiService.analyzeWorkflow(workflowId);
            await this.refreshPanel(workflowId);
            panel.webview.postMessage({ type: 'success', message: 'Workflow analyzed successfully' });
        } catch (error) {
            panel.webview.postMessage({ type: 'error', message: 'Failed to analyze workflow' });
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

    private async handleChat(workflowId: string, text: string, panel: vscode.WebviewPanel): Promise<void> {
        panel.webview.postMessage({ type: 'chatLoading' });
        try {
            const response = await this.apiService.sendWorkflowChat(workflowId, text);
            panel.webview.postMessage({
                type: 'chatResponse',
                response: response.response,
                planModified: response.planModified
            });
            if (response.planModified) {
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

    <div class="chat-section">
        <div class="chat-input-container">
            <input type="text" class="chat-input" id="chatInput" 
                   placeholder="Chat to modify the plan... (e.g., 'Add a step for logging')"
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
        <div style="font-size: 0.9em; color: var(--vscode-descriptionForeground);">
            Context extracted and indexed
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

        function executeStep(stepId) {
            vscode.postMessage({ type: 'executeStep', stepId });
        }

        function analyze() {
            vscode.postMessage({ type: 'analyze' });
        }

        function plan() {
            vscode.postMessage({ type: 'plan' });
        }

        function complete() {
            vscode.postMessage({ type: 'complete' });
        }

        function cancel() {
            vscode.postMessage({ type: 'cancel' });
        }

        function refresh() {
            vscode.postMessage({ type: 'refresh' });
        }

        function toggleOutput(stepId) {
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
                    if (message.planModified) {
                        responseDiv.innerHTML += '<br><br><em>Plan was modified. Refreshing...</em>';
                    }
                    break;
                case 'chatError':
                    document.getElementById('chatLoading').classList.remove('active');
                    document.getElementById('chatResponse').textContent = 'Error: ' + message.message;
                    document.getElementById('chatResponse').style.display = 'block';
                    break;
                case 'loading':
                    // Show loading state for buttons
                    break;
                case 'success':
                case 'error':
                    // Handle success/error
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

    private getActionButtons(workflow: Workflow): string {
        const buttons: string[] = [];

        switch (workflow.status) {
            case 'Created':
                buttons.push('<button class="btn btn-primary" onclick="analyze()">🔍 Analyze</button>');
                buttons.push('<button class="btn btn-danger" onclick="cancel()">Cancel</button>');
                break;
            case 'Analyzed':
                buttons.push('<button class="btn btn-primary" onclick="plan()">📋 Create Plan</button>');
                buttons.push('<button class="btn btn-danger" onclick="cancel()">Cancel</button>');
                break;
            case 'Planned':
            case 'Executing':
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
}
