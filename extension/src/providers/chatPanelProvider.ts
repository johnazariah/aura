import * as vscode from 'vscode';
import { AuraApiService, AgentInfo, IndexHealthResponse } from '../services/auraApiService';

// Context modes for chat - determines what context is included with prompts
export type ContextMode = 'none' | 'text' | 'graph' | 'full';

interface ChatMessage {
    role: 'user' | 'assistant' | 'system';
    content: string;
    timestamp: Date;
    tokensUsed?: number;
    model?: string;
    ragSources?: string[];
}

interface Conversation {
    id: string;
    title: string;
    agentId: string;
    agentName: string;
    messages: ChatMessage[];
    contextMode: ContextMode;
}

export class ChatPanelProvider implements vscode.WebviewViewProvider {
    public static readonly viewType = 'aura.chat';

    private _view?: vscode.WebviewView;
    private _currentConversation?: Conversation;
    private _currentAgent?: AgentInfo;
    private _contextMode: ContextMode = 'full';
    private _indexHealth?: IndexHealthResponse;

    constructor(
        private readonly _extensionUri: vscode.Uri,
        private readonly _apiService: AuraApiService
    ) {}

    public resolveWebviewView(
        webviewView: vscode.WebviewView,
        _context: vscode.WebviewViewResolveContext,
        _token: vscode.CancellationToken
    ) {
        this._view = webviewView;

        webviewView.webview.options = {
            enableScripts: true,
            localResourceRoots: [this._extensionUri]
        };

        webviewView.webview.html = this._getHtmlForWebview(webviewView.webview);

        // Handle messages from the webview
        webviewView.webview.onDidReceiveMessage(async (data) => {
            switch (data.type) {
                case 'sendMessage':
                    await this._handleUserMessage(data.message);
                    break;
                case 'selectAgent':
                    await this._selectAgent();
                    break;
                case 'setContextMode':
                    this._contextMode = data.mode as ContextMode;
                    this._updateState();
                    break;
                case 'newConversation':
                    this._startNewConversation();
                    break;
                case 'ready':
                    await this._initializeChat();
                    break;
                case 'refreshHealth':
                    await this._refreshIndexHealth();
                    break;
            }
        });
    }

    private async _initializeChat() {
        // Load available agents and select default
        try {
            const agents = await this._apiService.getAgents();
            const chatAgent = agents.find(a => a.id === 'chat-agent') || agents[0];
            
            if (chatAgent) {
                this._currentAgent = chatAgent;
                this._startNewConversation();
            }

            this._view?.webview.postMessage({
                type: 'agents',
                agents: agents.map(a => ({ id: a.id, name: a.name }))
            });

            // Also fetch index health
            await this._refreshIndexHealth();
        } catch (error) {
            this._view?.webview.postMessage({
                type: 'error',
                message: 'Failed to connect to Aura API. Is the service running?'
            });
        }
    }

    private async _refreshIndexHealth() {
        try {
            const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            if (workspacePath) {
                this._indexHealth = await this._apiService.getIndexHealth(workspacePath);
                this._updateState();
            }
        } catch {
            // Index health is optional - don't show error
            this._indexHealth = undefined;
        }
    }

    private _startNewConversation() {
        if (!this._currentAgent) return;

        this._currentConversation = {
            id: crypto.randomUUID(),
            title: 'New Chat',
            agentId: this._currentAgent.id,
            agentName: this._currentAgent.name,
            messages: [],
            contextMode: this._contextMode
        };

        this._updateState();
    }

    private async _selectAgent() {
        try {
            const agents = await this._apiService.getAgents();
            
            const items = agents.map(a => ({
                label: a.name,
                description: a.model,
                detail: a.description,
                agent: a
            }));

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'Select an agent for chat'
            });

            if (selected) {
                this._currentAgent = selected.agent;
                this._startNewConversation();
            }
        } catch (error) {
            vscode.window.showErrorMessage('Failed to load agents');
        }
    }

    private async _handleUserMessage(message: string) {
        if (!this._currentConversation || !this._currentAgent) {
            this._view?.webview.postMessage({
                type: 'error',
                message: 'No conversation active. Please select an agent.'
            });
            return;
        }

        // Add user message
        const userMessage: ChatMessage = {
            role: 'user',
            content: message,
            timestamp: new Date()
        };
        this._currentConversation.messages.push(userMessage);
        this._updateState();

        // Show typing indicator
        this._view?.webview.postMessage({ type: 'typing', isTyping: true, status: 'Thinking and exploring codebase...' });

        try {
            const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            let response: { content: string; tokensUsed: number; ragEnriched?: boolean };

            // Use RAG based on context mode (text or full mode)
            const useRag = this._contextMode === 'text' || this._contextMode === 'full';
            
            if (useRag) {
                // Use agentic chat for multi-step exploration
                const agenticResponse = await this._apiService.executeAgentAgentic(
                    this._currentAgent.id,
                    message,
                    workspacePath,
                    10, // maxSteps
                    true, // useRag
                    true  // useCodeGraph
                );
                response = { content: agenticResponse.content, tokensUsed: agenticResponse.tokensUsed };
            } else {
                const content = await this._apiService.executeAgent(
                    this._currentAgent.id,
                    message,
                    workspacePath
                );
                response = { content, tokensUsed: 0 };
            }

            const assistantMessage: ChatMessage = {
                role: 'assistant',
                content: response.content,
                timestamp: new Date(),
                tokensUsed: response.tokensUsed,
                model: this._currentAgent.model
            };
            this._currentConversation.messages.push(assistantMessage);

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            const assistantMessage: ChatMessage = {
                role: 'assistant',
                content: `❌ Error: ${errorMessage}`,
                timestamp: new Date()
            };
            this._currentConversation.messages.push(assistantMessage);
        }

        this._view?.webview.postMessage({ type: 'typing', isTyping: false });
        this._updateState();
    }

    private _updateState() {
        this._view?.webview.postMessage({
            type: 'state',
            conversation: this._currentConversation,
            currentAgent: this._currentAgent ? {
                id: this._currentAgent.id,
                name: this._currentAgent.name,
                model: this._currentAgent.model
            } : null,
            contextMode: this._contextMode,
            indexHealth: this._indexHealth
        });
    }

    private _getHtmlForWebview(webview: vscode.Webview): string {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src ${webview.cspSource} 'unsafe-inline';">
    <title>Aura Chat</title>
    <style>
        :root {
            --container-padding: 12px;
            --input-padding: 8px;
        }
        
        * {
            box-sizing: border-box;
        }
        
        body {
            padding: 0;
            margin: 0;
            font-family: var(--vscode-font-family);
            font-size: var(--vscode-font-size);
            color: var(--vscode-foreground);
            background-color: var(--vscode-sideBar-background);
            height: 100vh;
            display: flex;
            flex-direction: column;
        }
        
        .header {
            padding: var(--container-padding);
            border-bottom: 1px solid var(--vscode-panel-border);
            display: flex;
            align-items: center;
            gap: 8px;
            flex-shrink: 0;
        }
        
        .header-title {
            flex: 1;
            font-weight: 600;
            cursor: pointer;
        }
        
        .header-title:hover {
            color: var(--vscode-textLink-foreground);
        }
        
        .header-controls {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        
        .context-selector {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 11px;
        }
        
        .context-select {
            padding: 2px 6px;
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            border-radius: 3px;
            font-size: 11px;
            cursor: pointer;
        }
        
        .context-select:focus {
            outline: none;
            border-color: var(--vscode-focusBorder);
        }
        
        .health-indicator {
            display: flex;
            align-items: center;
            gap: 3px;
            font-size: 10px;
            padding: 2px 6px;
            border-radius: 3px;
            cursor: pointer;
        }
        
        .health-indicator.fresh {
            color: var(--vscode-testing-iconPassed);
        }
        
        .health-indicator.stale {
            color: var(--vscode-testing-iconFailed);
            background: var(--vscode-inputValidation-warningBackground);
        }
        
        .health-indicator.not-indexed {
            color: var(--vscode-disabledForeground);
        }
        
        .health-dot {
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background: currentColor;
        }
        
        .icon-button {
            background: none;
            border: none;
            color: var(--vscode-foreground);
            cursor: pointer;
            padding: 4px;
            border-radius: 4px;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        
        .icon-button:hover {
            background: var(--vscode-toolbar-hoverBackground);
        }
        
        .messages {
            flex: 1;
            overflow-y: auto;
            padding: var(--container-padding);
            display: flex;
            flex-direction: column;
            gap: 12px;
        }
        
        .message {
            padding: 10px 12px;
            border-radius: 8px;
            max-width: 90%;
            word-wrap: break-word;
        }
        
        .message.user {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            align-self: flex-end;
        }
        
        .message.assistant {
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            align-self: flex-start;
        }
        
        .message.system {
            background: var(--vscode-editorInfo-background);
            border: 1px solid var(--vscode-editorInfo-border);
            align-self: center;
            font-size: 12px;
            opacity: 0.8;
        }
        
        .message-content {
            white-space: pre-wrap;
        }
        
        .message-content code {
            background: var(--vscode-textCodeBlock-background);
            padding: 2px 4px;
            border-radius: 3px;
            font-family: var(--vscode-editor-font-family);
            font-size: 0.9em;
        }
        
        .message-content pre {
            background: var(--vscode-textCodeBlock-background);
            padding: 8px;
            border-radius: 4px;
            overflow-x: auto;
            margin: 8px 0;
        }
        
        .message-content pre code {
            padding: 0;
            background: none;
        }
        
        .message-meta {
            font-size: 10px;
            opacity: 0.7;
            margin-top: 6px;
        }
        
        .typing-indicator {
            padding: 10px 12px;
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            border-radius: 8px;
            align-self: flex-start;
            display: none;
        }
        
        .typing-indicator.visible {
            display: block;
        }
        
        .typing-status {
            font-size: 11px;
            color: var(--vscode-descriptionForeground);
            margin-bottom: 6px;
        }
        
        .typing-dots {
            display: flex;
            gap: 4px;
        }
        
        .typing-dots span {
            width: 6px;
            height: 6px;
            background: var(--vscode-foreground);
            border-radius: 50%;
            animation: bounce 1.4s infinite ease-in-out;
        }
        
        .typing-dots span:nth-child(1) { animation-delay: -0.32s; }
        .typing-dots span:nth-child(2) { animation-delay: -0.16s; }
        
        @keyframes bounce {
            0%, 80%, 100% { transform: scale(0); }
            40% { transform: scale(1); }
        }
        
        .input-container {
            padding: var(--container-padding);
            border-top: 1px solid var(--vscode-panel-border);
            flex-shrink: 0;
        }
        
        .input-wrapper {
            display: flex;
            gap: 8px;
        }
        
        #messageInput {
            flex: 1;
            padding: var(--input-padding);
            background: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            border-radius: 4px;
            font-family: inherit;
            font-size: inherit;
            resize: none;
            min-height: 36px;
            max-height: 120px;
        }
        
        #messageInput:focus {
            outline: none;
            border-color: var(--vscode-focusBorder);
        }
        
        #sendButton {
            padding: 8px 16px;
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-weight: 500;
        }
        
        #sendButton:hover {
            background: var(--vscode-button-hoverBackground);
        }
        
        #sendButton:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
        
        .empty-state {
            flex: 1;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 20px;
            text-align: center;
            opacity: 0.7;
        }
        
        .empty-state h3 {
            margin: 0 0 8px 0;
        }
        
        .empty-state p {
            margin: 0;
            font-size: 12px;
        }
        
        .error-banner {
            background: var(--vscode-inputValidation-errorBackground);
            border: 1px solid var(--vscode-inputValidation-errorBorder);
            color: var(--vscode-inputValidation-errorForeground);
            padding: 8px 12px;
            margin: 8px var(--container-padding);
            border-radius: 4px;
            font-size: 12px;
            display: none;
        }
        
        .error-banner.visible {
            display: block;
        }
    </style>
</head>
<body>
    <div class="header">
        <span class="header-title" id="agentName" title="Click to change agent">Select Agent</span>
        <div class="header-controls">
            <div class="context-selector">
                <span id="healthIndicator" class="health-indicator not-indexed" title="Index status">
                    <span class="health-dot"></span>
                    <span id="healthText">Not indexed</span>
                </span>
                <select id="contextMode" class="context-select" title="Context mode">
                    <option value="none">No context</option>
                    <option value="text">Text RAG</option>
                    <option value="graph">Graph RAG</option>
                    <option value="full" selected>Full context</option>
                </select>
            </div>
            <button class="icon-button" id="newChatBtn" title="New conversation">
                <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M14 7v1H8v6H7V8H1V7h6V1h1v6h6z"/>
                </svg>
            </button>
        </div>
    </div>
    
    <div class="error-banner" id="errorBanner"></div>
    
    <div class="messages" id="messages">
        <div class="empty-state" id="emptyState">
            <h3>🌟 Aura Chat</h3>
            <p>Ask questions about your codebase.<br/>Select a context mode for RAG-enriched answers.</p>
        </div>
    </div>
    
    <div class="typing-indicator" id="typingIndicator">
        <div class="typing-status" id="typingStatus">Exploring codebase...</div>
        <div class="typing-dots">
            <span></span>
            <span></span>
            <span></span>
        </div>
    </div>
    
    <div class="input-container">
        <div class="input-wrapper">
            <textarea 
                id="messageInput" 
                placeholder="Ask about your codebase..."
                rows="1"
            ></textarea>
            <button id="sendButton">Send</button>
        </div>
    </div>

    <script>
        const vscode = acquireVsCodeApi();
        
        const messagesEl = document.getElementById('messages');
        const emptyStateEl = document.getElementById('emptyState');
        const typingIndicator = document.getElementById('typingIndicator');
        const messageInput = document.getElementById('messageInput');
        const sendButton = document.getElementById('sendButton');
        const agentNameEl = document.getElementById('agentName');
        const contextModeSelect = document.getElementById('contextMode');
        const healthIndicator = document.getElementById('healthIndicator');
        const healthText = document.getElementById('healthText');
        const newChatBtn = document.getElementById('newChatBtn');
        const errorBanner = document.getElementById('errorBanner');
        
        let currentState = null;
        
        // Handle messages from extension
        window.addEventListener('message', event => {
            const message = event.data;
            
            switch (message.type) {
                case 'state':
                    currentState = message;
                    renderState();
                    break;
                case 'typing':
                    typingIndicator.classList.toggle('visible', message.isTyping);
                    sendButton.disabled = message.isTyping;
                    if (message.isTyping) {
                        document.getElementById('typingStatus').textContent = message.status || 'Exploring codebase...';
                        scrollToBottom();
                    }
                    break;
                case 'error':
                    showError(message.message);
                    break;
                case 'agents':
                    // Agents loaded
                    break;
            }
        });
        
        function renderState() {
            if (!currentState) return;
            
            // Update agent name
            if (currentState.currentAgent) {
                agentNameEl.textContent = currentState.currentAgent.name;
                agentNameEl.title = \`\${currentState.currentAgent.name} (\${currentState.currentAgent.model}) - Click to change\`;
            } else {
                agentNameEl.textContent = 'Select Agent';
            }
            
            // Update context mode selector
            if (currentState.contextMode) {
                contextModeSelect.value = currentState.contextMode;
            }
            
            // Update index health indicator
            if (currentState.indexHealth) {
                const health = currentState.indexHealth;
                healthIndicator.className = 'health-indicator ' + health.overallStatus;
                
                if (health.overallStatus === 'fresh') {
                    healthText.textContent = 'Indexed';
                    healthIndicator.title = 'Index is up to date with latest commit';
                } else if (health.overallStatus === 'stale') {
                    const behind = health.rag?.commitsBehind || health.graph?.commitsBehind || 0;
                    healthText.textContent = behind > 0 ? \`\${behind} behind\` : 'Stale';
                    healthIndicator.title = 'Index is behind - click to refresh';
                } else {
                    healthText.textContent = 'Not indexed';
                    healthIndicator.title = 'Workspace not indexed - run indexing first';
                }
            }
            
            // Render messages
            const messages = currentState.conversation?.messages || [];
            
            if (messages.length === 0) {
                emptyStateEl.style.display = 'flex';
                // Clear any existing messages except empty state
                const existingMessages = messagesEl.querySelectorAll('.message');
                existingMessages.forEach(m => m.remove());
            } else {
                emptyStateEl.style.display = 'none';
                renderMessages(messages);
            }
        }
        
        function renderMessages(messages) {
            // Clear existing messages
            const existingMessages = messagesEl.querySelectorAll('.message');
            existingMessages.forEach(m => m.remove());
            
            messages.forEach(msg => {
                const div = document.createElement('div');
                div.className = \`message \${msg.role}\`;
                
                const content = document.createElement('div');
                content.className = 'message-content';
                content.innerHTML = formatContent(msg.content);
                div.appendChild(content);
                
                if (msg.role === 'assistant' && (msg.tokensUsed || msg.model)) {
                    const meta = document.createElement('div');
                    meta.className = 'message-meta';
                    const parts = [];
                    if (msg.model) parts.push(msg.model);
                    if (msg.tokensUsed) parts.push(\`\${msg.tokensUsed} tokens\`);
                    meta.textContent = parts.join(' • ');
                    div.appendChild(meta);
                }
                
                messagesEl.appendChild(div);
            });
            
            scrollToBottom();
        }
        
        function formatContent(content) {
            // Basic markdown-ish formatting
            // Code blocks
            content = content.replace(/\`\`\`(\\w*)\\n([\\s\\S]*?)\`\`\`/g, '<pre><code>$2</code></pre>');
            // Inline code
            content = content.replace(/\`([^\`]+)\`/g, '<code>$1</code>');
            // Bold
            content = content.replace(/\\*\\*([^*]+)\\*\\*/g, '<strong>$1</strong>');
            // Italic
            content = content.replace(/\\*([^*]+)\\*/g, '<em>$1</em>');
            
            return content;
        }
        
        function scrollToBottom() {
            messagesEl.scrollTop = messagesEl.scrollHeight;
        }
        
        function showError(message) {
            errorBanner.textContent = message;
            errorBanner.classList.add('visible');
            setTimeout(() => {
                errorBanner.classList.remove('visible');
            }, 5000);
        }
        
        function sendMessage() {
            const message = messageInput.value.trim();
            if (!message) return;
            
            vscode.postMessage({ type: 'sendMessage', message });
            messageInput.value = '';
            messageInput.style.height = 'auto';
        }
        
        // Event listeners
        sendButton.addEventListener('click', sendMessage);
        
        messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
        
        // Auto-resize textarea
        messageInput.addEventListener('input', () => {
            messageInput.style.height = 'auto';
            messageInput.style.height = Math.min(messageInput.scrollHeight, 120) + 'px';
        });
        
        agentNameEl.addEventListener('click', () => {
            vscode.postMessage({ type: 'selectAgent' });
        });
        
        contextModeSelect.addEventListener('change', (e) => {
            vscode.postMessage({ type: 'setContextMode', mode: e.target.value });
        });
        
        healthIndicator.addEventListener('click', () => {
            vscode.postMessage({ type: 'refreshHealth' });
        });
        
        newChatBtn.addEventListener('click', () => {
            vscode.postMessage({ type: 'newConversation' });
        });
        
        // Initialize
        vscode.postMessage({ type: 'ready' });
    </script>
</body>
</html>`;
    }
}
