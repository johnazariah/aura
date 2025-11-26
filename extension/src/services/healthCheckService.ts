import * as vscode from 'vscode';
import axios, { AxiosInstance } from 'axios';

export interface ServiceStatus {
    status: 'healthy' | 'unhealthy' | 'checking' | 'unknown';
    url?: string;
    responseTime?: number;
    details?: string;
    error?: string;
    lastChecked?: Date;
}

export interface HealthStatuses {
    api: ServiceStatus;
    ollama: ServiceStatus;
    database: ServiceStatus;
    rag: ServiceStatus;
}

export class HealthCheckService {
    private statuses: HealthStatuses = {
        api: { status: 'unknown' },
        ollama: { status: 'unknown' },
        database: { status: 'unknown' },
        rag: { status: 'unknown' }
    };

    private httpClient: AxiosInstance;

    constructor(private apiService: { getBaseUrl(): string }) {
        this.httpClient = axios.create({
            timeout: 5000
        });
    }

    async checkAll(): Promise<void> {
        // Run health checks in parallel
        await Promise.all([
            this.checkApi(),
            this.checkOllama(),
            this.checkDatabase(),
            this.checkRag()
        ]);
    }

    getStatuses(): HealthStatuses {
        return { ...this.statuses };
    }

    getOverallStatus(): 'healthy' | 'degraded' | 'error' | 'checking' {
        const values = Object.values(this.statuses);
        
        if (values.some(s => s.status === 'checking')) {
            return 'checking';
        }
        
        if (values.every(s => s.status === 'healthy')) {
            return 'healthy';
        }
        
        if (values.every(s => s.status === 'unhealthy')) {
            return 'error';
        }
        
        return 'degraded';
    }

    private async checkApi(): Promise<void> {
        const url = this.apiService.getBaseUrl();
        this.statuses.api = { status: 'checking', url };

        try {
            const start = Date.now();
            const response = await this.httpClient.get(`${url}/health`);
            const responseTime = Date.now() - start;

            this.statuses.api = {
                status: 'healthy',
                url,
                responseTime,
                details: response.data?.status || 'Connected',
                lastChecked: new Date()
            };
        } catch (error) {
            this.statuses.api = {
                status: 'unhealthy',
                url,
                error: this.getErrorMessage(error),
                lastChecked: new Date()
            };
        }
    }

    private async checkOllama(): Promise<void> {
        const config = vscode.workspace.getConfiguration('aura');
        const url = config.get<string>('ollamaUrl', 'http://localhost:11434');
        this.statuses.ollama = { status: 'checking', url };

        try {
            const start = Date.now();
            const response = await this.httpClient.get(`${url}/api/tags`);
            const responseTime = Date.now() - start;

            const models = response.data?.models || [];
            const modelCount = models.length;

            this.statuses.ollama = {
                status: 'healthy',
                url,
                responseTime,
                details: `${modelCount} model${modelCount !== 1 ? 's' : ''} available`,
                lastChecked: new Date()
            };
        } catch (error) {
            this.statuses.ollama = {
                status: 'unhealthy',
                url,
                error: this.getErrorMessage(error),
                lastChecked: new Date()
            };
        }
    }

    private async checkDatabase(): Promise<void> {
        // Database health is checked via the API
        const url = this.apiService.getBaseUrl();
        this.statuses.database = { status: 'checking' };

        try {
            const start = Date.now();
            const response = await this.httpClient.get(`${url}/health/db`);
            const responseTime = Date.now() - start;

            this.statuses.database = {
                status: response.data?.healthy ? 'healthy' : 'unhealthy',
                responseTime,
                details: response.data?.details || 'Connected',
                lastChecked: new Date()
            };
        } catch (error) {
            // If API is down, we can't check DB
            if (this.statuses.api.status === 'unhealthy') {
                this.statuses.database = {
                    status: 'unknown',
                    error: 'API unavailable',
                    lastChecked: new Date()
                };
            } else {
                this.statuses.database = {
                    status: 'unhealthy',
                    error: this.getErrorMessage(error),
                    lastChecked: new Date()
                };
            }
        }
    }

    private async checkRag(): Promise<void> {
        // RAG health is checked via the API
        const url = this.apiService.getBaseUrl();
        this.statuses.rag = { status: 'checking' };

        try {
            const start = Date.now();
            const response = await this.httpClient.get(`${url}/health/rag`);
            const responseTime = Date.now() - start;

            this.statuses.rag = {
                status: response.data?.healthy ? 'healthy' : 'unhealthy',
                responseTime,
                details: response.data?.details || 'Index ready',
                lastChecked: new Date()
            };
        } catch (error) {
            // If API is down, we can't check RAG
            if (this.statuses.api.status === 'unhealthy') {
                this.statuses.rag = {
                    status: 'unknown',
                    error: 'API unavailable',
                    lastChecked: new Date()
                };
            } else {
                this.statuses.rag = {
                    status: 'unhealthy',
                    error: this.getErrorMessage(error),
                    lastChecked: new Date()
                };
            }
        }
    }

    private getErrorMessage(error: unknown): string {
        if (axios.isAxiosError(error)) {
            if (error.code === 'ECONNREFUSED') {
                return 'Connection refused';
            }
            if (error.code === 'ETIMEDOUT') {
                return 'Connection timed out';
            }
            if (error.response) {
                return `HTTP ${error.response.status}`;
            }
            return error.message;
        }
        return 'Unknown error';
    }
}
