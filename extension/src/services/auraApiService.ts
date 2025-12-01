import * as vscode from 'vscode';
import axios, { AxiosInstance } from 'axios';

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

export interface RagIndexResult {
    success: boolean;
    filesIndexed: number;
    message: string;
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

// =====================
// Developer Module Types
// =====================

export interface WorkflowStep {
    id: string;
    order: number;
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
}

export interface Workflow {
    id: string;
    title: string;
    description?: string;
    status: string;
    gitBranch?: string;
    workspacePath?: string;
    repositoryPath?: string;
    analyzedContext?: string;
    executionPlan?: string;
    steps: WorkflowStep[];
    createdAt: string;
    updatedAt: string;
    completedAt?: string;
}

export interface WorkflowChatResponse {
    response: string;
    planModified: boolean;
    stepsAdded: { id: string; order: number; name: string; capability: string }[];
    stepsRemoved: string[];
}

export class AuraApiService {
    private httpClient: AxiosInstance;

    constructor() {
        this.httpClient = axios.create({
            timeout: 10000  // 10s for health checks and agent list
        });
    }

    getBaseUrl(): string {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<string>('apiUrl', 'http://localhost:5300');
    }

    getExecutionTimeout(): number {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<number>('executionTimeout', 120000);  // 2 minutes default
    }

    async getHealth(): Promise<HealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/health`);
        return response.data;
    }

    async getRagHealth(): Promise<RagHealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/health/rag`);
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

    async indexDirectory(
        path: string,
        includePatterns?: string[],
        excludePatterns?: string[],
        recursive: boolean = true
    ): Promise<RagIndexResult> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/rag/index/directory`,
            { path, includePatterns, excludePatterns, recursive },
            { timeout: 300000 }  // 5 minutes for indexing
        );
        return response.data;
    }

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
        topK: number = 5
    ): Promise<RagExecuteResult> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/agents/${agentId}/execute/rag`,
            { prompt, workspacePath, useRag: true, topK },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async getRagStats(): Promise<{ totalDocuments: number; totalChunks: number }> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/rag/stats`);
        return response.data;
    }

    async clearRagIndex(): Promise<void> {
        await this.httpClient.delete(`${this.getBaseUrl()}/api/rag`);
    }

    // =====================
    // Developer Module Methods
    // =====================

    async createWorkflow(title: string, description?: string, repositoryPath?: string): Promise<Workflow> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows`,
            { title, description, repositoryPath }
        );
        return response.data;
    }

    async getWorkflows(status?: string): Promise<Workflow[]> {
        let url = `${this.getBaseUrl()}/api/developer/workflows`;
        if (status) {
            url += `?status=${encodeURIComponent(status)}`;
        }
        const response = await this.httpClient.get(url);
        return response.data.workflows;
    }

    async getWorkflow(id: string): Promise<Workflow> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/developer/workflows/${id}`);
        return response.data;
    }

    async deleteWorkflow(id: string): Promise<void> {
        await this.httpClient.delete(`${this.getBaseUrl()}/api/developer/workflows/${id}`);
    }

    async analyzeWorkflow(workflowId: string): Promise<Workflow> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/analyze`,
            {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async planWorkflow(workflowId: string): Promise<Workflow> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/plan`,
            {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async executeWorkflowStep(workflowId: string, stepId: string, agentId?: string): Promise<WorkflowStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/steps/${stepId}/execute`,
            agentId ? { agentId } : {},
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }

    async addWorkflowStep(workflowId: string, name: string, capability: string, description?: string): Promise<WorkflowStep> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/steps`,
            { name, capability, description }
        );
        return response.data;
    }

    async removeWorkflowStep(workflowId: string, stepId: string): Promise<void> {
        await this.httpClient.delete(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/steps/${stepId}`
        );
    }

    async completeWorkflow(workflowId: string): Promise<Workflow> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/complete`
        );
        return response.data;
    }

    async cancelWorkflow(workflowId: string): Promise<Workflow> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/cancel`
        );
        return response.data;
    }

    async sendWorkflowChat(workflowId: string, message: string): Promise<WorkflowChatResponse> {
        const response = await this.httpClient.post(
            `${this.getBaseUrl()}/api/developer/workflows/${workflowId}/chat`,
            { message },
            { timeout: this.getExecutionTimeout() }
        );
        return response.data;
    }
}
