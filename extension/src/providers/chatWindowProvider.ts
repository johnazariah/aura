import * as vscode from 'vscode';
import { AuraApiService, AgentInfo, IndexHealthResponse } from '../services/auraApiService';

/**
 * Normalizes a file path for consistent API calls.
 * Matches the C# PathNormalizer: forward slashes, lowercase.
 */
function normalizePath(path: string): string {
    if (!path) return path;
    return path
        .replace(/\\\\/g, '/')  // Handle escaped backslashes
        .replace(/\\/g, '/')     // Convert backslashes to forward slashes
        .toLowerCase();
}

export type ContextMode = 'none' | 'text' | 'graph' | 'full';

interface ChatMessage {
    role: 'user' | 'assistant' | 'system';
    content: string;
    timestamp: Date;
    tokensUsed?: number;
    durationMs?: number;
    model?: string;
}

/**
 * Opens a dedicated chat window (WebviewPanel) for a specific agent.
 * Unlike the sidebar panel, this creates a full editor-area chat experience.
 */
export class ChatWindowProvider {
    private static _panels: Map<string, vscode.WebviewPanel> = new Map();

    constructor(
        private readonly _extensionUri: vscode.Uri,
        private readonly _apiService: AuraApiService
    ) {}

    /**
     * Opens or focuses a chat window for the specified agent.
     */
    public async openChatWindow(agent: AgentInfo): Promise<void> {
        const panelKey = agent.id;

        // If panel already exists, reveal it
        const existingPanel = ChatWindowProvider._panels.get(panelKey);
        if (existingPanel) {
            existingPanel.reveal(vscode.ViewColumn.One);
            return;
        }

        // Create new panel
        const panel = vscode.window.createWebviewPanel(
            'auraChat',
            `Chat: ${agent.name}`,
            vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [this._extensionUri]
            }
        );

        // Set icon
        panel.iconPath = {
            light: vscode.Uri.joinPath(this._extensionUri, 'resources', 'chat-light.svg'),
            dark: vscode.Uri.joinPath(this._extensionUri, 'resources', 'chat-dark.svg')
        };

        ChatWindowProvider._panels.set(panelKey, panel);

        // Clean up when panel is closed
        panel.onDidDispose(() => {
            ChatWindowProvider._panels.delete(panelKey);
        });

        // Set up the webview content
        panel.webview.html = this._getHtmlForWebview(panel.webview, agent);

        // Track conversation state
        let messages: ChatMessage[] = [];
        let contextMode: ContextMode = 'full';
        let indexHealth: IndexHealthResponse | undefined;

        // Refresh index health
        const refreshIndexHealth = async () => {
            const workspacePath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            if (workspacePath) {
                try {
                    indexHealth = await this._apiService.getIndexHealth(workspacePath);
                    panel.webview.postMessage({ type: 'healthUpdate', health: indexHealth });
                } catch {
                    // Silently fail - health indicator will show unknown
                }
            }
        };

        // Handle messages from webview
        panel.webview.onDidReceiveMessage(async (data) => {
            switch (data.type) {
                case 'sendMessage':
                    await this._handleMessage(panel, agent, data.message, messages, contextMode);
                    break;
                case 'setContextMode':
                    contextMode = data.mode as ContextMode;
                    break;
                case 'refreshHealth':
                    await refreshIndexHealth();
                    break;
                case 'clearChat':
                    messages = [];
                    panel.webview.postMessage({ type: 'cleared' });
                    break;
                case 'ready':
                    // Send initial state
                    panel.webview.postMessage({
                        type: 'init',
                        agent: { id: agent.id, name: agent.name, model: agent.model },
                        contextMode,
                        indexHealth
                    });
                    // Fetch initial health
                    await refreshIndexHealth();
                    break;
            }
        });
    }

    private async _handleMessage(
        panel: vscode.WebviewPanel,
        agent: AgentInfo,
        message: string,
        messages: ChatMessage[],
        contextMode: ContextMode
    ): Promise<void> {
        // Add user message
        const userMessage: ChatMessage = {
            role: 'user',
            content: message,
            timestamp: new Date()
        };
        messages.push(userMessage);

        panel.webview.postMessage({
            type: 'userMessage',
            message: userMessage
        });

        // Show typing indicator
        panel.webview.postMessage({ type: 'typing', isTyping: true, status: 'Thinking and exploring codebase...' });

        try {
            const rawPath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            const workspacePath = rawPath ? normalizePath(rawPath) : undefined;
            let response: { content: string; tokensUsed: number; durationMs?: number };

            const startTime = Date.now();
            const useRag = contextMode === 'text' || contextMode === 'full';
            const useCodeGraph = contextMode === 'graph' || contextMode === 'full';

            if (contextMode !== 'none') {
                // Use agentic chat for multi-step exploration
                const agenticResponse = await this._apiService.executeAgentAgentic(
                    agent.id,
                    message,
                    workspacePath,
                    10, // maxSteps
                    useRag,
                    useCodeGraph
                );
                response = {
                    content: agenticResponse.content,
                    tokensUsed: agenticResponse.tokensUsed,
                    durationMs: agenticResponse.durationMs
                };
            } else {
                const content = await this._apiService.executeAgent(
                    agent.id,
                    message,
                    workspacePath
                );
                // For non-RAG, calculate duration client-side
                response = {
                    content,
                    tokensUsed: 0,
                    durationMs: Date.now() - startTime
                };
            }

            const assistantMessage: ChatMessage = {
                role: 'assistant',
                content: response.content,
                timestamp: new Date(),
                tokensUsed: response.tokensUsed,
                durationMs: response.durationMs,
                model: agent.model
            };
            messages.push(assistantMessage);

            panel.webview.postMessage({
                type: 'assistantMessage',
                message: assistantMessage
            });

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            panel.webview.postMessage({
                type: 'error',
                message: errorMessage
            });
        }

        panel.webview.postMessage({ type: 'typing', isTyping: false });
    }

    private _getHtmlForWebview(webview: vscode.Webview, agent: AgentInfo): string {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src ${webview.cspSource} 'unsafe-inline';">
    <title>Chat: ${agent.name}</title>
    <style>
        :root {
            --container-padding: 20px;
            --message-max-width: 800px;
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
            background-color: var(--vscode-editor-background);
            height: 100vh;
            display: flex;
            flex-direction: column;
        }

        /* Header */
        .header {
            padding: 12px var(--container-padding);
            background: var(--vscode-sideBar-background);
            border-bottom: 1px solid var(--vscode-panel-border);
            display: flex;
            align-items: center;
            gap: 16px;
            flex-shrink: 0;
        }

        .agent-info {
            display: flex;
            align-items: center;
            gap: 12px;
            flex: 1;
        }

        .agent-avatar {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background: var(--vscode-button-background);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 18px;
        }

        .agent-details h2 {
            margin: 0;
            font-size: 16px;
            font-weight: 600;
        }

        .agent-details .model {
            font-size: 12px;
            opacity: 0.7;
            margin-top: 2px;
        }

        .header-controls {
            display: flex;
            align-items: center;
            gap: 16px;
        }

        .context-mode-container {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 13px;
        }

        .context-select {
            background: var(--vscode-dropdown-background);
            color: var(--vscode-dropdown-foreground);
            border: 1px solid var(--vscode-dropdown-border);
            border-radius: 4px;
            padding: 4px 8px;
            font-size: 12px;
            cursor: pointer;
        }

        .context-select:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }

        .health-indicator {
            font-size: 12px;
            cursor: help;
        }

        .health-indicator.fresh { color: #4caf50; }
        .health-indicator.stale { color: #ff9800; }
        .health-indicator.outdated { color: #f44336; }
        .health-indicator.unknown { color: #9e9e9e; }

        .toggle-container {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 13px;
        }

        .toggle {
            width: 40px;
            height: 20px;
            background: var(--vscode-input-background);
            border-radius: 10px;
            position: relative;
            cursor: pointer;
            border: 1px solid var(--vscode-input-border);
            transition: background 0.2s;
        }

        .toggle.active {
            background: var(--vscode-button-background);
        }

        .toggle::after {
            content: '';
            position: absolute;
            width: 16px;
            height: 16px;
            background: var(--vscode-button-foreground);
            border-radius: 50%;
            top: 1px;
            left: 1px;
            transition: transform 0.2s;
        }

        .toggle.active::after {
            transform: translateX(20px);
        }

        .header-button {
            background: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
            border: none;
            padding: 6px 12px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .header-button:hover {
            background: var(--vscode-button-secondaryHoverBackground);
        }

        /* Messages Area */
        .messages-container {
            flex: 1;
            overflow-y: auto;
            padding: var(--container-padding);
        }

        .messages {
            max-width: var(--message-max-width);
            margin: 0 auto;
            display: flex;
            flex-direction: column;
            gap: 16px;
        }

        .message {
            display: flex;
            gap: 12px;
            animation: fadeIn 0.2s ease-out;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(8px); }
            to { opacity: 1; transform: translateY(0); }
        }

        .message.user {
            flex-direction: row-reverse;
        }

        .message-avatar {
            width: 32px;
            height: 32px;
            border-radius: 50%;
            background: var(--vscode-input-background);
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
            font-size: 14px;
        }

        .message.user .message-avatar {
            background: var(--vscode-button-background);
        }

        .message-bubble {
            max-width: 80%;
            padding: 12px 16px;
            border-radius: 12px;
            line-height: 1.5;
        }

        .message.user .message-bubble {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border-bottom-right-radius: 4px;
        }

        .message.assistant .message-bubble {
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            border-bottom-left-radius: 4px;
        }

        .message-content {
            white-space: pre-wrap;
            word-wrap: break-word;
        }

        .message-content code {
            background: var(--vscode-textCodeBlock-background);
            padding: 2px 6px;
            border-radius: 4px;
            font-family: var(--vscode-editor-font-family);
            font-size: 0.9em;
        }

        .message-content pre {
            background: var(--vscode-textCodeBlock-background);
            padding: 12px;
            border-radius: 6px;
            overflow-x: auto;
            margin: 8px 0;
        }

        .message-content pre code {
            padding: 0;
            background: none;
        }

        .message-meta {
            font-size: 11px;
            opacity: 0.6;
            margin-top: 8px;
            display: flex;
            gap: 12px;
        }

        /* Typing Indicator */
        .typing-indicator {
            display: none;
            gap: 12px;
            padding: 0 var(--container-padding);
            max-width: var(--message-max-width);
            margin: 0 auto 16px;
        }

        .typing-indicator.visible {
            display: flex;
        }

        .typing-bubble {
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            padding: 12px 16px;
            border-radius: 12px;
            border-bottom-left-radius: 4px;
        }

        .typing-status {
            font-size: 12px;
            color: var(--vscode-descriptionForeground);
            margin-bottom: 8px;
        }

        .typing-dots {
            display: flex;
            gap: 4px;
        }

        .typing-dots span {
            width: 8px;
            height: 8px;
            background: var(--vscode-foreground);
            border-radius: 50%;
            animation: bounce 1.4s infinite ease-in-out;
            opacity: 0.6;
        }

        .typing-dots span:nth-child(1) { animation-delay: -0.32s; }
        .typing-dots span:nth-child(2) { animation-delay: -0.16s; }

        @keyframes bounce {
            0%, 80%, 100% { transform: scale(0); }
            40% { transform: scale(1); }
        }

        /* Empty State */
        .empty-state {
            flex: 1;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            text-align: center;
            padding: 40px;
            opacity: 0.8;
        }

        .empty-state h2 {
            margin: 0 0 8px;
            font-size: 20px;
        }

        .empty-state p {
            margin: 0 0 24px;
            font-size: 14px;
            max-width: 400px;
            line-height: 1.5;
        }

        .suggestions {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            justify-content: center;
        }

        .suggestion {
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            padding: 8px 16px;
            border-radius: 20px;
            cursor: pointer;
            font-size: 13px;
            transition: all 0.2s;
        }

        .suggestion:hover {
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border-color: var(--vscode-button-background);
        }

        /* Input Area */
        .input-area {
            padding: var(--container-padding);
            background: var(--vscode-sideBar-background);
            border-top: 1px solid var(--vscode-panel-border);
            flex-shrink: 0;
        }

        .input-wrapper {
            max-width: var(--message-max-width);
            margin: 0 auto;
            display: flex;
            gap: 12px;
            align-items: flex-end;
        }

        .input-box {
            flex: 1;
            background: var(--vscode-input-background);
            border: 1px solid var(--vscode-input-border);
            border-radius: 12px;
            padding: 12px 16px;
            display: flex;
            align-items: flex-end;
            gap: 12px;
        }

        .input-box:focus-within {
            border-color: var(--vscode-focusBorder);
        }

        #messageInput {
            flex: 1;
            background: transparent;
            border: none;
            color: var(--vscode-input-foreground);
            font-family: inherit;
            font-size: 14px;
            line-height: 1.5;
            resize: none;
            min-height: 24px;
            max-height: 200px;
            outline: none;
        }

        #messageInput::placeholder {
            color: var(--vscode-input-placeholderForeground);
        }

        #sendButton {
            width: 36px;
            height: 36px;
            background: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            border-radius: 50%;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
            transition: all 0.2s;
        }

        #sendButton:hover {
            background: var(--vscode-button-hoverBackground);
            transform: scale(1.05);
        }

        #sendButton:disabled {
            opacity: 0.5;
            cursor: not-allowed;
            transform: none;
        }

        /* Error Banner */
        .error-banner {
            background: var(--vscode-inputValidation-errorBackground);
            border: 1px solid var(--vscode-inputValidation-errorBorder);
            color: var(--vscode-inputValidation-errorForeground);
            padding: 12px 20px;
            margin: 0 var(--container-padding);
            border-radius: 8px;
            font-size: 13px;
            display: none;
            max-width: var(--message-max-width);
            margin-left: auto;
            margin-right: auto;
        }

        .error-banner.visible {
            display: block;
            margin-bottom: 16px;
        }
    </style>
</head>
<body>
    <div class="header">
        <div class="agent-info">
            <div class="agent-avatar">🤖</div>
            <div class="agent-details">
                <h2 id="agentName">${agent.name}</h2>
                <div class="model" id="agentModel">${agent.model || 'Loading...'}</div>
            </div>
        </div>
        <div class="header-controls">
            <div class="context-mode-container">
                <label for="contextMode">Context:</label>
                <select id="contextMode" class="context-select">
                    <option value="full">Full (Text + Graph)</option>
                    <option value="text">Text RAG Only</option>
                    <option value="graph">Graph RAG Only</option>
                    <option value="none">No Context</option>
                </select>
                <span id="healthIndicator" class="health-indicator" title="Index health unknown">●</span>
            </div>
            <button class="header-button" id="clearBtn">
                <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M10 3h3v1h-1v9l-1 1H4l-1-1V4H2V3h3V2a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v1zM9 2H6v1h3V2zM4 13h7V4H4v9zm2-8H5v7h1V5zm1 0h1v7H7V5zm2 0h1v7H9V5z"/>
                </svg>
                Clear
            </button>
        </div>
    </div>

    <div class="error-banner" id="errorBanner"></div>

    <div class="messages-container" id="messagesContainer">
        <div class="empty-state" id="emptyState">
            <h2>Chat with ${agent.name}</h2>
            <p>Ask questions about your code, get help with tasks, or just have a conversation. RAG is enabled by default for context-aware responses.</p>
            <div class="suggestions">
                <div class="suggestion" data-prompt="What does this codebase do?">What does this codebase do?</div>
                <div class="suggestion" data-prompt="Explain the architecture">Explain the architecture</div>
                <div class="suggestion" data-prompt="Find potential bugs">Find potential bugs</div>
            </div>
        </div>
        <div class="messages" id="messages"></div>
    </div>

    <div class="typing-indicator" id="typingIndicator">
        <div class="message-avatar">🤖</div>
        <div class="typing-bubble">
            <div class="typing-status" id="typingStatus">Exploring codebase...</div>
            <div class="typing-dots">
                <span></span>
                <span></span>
                <span></span>
            </div>
        </div>
    </div>

    <div class="input-area">
        <div class="input-wrapper">
            <div class="input-box">
                <textarea 
                    id="messageInput" 
                    placeholder="Type your message..."
                    rows="1"
                ></textarea>
            </div>
            <button id="sendButton" title="Send message (Enter)">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                </svg>
            </button>
        </div>
    </div>

    <script>
        const vscode = acquireVsCodeApi();

        const messagesContainer = document.getElementById('messagesContainer');
        const messagesEl = document.getElementById('messages');
        const emptyState = document.getElementById('emptyState');
        const typingIndicator = document.getElementById('typingIndicator');
        const messageInput = document.getElementById('messageInput');
        const sendButton = document.getElementById('sendButton');
        const ragToggle = document.getElementById('contextMode');
        const healthIndicator = document.getElementById('healthIndicator');
        const clearBtn = document.getElementById('clearBtn');
        const errorBanner = document.getElementById('errorBanner');
        const suggestions = document.querySelectorAll('.suggestion');

        let contextMode = 'full';
        let hasMessages = false;

        function updateHealthIndicator(health) {
            if (!health) {
                healthIndicator.className = 'health-indicator unknown';
                healthIndicator.title = 'Index health unknown';
                return;
            }
            const status = health.overallStatus || 'unknown';
            healthIndicator.className = 'health-indicator ' + status;
            let title = 'Index status: ' + status;
            if (health.commitsBehind > 0) {
                title += ' (' + health.commitsBehind + ' commits behind)';
            }
            healthIndicator.title = title;
        }

        // Handle messages from extension
        window.addEventListener('message', event => {
            const message = event.data;

            switch (message.type) {
                case 'init':
                    contextMode = message.contextMode || 'full';
                    ragToggle.value = contextMode;
                    updateHealthIndicator(message.indexHealth);
                    break;

                case 'healthUpdate':
                    updateHealthIndicator(message.health);
                    break;

                case 'userMessage':
                    addMessage(message.message, 'user');
                    break;

                case 'assistantMessage':
                    addMessage(message.message, 'assistant');
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

                case 'cleared':
                    messagesEl.innerHTML = '';
                    hasMessages = false;
                    emptyState.style.display = 'flex';
                    break;
            }
        });

        function addMessage(msg, role) {
            if (!hasMessages) {
                hasMessages = true;
                emptyState.style.display = 'none';
            }

            const div = document.createElement('div');
            div.className = 'message ' + role;

            const avatar = document.createElement('div');
            avatar.className = 'message-avatar';
            avatar.textContent = role === 'user' ? '👤' : '🤖';
            div.appendChild(avatar);

            const bubble = document.createElement('div');
            bubble.className = 'message-bubble';

            const content = document.createElement('div');
            content.className = 'message-content';
            content.innerHTML = formatContent(msg.content);
            bubble.appendChild(content);

            if (role === 'assistant' && (msg.tokensUsed || msg.model || msg.durationMs)) {
                const meta = document.createElement('div');
                meta.className = 'message-meta';
                if (msg.model) {
                    const modelSpan = document.createElement('span');
                    modelSpan.textContent = msg.model;
                    meta.appendChild(modelSpan);
                }
                if (msg.tokensUsed && msg.durationMs) {
                    const tokensPerSec = (msg.tokensUsed / (msg.durationMs / 1000)).toFixed(1);
                    const throughputSpan = document.createElement('span');
                    throughputSpan.textContent = tokensPerSec + ' tok/s';
                    throughputSpan.title = msg.tokensUsed + ' tokens in ' + (msg.durationMs / 1000).toFixed(1) + 's';
                    meta.appendChild(throughputSpan);
                } else if (msg.tokensUsed) {
                    const tokensSpan = document.createElement('span');
                    tokensSpan.textContent = msg.tokensUsed + ' tokens';
                    meta.appendChild(tokensSpan);
                }
                if (msg.durationMs) {
                    const timeSpan = document.createElement('span');
                    const seconds = (msg.durationMs / 1000).toFixed(1);
                    timeSpan.textContent = seconds + 's';
                    meta.appendChild(timeSpan);
                }
                bubble.appendChild(meta);
            }

            div.appendChild(bubble);
            messagesEl.appendChild(div);
            scrollToBottom();
        }

        function formatContent(content) {
            // Code blocks with syntax highlighting hint
            content = content.replace(/\`\`\`(\\w*)\\n([\\s\\S]*?)\`\`\`/g, '<pre><code class="language-$1">$2</code></pre>');
            // Inline code
            content = content.replace(/\`([^\`]+)\`/g, '<code>$1</code>');
            // Bold
            content = content.replace(/\\*\\*([^*]+)\\*\\*/g, '<strong>$1</strong>');
            // Italic
            content = content.replace(/\\*([^*]+)\\*/g, '<em>$1</em>');
            // Line breaks
            content = content.replace(/\\n/g, '<br>');
            return content;
        }

        function scrollToBottom() {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        function showError(message) {
            errorBanner.textContent = '❌ ' + message;
            errorBanner.classList.add('visible');
            setTimeout(() => {
                errorBanner.classList.remove('visible');
            }, 5000);
        }

        function sendMessage(text) {
            const message = text || messageInput.value.trim();
            if (!message) return;

            vscode.postMessage({ type: 'sendMessage', message });
            messageInput.value = '';
            messageInput.style.height = 'auto';
        }

        // Event listeners
        sendButton.addEventListener('click', () => sendMessage());

        messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        // Auto-resize textarea
        messageInput.addEventListener('input', () => {
            messageInput.style.height = 'auto';
            messageInput.style.height = Math.min(messageInput.scrollHeight, 200) + 'px';
        });

        ragToggle.addEventListener('change', () => {
            contextMode = ragToggle.value;
            vscode.postMessage({ type: 'setContextMode', mode: contextMode });
        });

        clearBtn.addEventListener('click', () => {
            vscode.postMessage({ type: 'clearChat' });
        });

        // Suggestion clicks
        suggestions.forEach(s => {
            s.addEventListener('click', () => {
                const prompt = s.getAttribute('data-prompt');
                if (prompt) {
                    sendMessage(prompt);
                }
            });
        });

        // Focus input on load
        messageInput.focus();

        // Initialize
        vscode.postMessage({ type: 'ready' });
    </script>
</body>
</html>`;
    }
}
