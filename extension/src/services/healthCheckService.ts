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

export interface OllamaModelInfo {
    name: string;
    size: number;  // bytes
    parameterSize: string;
    quantization: string;
    family: string;
}

export interface OllamaRunningModel {
    name: string;
    sizeVram: number;  // bytes in VRAM
    expiresAt: Date;
}

export interface OllamaStatus extends ServiceStatus {
    models?: OllamaModelInfo[];
    runningModels?: OllamaRunningModel[];
    totalModelSize?: number;  // total size of all models on disk
}

export interface RagStatus extends ServiceStatus {
    totalDocuments?: number;
    totalChunks?: number;
    chunksByType?: Record<string, number>;
    // Repository-filtered stats (indexing is per-repo, not per-workspace)
    repoDocuments?: number;
    repoChunks?: number;
    repositoryPath?: string;
    // Code graph stats
    graphNodes?: number;
    graphEdges?: number;
}

export interface HealthStatuses {
    api: ServiceStatus;
    ollama: OllamaStatus;
    database: ServiceStatus;
    rag: RagStatus;
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
            
            // Get available models
            const tagsResponse = await this.httpClient.get(`${url}/api/tags`);
            const responseTime = Date.now() - start;

            const rawModels = tagsResponse.data?.models || [];
            const models: OllamaModelInfo[] = rawModels.map((m: any) => ({
                name: m.name,
                size: m.size || 0,
                parameterSize: m.details?.parameter_size || 'unknown',
                quantization: m.details?.quantization_level || 'unknown',
                family: m.details?.family || 'unknown'
            }));

            const totalModelSize = models.reduce((sum, m) => sum + m.size, 0);

            // Get running models (loaded in memory)
            let runningModels: OllamaRunningModel[] = [];
            try {
                const psResponse = await this.httpClient.get(`${url}/api/ps`);
                const rawRunning = psResponse.data?.models || [];
                runningModels = rawRunning.map((m: any) => ({
                    name: m.name,
                    sizeVram: m.size_vram || m.size || 0,
                    expiresAt: m.expires_at ? new Date(m.expires_at) : new Date()
                }));
            } catch {
                // /api/ps might not be available in older Ollama versions
            }

            const modelCount = models.length;
            const loadedCount = runningModels.length;
            const loadedVram = runningModels.reduce((sum, m) => sum + m.sizeVram, 0);

            let details = `${modelCount} model${modelCount !== 1 ? 's' : ''}`;
            if (loadedCount > 0) {
                details += ` (${loadedCount} loaded, ${this.formatBytes(loadedVram)} VRAM)`;
            }

            this.statuses.ollama = {
                status: 'healthy',
                url,
                responseTime,
                details,
                models,
                runningModels,
                totalModelSize,
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

            const totalDocuments = response.data?.totalDocuments || 0;
            const totalChunks = response.data?.totalChunks || 0;

            // Get current repository path (first workspace folder, which is typically the git root)
            // This matches how indexing works - we index by repository path, not VS Code workspace
            const repositoryPath = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

            // Fetch repository-filtered stats if we have a repository open
            let repoDocuments = 0;
            let repoChunks = 0;
            let graphNodes = 0;
            let graphEdges = 0;
            let chunksByType: Record<string, number> = {};

            try {
                if (repositoryPath) {
                    // Get RAG stats filtered by repository path
                    const dirStatsResponse = await this.httpClient.get(
                        `${url}/api/rag/stats/directory`,
                        { params: { path: repositoryPath } }
                    );
                    repoChunks = dirStatsResponse.data?.chunkCount || 0;
                    repoDocuments = dirStatsResponse.data?.fileCount || 0;

                    // Get code graph stats filtered by repository path
                    const graphStatsResponse = await this.httpClient.get(
                        `${url}/api/graph/stats`,
                        { params: { workspacePath: repositoryPath } }
                    );
                    graphNodes = graphStatsResponse.data?.totalNodes || 0;
                    graphEdges = graphStatsResponse.data?.totalEdges || 0;
                }

                // Also get overall stats
                const statsResponse = await this.httpClient.get(`${url}/api/rag/stats`);
                chunksByType = statsResponse.data?.chunksByType || {};
            } catch {
                // Stats endpoints might fail, continue anyway
            }

            // Build details string based on whether we have repository filtering
            let details: string;
            if (repositoryPath && (repoDocuments > 0 || repoChunks > 0 || graphNodes > 0)) {
                details = `${repoDocuments} files, ${repoChunks} chunks, ${graphNodes} graph nodes`;
            } else if (repositoryPath) {
                details = `Not indexed (${totalDocuments} symbols in other repos)`;
            } else {
                details = `${totalDocuments} symbols, ${totalChunks} embeddings (all repos)`;
            }

            this.statuses.rag = {
                status: response.data?.healthy ? 'healthy' : 'unhealthy',
                responseTime,
                details,
                totalDocuments,
                totalChunks,
                chunksByType,
                repoDocuments,
                repoChunks,
                repositoryPath,
                graphNodes,
                graphEdges,
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

    private formatBytes(bytes: number): string {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }
}
