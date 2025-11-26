import * as vscode from 'vscode';
import axios, { AxiosInstance } from 'axios';

export interface AgentInfo {
    id: string;
    name: string;
    description: string;
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

export class AuraApiService {
    private httpClient: AxiosInstance;

    constructor() {
        this.httpClient = axios.create({
            timeout: 10000
        });
    }

    getBaseUrl(): string {
        const config = vscode.workspace.getConfiguration('aura');
        return config.get<string>('apiUrl', 'http://localhost:5300');
    }

    async getHealth(): Promise<HealthResponse> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/health`);
        return response.data;
    }

    async getAgents(): Promise<AgentInfo[]> {
        const response = await this.httpClient.get(`${this.getBaseUrl()}/api/agents`);
        return response.data;
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
        const response = await this.httpClient.post(`${this.getBaseUrl()}/api/agents/${agentId}/execute`, {
            prompt,
            workspacePath
        });
        return response.data.content;
    }
}
