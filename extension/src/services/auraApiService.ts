import * as vscode from 'vscode';
import axios, { AxiosInstance } from 'axios';

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

export interface AgentInfo {
    id: string;
    name: string;
    description: string;
    capabilities: string[];
    priority: number;
    languages?: string[];
    provider: string;
    model: string;
    tags: string[];
}

export interface HealthResponse {
    status: string;
    healthy: boolean;
    details?: string;
    version?: string;
}

export interface RagHealthResponse {
    healthy: boolean;
    details: string;
    totalDocuments: number;
    totalChunks: number;
}

export interface IndexHealthInfo {
    indexType: string;
    status: 'fresh' | 'stale' | 'not-indexed';
    indexedAt?: string;
    indexedCommitSha?: string;
    commitsBehind?: number;
    isStale: boolean;
    itemCount: number;
}

export interface IndexHealthResponse {
    workspacePath: string;
    isGitRepository: boolean;
    currentCommitSha?: string;
    currentCommitAt?: string;
    overallStatus: 'fresh' | 'stale' | 'not-indexed';
    rag: IndexHealthInfo;
    graph: IndexHealthInfo;
}

export interface RagIndexResult {
    success: boolean;
    filesIndexed: number;
    message: string;
}

export interface BackgroundIndexJob {
    jobId: string;
    directory: string;
    message: string;
}

export interface BackgroundJobStatus {
    jobId: string;
    source: string;
    state: 'Queued' | 'Processing' | 'Completed' | 'Failed';
    totalItems: number;
    processedItems: number;
    failedItems: number;
    progressPercent: number;
    startedAt?: string;
    completedAt?: string;
    error?: string;
}

export interface CodeGraphIndexResult {
    success: boolean;
    nodesCreated: number;
    edgesCreated: number;
    projectsIndexed: number;
    filesIndexed: number;
    typesIndexed: number;
    durationMs: number;
    warnings: string[];
    error?: string;
}

export interface RagQueryResult {
    contentId: string;
    chunkIndex: number;
    text: string;
    score: number;
    sourcePath?: string;
    contentType: string;
}

export interface RagExecuteResult {
    content: string;
    tokensUsed: number;
    ragEnriched: boolean;
    durationMs: number;
}

export interface AgenticResult {
    content: string;
    tokensUsed: number;
    stepCount: number;
    durationMs: number;
    toolSteps: Array<{
        toolId: string;
        input: string;
        output: string;
        success: boolean;
    }>;
}

export interface StreamChatMessage {
    role: 'user' | 'assistant' | 'system';
    content: string;
}

export interface StreamChatCallbacks {
    onToken: (content: string) => void;
    onDone: (totalTokens: number, finishReason: string) => void;
    onError: (message: string, code?: string) => void;
}

// Story execution progress events (SSE streaming)
export type StoryProgressEventType =
    | 'Started'
    | 'WaveStarted'
    | 'StepStarted'
    | 'StepOutput'
    | 'StepCompleted'
    | 'StepFailed'
    | 'WaveCompleted'
    | 'GateStarted'
    | 'GatePassed'
    | 'GateFailed'
    | 'Completed'
    | 'Failed'
    | 'Cancelled';

export interface StoryProgressEvent {
    type: StoryProgressEventType;
    storyId: string;
    timestamp: string;
    wave?: number;
    totalWaves?: number;
    stepId?: string;
    stepName?: string;
    output?: string;
    error?: string;
    gateResult?: {
        passed: boolean;
        gateType: string;
        afterWave: number;
        buildOutput?: string;
        testOutput?: string;
        error?: string;
    };
}

export interface StoryStreamCallbacks {
    onEvent: (event: StoryProgressEvent) => void;
    onDone: () => void;
    onError: (message: string) => void;
}

// =====================
// Developer Module Types
// =====================

export interface StoryStep {
    id: string;
    order: number;
    wave: number;
    name: string;
    capability: string;
    description?: string;
    status: string;
    assignedAgentId?: string;
    attempts: number;
    output?: string;
    error?: string;
    startedAt?: string;
    completedAt?: string;
    needsRework?: boolean;
    previousOutput?: string;
    approval?: string;
    chatHistory?: string;
}

export interface Story {
    id: string;
    title: string;
    description?: string;
    status: string;
    automationMode?: string;
    gitBranch?: string;
    worktreePath?: string;
    repositoryPath?: string;
    patternName?: string;
    issueUrl?: string;
    issueProvider?: string;
    issueNumber?: number;
    issueOwner?: string;
    issueRepo?: string;
    analyzedContext?: string;
    executionPlan?: string;
    pullRequestUrl?: string;
    chatHistory?: string;
    // Wave execution state
    currentWave: number;
    waveCount: number;
    maxParallelism: number;
    gateMode?: string;
    gateResult?: string;
    steps: StoryStep[];
    createdAt: string;
    updatedAt: string;
    completedAt?: string;
}

export interface StoryChatResponse {
    response: string;
    planModified: boolean;
    analysisUpdated: boolean;
    stepsAdded: { id: string; order: number; name: string; capability: string }[];
    stepsRemoved: string[];
}

export interface StepChatResponse {
    stepId: string;
    response: string;
    updatedDescription?: string;
}

export class AuraApiService {
    private httpClient: AxiosInstance;
    private cachedGitHubToken: string | null = null;
    private tokenCacheExpiry: number = 0;

    constructor() {
        this.httpClient = axios.create({
            timeout: 30000  // 30s default for general API calls
        });

        // Add request interceptor to automatically include GitHub token
        this.httpClient.interceptors.request.use(async (config) => {
            const token = await this.getGitHubToken();
            if (token) {
                config.headers['X-GitHub-Token'] = token;
            }
            return config;
        });
    }

    /**
     * Gets a GitHub token for CopilotCli dispatcher authentication.
     * Uses VS Code's built-in GitHub authentication provider.
     * Tokens are cached for 5 minutes to avoid repeated auth prompts.
     */
    async getGitHubToken(): Promise<string | null> {
        // Check cache first (tokens are valid for much longer, but we refresh every 5 min)
        const now = Date.now();
        if (this.cachedGitHubToken && now < this.tokenCacheExpiry) {
            return this.cachedGitHubToken;
        }

        try {
            // Request a GitHub session - this will prompt the user if not already authenticated
            const session = await vscode.authentication.getSession('github', ['repo'], {
                createIfNone: false  // Don't force login, just get existing session
            });

            if (session) {
                this.cachedGitHubToken = session.accessToken;
                this.tokenCacheExpiry = now + (5 * 60 * 1000); // Cache for 5 minutes
                return session.accessToken;
            }

            return null;
        } catch (error) {
            console.warn('Failed to get GitHub token:', error);
            return null;
        }
    }

    getBaseUrl(): string {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<string>('apiUrl', 'http://localhost:5300');
    }

    getExecutionTimeout(): number {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<number>('executionTimeout', 120000);  // 2 minutes default
    }

    getStoryTimeout(): number {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<number>('storyTimeout', 60000);  // 1 minute for story operations (worktree creation, etc.)
    }

    getIndexingTimeout(): number {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<number>('indexingTimeout', 300000);  // 5 minutes for indexing
    }

    async getHealth(): Promise<HealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/health`);
        return response.data;
    }

    async getRagHealth(): Promise<RagHealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/health/rag`);
        return response.data;
    }

    async getIndexHealth(workspacePath: string): Promise<IndexHealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/index/health`, {
            params: { workspacePath }
        });
        return response.data;
    }

    async getAgents(): Promise<AgentInfo[]> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/agents`);
        return response.data;
    }

    async getAgentsByCapability(capability: string, language?: string): Promise<AgentInfo[]> {
        let url = `${this.getBaseUrl()}/api/agents?capability=${encodeURIComponent(capability)}`;
        if (language) {
            url += `&language=${encodeURIComponent(language)}`;
        }
        const response = await this.httpClient.get(url);
        return response.data;
    }

    async getBestAgent(capability: string, language?: string): Promise<AgentInfo | null> {
        try {
            let url = `${this.getBaseUrl()}/api/agents/best?capability=${encodeURIComponent(capability)}`;
            if (language) {
                url += `&language=${encodeURIComponent(language)}`;
            }
            const response = await this.httpClient.get(url);
            return response.data;
        } catch {
            return null;
        }
    }

    async getAgentById(agentId: string): Promise<AgentInfo | null> {
        try {
            const response = await this.httpClient.get(`${this.getBaseUrl()}/api/agents/${agentId}`);
            return response.data;
        } catch {
            return null;
        }
    }

    async executeAgent(agentId: string, prompt: string, workspacePath?: string): Promise<string> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/agents/${agentId}/execute`,
            { prompt, workspacePath },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data.content;
    }

    // =====================
    // RAG Methods
    // =====================

    async queryRag(query: string, topK: number = 5): Promise<RagQueryResult[]> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/rag/query`,
            { query, topK },
            { timeout: 30000 }
        );
        return response.data.results;
    }

    async executeAgentWithRag(
        agentId: string,
        prompt: string,
        workspacePath?: string,
        topK: number = 5,
        useRag: boolean = true,
        useCodeGraph: boolean = true
    ): Promise<RagExecuteResult> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/agents/${agentId}/execute/rag`,
            { prompt, workspacePath, useRag, useCodeGraph, topK },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    /**
     * Execute an agent with agentic chat (multi-step tool use).
     * The agent can explore the codebase using tools before answering.
     */
    async executeAgentAgentic(
        agentId: string,
        prompt: string,
        workspacePath?: string,
        maxSteps: number = 10,
        useRag: boolean = true,
        useCodeGraph: boolean = true
    ): Promise<AgenticResult> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/agents/${agentId}/execute/agentic`,
            { prompt, workspacePath, useRag, useCodeGraph, maxSteps },
            { timeout: this.getExecutionTimeout() * 2 } // Allow more time for multi-step
        );
        return response.data;
    }

    /**
     * Execute an agent with streaming responses using Server-Sent Events.
     * Tokens are delivered incrementally via callbacks.
     * @param agentId The agent to execute
     * @param message The user's message
     * @param history Optional conversation history
     * @param callbacks Callbacks for token, done, and error events
     * @param abortController Optional AbortController to cancel the stream
     */
    async executeAgentStreaming(
        agentId: string,
        message: string,
        history: StreamChatMessage[] = [],
        callbacks: StreamChatCallbacks,
        abortController?: AbortController
    ): Promise<void> {
        const url = `${this.getBaseUrl()}/api/agents/${agentId}/chat/stream`;
        
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'text/event-stream'
                },
                body: JSON.stringify({ message, history }),
                signal: abortController?.signal
            });

            if (!response.ok) {
                const errorText = await response.text();
                try {
                    const errorJson = JSON.parse(errorText);
                    callbacks.onError(errorJson.error || 'Request failed', response.status.toString());
                } catch {
                    callbacks.onError(`Request failed: ${response.statusText}`, response.status.toString());
                }
                return;
            }

            const reader = response.body?.getReader();
            if (!reader) {
                callbacks.onError('No response body');
                return;
            }

            const decoder = new TextDecoder();
            let buffer = '';

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                
                // Process complete SSE events (lines ending with \n\n)
                const events = buffer.split('\n\n');
                buffer = events.pop() || ''; // Keep incomplete event in buffer

                for (const event of events) {
                    if (!event.trim()) continue;
                    
                    const lines = event.split('\n');
                    let eventType = '';
                    let eventData = '';

                    for (const line of lines) {
                        if (line.startsWith('event: ')) {
                            eventType = line.slice(7);
                        } else if (line.startsWith('data: ')) {
                            eventData = line.slice(6);
                        }
                    }

                    if (!eventType || !eventData) continue;

                    try {
                        const data = JSON.parse(eventData);
                        
                        switch (eventType) {
                            case 'token':
                                if (data.content) {
                                    callbacks.onToken(data.content);
                                }
                                break;
                            case 'done':
                                callbacks.onDone(data.totalTokens || 0, data.finishReason || 'stop');
                                break;
                            case 'error':
                                callbacks.onError(data.message || 'Unknown error', data.code);
                                break;
                        }
                    } catch (parseError) {
                        console.error('Failed to parse SSE event:', eventData, parseError);
                    }
                }
            }
        } catch (error) {
            if (error instanceof Error) {
                if (error.name === 'AbortError') {
                    // Request was cancelled - this is normal
                    return;
                }
                callbacks.onError(error.message);
            } else {
                callbacks.onError('Unknown error during streaming');
            }
        }
    }

    async getRagStats(): Promise<{ totalDocuments: number; totalChunks: number }> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/rag/stats`);
        return response.data;
    }

    async getBackgroundJobStatus(jobId: string): Promise<BackgroundJobStatus> {
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/index/jobs/${jobId}`,
            { timeout: 5000 }
        );
        return response.data;
    }

    // =====================
    // Code Graph Methods
    // =====================

    /**
     * Get code graph statistics, optionally filtered by repository.
     */
    async getCodeGraphStats(repositoryPath?: string): Promise<{
        totalNodes: number;
        totalEdges: number;
        nodesByType: Record<string, number>;
        edgesByType: Record<string, number>;
        repositoryPath?: string;
    }> {
        const params = repositoryPath ? { repositoryPath } : {};
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/graph/stats`,
            { params, timeout: 5000 }
        );
        return response.data;
    }

    /**
     * Find implementations of an interface or abstract class.
     */
    async findImplementations(interfaceName: string, repositoryPath?: string): Promise<CodeGraphNode[]> {
        const params = repositoryPath ? { repositoryPath } : {};
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/graph/implementations/${encodeURIComponent(interfaceName)}`,
            { params, timeout: 10000 }
        );
        return response.data.implementations || [];
    }

    /**
     * Find callers of a method.
     */
    async findCallers(methodName: string, containingType?: string, repositoryPath?: string): Promise<CodeGraphNode[]> {
        const params: Record<string, string> = {};
        if (containingType) params.containingType = containingType;
        if (repositoryPath) params.repositoryPath = repositoryPath;
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/graph/callers/${encodeURIComponent(methodName)}`,
            { params, timeout: 10000 }
        );
        return response.data.callers || [];
    }

    /**
     * Get members of a type (methods, properties, fields).
     */
    async getTypeMembers(typeName: string, repositoryPath?: string): Promise<CodeGraphNode[]> {
        const params = repositoryPath ? { repositoryPath } : {};
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/graph/members/${encodeURIComponent(typeName)}`,
            { params, timeout: 10000 }
        );
        return response.data.members || [];
    }

    /**
     * Find nodes by name (fuzzy search).
     */
    async findNodes(name: string, nodeType?: string, repositoryPath?: string): Promise<CodeGraphNode[]> {
        const params: Record<string, string> = {};
        if (nodeType) params.nodeType = nodeType;
        if (repositoryPath) params.repositoryPath = repositoryPath;
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/graph/find/${encodeURIComponent(name)}`,
            { params, timeout: 10000 }
        );
        return response.data.nodes || [];
    }

    // =====================
    // Developer Module Methods
    // =====================

    async createStory(title: string, description?: string, repositoryPath?: string): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories`,
            { title, description, repositoryPath },
            { timeout: this.getStoryTimeout() }
        );
        return response.data;
    }

    async getStories(status?: string, repositoryPath?: string): Promise<Story[]> {
        const params = new URLSearchParams();
        if (status) {
            params.append('status', status);
        }
        if (repositoryPath) {
            params.append('repositoryPath', repositoryPath);
        }
        const queryString = params.toString();
        const url = `${this.getBaseUrl()}/api/developer/stories${queryString ? '?' + queryString : ''}`;
        const response = await this.httpClient.get(url);
        return response.data.stories;
    }

    async getStory(id: string): Promise<Story> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/developer/stories/${id}`);
        return response.data;
    }

    async getStoryByPath(worktreePath: string): Promise<Story | null> {
        try {
            const params = new URLSearchParams();
            params.append('path', worktreePath);
            const response = await this.httpClient.get(
                `${this.getBaseUrl()}/api/developer/stories/by-path?${params.toString()}`
            );
            return response.data;
        } catch (error: unknown) {
            if (axios.isAxiosError(error) && error.response?.status === 404) {
                return null;
            }
            throw error;
        }
    }

    async deleteStory(id: string): Promise<void> {
        await this.httpClient.delete(`${this.getBaseUrl()}/api/developer/stories/${id}`);
    }

    async analyzeStory(storyId: string): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/analyze`,
            {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async planStory(storyId: string): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/plan`,
            {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async executeStoryStep(storyId: string, stepId: string, agentId?: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/execute`,
            agentId ? { agentId } : {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    /**
     * Stream story execution progress via SSE.
     * Delivers real-time progress events as waves and steps execute.
     * @param storyId The story to execute
     * @param callbacks Callbacks for progress events
     * @param abortController Optional AbortController to cancel the stream
     */
    async streamStoryExecution(
        storyId: string,
        callbacks: StoryStreamCallbacks,
        abortController?: AbortController
    ): Promise<void> {
        const url = `${this.getBaseUrl()}/api/developer/stories/${storyId}/stream`;

        // Get GitHub token for CopilotCli dispatcher authentication
        const githubToken = await this.getGitHubToken();

        try {
            const headers: Record<string, string> = {
                'Accept': 'text/event-stream'
            };

            // Pass GitHub token if available (required for CopilotCli dispatcher)
            if (githubToken) {
                headers['X-GitHub-Token'] = githubToken;
            }

            const response = await fetch(url, {
                method: 'GET',
                headers,
                signal: abortController?.signal
            });

            if (!response.ok) {
                const errorText = await response.text();
                try {
                    const errorJson = JSON.parse(errorText);
                    callbacks.onError(errorJson.message || errorJson.error || 'Request failed');
                } catch {
                    callbacks.onError(`Request failed: ${response.statusText}`);
                }
                return;
            }

            const reader = response.body?.getReader();
            if (!reader) {
                callbacks.onError('No response body');
                return;
            }

            const decoder = new TextDecoder();
            let buffer = '';

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });

                // Process complete SSE events (lines ending with \n\n)
                const events = buffer.split('\n\n');
                buffer = events.pop() || ''; // Keep incomplete event in buffer

                for (const event of events) {
                    if (!event.trim()) continue;

                    const lines = event.split('\n');
                    let eventType = '';
                    let eventData = '';

                    for (const line of lines) {
                        if (line.startsWith('event: ')) {
                            eventType = line.slice(7);
                        } else if (line.startsWith('data: ')) {
                            eventData = line.slice(6);
                        }
                    }

                    if (!eventData) continue;

                    try {
                        if (eventType === 'done') {
                            callbacks.onDone();
                            return;
                        }

                        if (eventType === 'error') {
                            const data = JSON.parse(eventData);
                            callbacks.onError(data.message || 'Unknown error');
                            return;
                        }

                        const data = JSON.parse(eventData) as StoryProgressEvent;
                        callbacks.onEvent(data);
                    } catch (parseError) {
                        console.error('Failed to parse SSE event:', eventData, parseError);
                    }
                }
            }

            callbacks.onDone();
        } catch (error) {
            if (error instanceof Error) {
                if (error.name === 'AbortError') {
                    // Request was cancelled - this is normal
                    return;
                }
                callbacks.onError(error.message);
            } else {
                callbacks.onError('Unknown error during streaming');
            }
        }
    }

    async addStoryStep(storyId: string, name: string, capability: string, description?: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps`,
            { name, capability, description }
        );
        return response.data;
    }

    async removeStoryStep(storyId: string, stepId: string): Promise<void> {
        await this.httpClient.delete(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}`
        );
    }

    async completeStory(storyId: string): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/complete`
        );
        return response.data;
    }

    async cancelStory(storyId: string): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/cancel`
        );
        return response.data;
    }

    async sendStoryChat(storyId: string, message: string): Promise<StoryChatResponse> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/chat`,
            { message },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    // Step management methods

    async approveStepOutput(storyId: string, stepId: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/approve`
        );
        return response.data;
    }

    async rejectStepOutput(storyId: string, stepId: string, feedback?: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/reject`,
            feedback ? { feedback } : {}
        );
        return response.data;
    }

    async skipStep(storyId: string, stepId: string, reason?: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/skip`,
            reason ? { reason } : {}
        );
        return response.data;
    }

    async resetStep(storyId: string, stepId: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/reset`,
            {}
        );
        return response.data;
    }

    async chatWithStep(storyId: string, stepId: string, message: string): Promise<StepChatResponse> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/chat`,
            { message },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async reassignStep(storyId: string, stepId: string, agentId: string): Promise<StoryStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/reassign`,
            { agentId }
        );
        return response.data;
    }

    async updateStepDescription(storyId: string, stepId: string, description: string): Promise<StoryStep> {
        const response = await this.httpClient.put(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/steps/${stepId}/description`,
            { description }
        );
        return response.data;
    }

    async finalizeStory(storyId: string, options: {
        commitMessage?: string;
        createPullRequest?: boolean;
        prTitle?: string;
        prBody?: string;
        baseBranch?: string;
        draft?: boolean;
    } = {}): Promise<FinalizeResult> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/finalize`,
            {
                commitMessage: options.commitMessage,
                createPullRequest: options.createPullRequest ?? true,
                prTitle: options.prTitle,
                prBody: options.prBody,
                baseBranch: options.baseBranch,
                draft: options.draft ?? true
            },
            { timeout: 120000 } // 2 minute timeout for git operations
        );
        return response.data;
    }

    async getGitStatus(path: string): Promise<GitStatusResponse> {
        try {
            const response = await this.httpClient.get(
                `${this.getBaseUrl()}/api/git/status`,
                { params: { path }, timeout: 10000 }
            );
            return response.data;
        } catch (error) {
            return {
                success: false,
                error: error instanceof Error ? error.message : 'Failed to get git status'
            };
        }
    }

    // =====================
    // Workspace Methods
    // =====================

    /**
     * List all workspaces.
     */
    async listWorkspaces(limit?: number): Promise<{ count: number; workspaces: WorkspaceInfo[] }> {
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/workspaces`,
            { params: limit ? { limit } : undefined, timeout: 5000 }
        );
        return response.data;
    }

    /**
     * Get workspace by ID or path.
     * The API accepts either a 16-char hex ID or a URL-encoded filesystem path.
     */
    async getWorkspace(idOrPath: string): Promise<WorkspaceInfo> {
        // URL-encode the path if it's not already an ID (16 hex chars)
        const isId = /^[0-9a-f]{16}$/i.test(idOrPath);
        // Normalize paths to match API expectations (forward slashes, lowercase)
        const normalizedValue = isId ? idOrPath : normalizePath(idOrPath);
        const param = isId ? idOrPath : encodeURIComponent(normalizedValue);
        
        const response = await this.httpClient.get(
            `${this.getBaseUrl()}/api/workspaces/${param}`,
            { timeout: 5000 }
        );
        return response.data;
    }

    /**
     * Get the onboarding status of a workspace by path.
     * Returns a status object (with isOnboarded=false if not found).
     */
    async getWorkspaceStatus(path: string): Promise<WorkspaceStatus> {
        try {
            const workspace = await this.getWorkspace(path);
            return {
                path: workspace.path,
                isOnboarded: workspace.status === 'ready' || workspace.status === 'indexing',
                onboardedAt: workspace.createdAt,
                lastIndexedAt: workspace.lastAccessedAt,
                indexHealth: workspace.status === 'ready' ? 'fresh' : workspace.status,
                stats: workspace.stats ?? { files: 0, chunks: 0, graphNodes: 0, graphEdges: 0 }
            };
        } catch (error: unknown) {
            if (error && typeof error === 'object' && 'response' in error) {
                const axiosError = error as { response?: { status?: number } };
                if (axiosError.response?.status === 404) {
                    return {
                        path,
                        isOnboarded: false,
                        indexHealth: 'not-indexed',
                        stats: { files: 0, chunks: 0, graphNodes: 0, graphEdges: 0 }
                    };
                }
            }
            throw error;
        }
    }

    /**
     * Onboard a workspace (create and start indexing).
     */
    async onboardWorkspace(path: string, options?: {
        name?: string;
        includePatterns?: string[];
        excludePatterns?: string[];
    }): Promise<OnboardResult> {
        // Normalize path for consistent storage
        const normalizedPath = normalizePath(path);
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/workspaces`,
            {
                path: normalizedPath,
                name: options?.name,
                startIndexing: true,
                options: options?.includePatterns || options?.excludePatterns ? {
                    includePatterns: options?.includePatterns,
                    excludePatterns: options?.excludePatterns
                } : undefined
            },
            { timeout: 30000 }
        );
        return {
            success: true,
            jobId: response.data.jobId,
            message: response.data.message,
            workspaceId: response.data.id
        };
    }

    /**
     * Remove a workspace from Aura (delete all indexed data).
     */
    async removeWorkspace(path: string): Promise<{ success: boolean; message: string }> {
        const workspace = await this.getWorkspace(path);
        if (!workspace) {
            return { success: true, message: 'Workspace not found (already removed)' };
        }
        const response = await this.httpClient.delete(
            `${this.getBaseUrl()}/api/workspaces/${workspace.id}`,
            { timeout: 30000 }
        );
        return response.data;
    }

    /**
     * Re-index an existing workspace.
     */
    async reindexWorkspace(path: string): Promise<{ jobId: string; message: string }> {
        const workspace = await this.getWorkspace(path);
        if (!workspace) {
            throw new Error('Workspace not found. Please onboard the workspace first.');
        }
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/workspaces/${workspace.id}/reindex`,
            {},
            { timeout: 30000 }
        );
        return {
            jobId: response.data.jobId,
            message: response.data.message
        };
    }

    // =====================
    // Story/Issue Integration Methods
    // =====================

    /**
     * Create a story from a GitHub issue URL.
     */
    async createStoryFromIssue(
        issueUrl: string,
        repositoryPath?: string
    ): Promise<Story> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/from-issue`,
            { issueUrl, repositoryPath, createWorktree: true },
            { timeout: this.getStoryTimeout() }
        );
        return response.data;
    }

    /**
     * Refresh a Story/story from its linked GitHub issue.
     */
    async refreshFromIssue(storyId: string): Promise<{ updated: boolean; changes: string[] }> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/refresh-from-issue`,
            {},
            { timeout: 10000 }
        );
        return response.data;
    }

    /**
     * Post an update comment to the linked GitHub issue.
     */
    async postUpdateToIssue(storyId: string, message: string): Promise<{ posted: boolean }> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/post-update`,
            { message },
            { timeout: 10000 }
        );
        return response.data;
    }

    /**
     * Close the linked GitHub issue.
     */
    async closeLinkedIssue(storyId: string, comment?: string): Promise<{ closed: boolean }> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/stories/${storyId}/close-issue`,
            { comment },
            { timeout: 10000 }
        );
        return response.data;
    }
}

export interface WorkspaceInfo {
    id: string;
    name: string;
    path: string;
    status: string;
    errorMessage?: string;
    createdAt?: string;
    lastAccessedAt?: string;
    gitRemoteUrl?: string;
    defaultBranch?: string;
    stats?: {
        files: number;
        chunks: number;
        graphNodes: number;
        graphEdges: number;
    };
}

export interface WorkspaceStatus {
    path: string;
    isOnboarded: boolean;
    onboardedAt?: string;
    lastIndexedAt?: string;
    indexHealth: string;
    stats: {
        files: number;
        chunks: number;
        graphNodes: number;
        graphEdges: number;
    };
}

export interface OnboardResult {
    success: boolean;
    jobId?: string;
    message: string;
    workspaceId?: string;
    setupActions?: string[];
}

export interface FinalizeResult {
    storyId: string;
    commitSha?: string;
    pushed: boolean;
    prNumber?: number;
    prUrl?: string;
    message: string;
}

export interface GitStatusResponse {
    success: boolean;
    branch?: string;
    isDirty?: boolean;
    modifiedFiles?: string[];
    untrackedFiles?: string[];
    stagedFiles?: string[];
    error?: string;
}

export interface CodeGraphNode {
    name: string;
    fullName: string;
    nodeType?: string;
    signature?: string;
    modifiers?: string[];
    filePath?: string;
    lineNumber?: number;
}
