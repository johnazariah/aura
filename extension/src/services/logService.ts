import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';

/**
 * Service for viewing Aura API logs in VS Code
 */
export class LogService {
    private outputChannel: vscode.OutputChannel;
    private logWatcher: fs.FSWatcher | undefined;
    private currentLogFile: string | undefined;
    private lastPosition: number = 0;

    constructor() {
        this.outputChannel = vscode.window.createOutputChannel('Aura');
    }

    /**
     * Get the log directory path
     */
    private getLogDirectory(): string {
        if (process.platform === 'win32') {
            return path.join(process.env.PROGRAMDATA || 'C:\\ProgramData', 'Aura', 'logs');
        } else if (process.platform === 'darwin') {
            return path.join(process.env.HOME || '~', 'Library', 'Application Support', 'Aura', 'logs');
        } else {
            return path.join(process.env.HOME || '~', '.aura', 'logs');
        }
    }

    /**
     * Find the most recent log file
     */
    private findLatestLogFile(): string | undefined {
        const logDir = this.getLogDirectory();
        
        if (!fs.existsSync(logDir)) {
            return undefined;
        }

        const files = fs.readdirSync(logDir)
            .filter(f => f.startsWith('aura-') && f.endsWith('.log'))
            .map(f => ({
                name: f,
                path: path.join(logDir, f),
                mtime: fs.statSync(path.join(logDir, f)).mtime
            }))
            .sort((a, b) => b.mtime.getTime() - a.mtime.getTime());

        return files.length > 0 ? files[0].path : undefined;
    }

    /**
     * Show the output channel and start tailing logs
     */
    public showLogs(): void {
        this.outputChannel.show(true);
        this.startTailing();
    }

    /**
     * Start tailing the log file
     */
    public startTailing(): void {
        this.stopTailing();

        const logFile = this.findLatestLogFile();
        
        if (!logFile) {
            this.outputChannel.appendLine('No Aura log files found.');
            this.outputChannel.appendLine(`Expected location: ${this.getLogDirectory()}`);
            this.outputChannel.appendLine('');
            this.outputChannel.appendLine('The Aura API service may not be running or has not written any logs yet.');
            return;
        }

        this.currentLogFile = logFile;
        this.outputChannel.appendLine(`=== Aura Logs: ${path.basename(logFile)} ===`);
        this.outputChannel.appendLine(`Log file: ${logFile}`);
        this.outputChannel.appendLine('');

        // Read existing content
        this.readNewContent();

        // Watch for changes
        const logDir = path.dirname(logFile);
        this.logWatcher = fs.watch(logDir, (eventType, filename) => {
            if (filename && filename.startsWith('aura-')) {
                // Check if there's a newer log file
                const latestLog = this.findLatestLogFile();
                if (latestLog && latestLog !== this.currentLogFile) {
                    this.outputChannel.appendLine('');
                    this.outputChannel.appendLine(`=== Switched to: ${path.basename(latestLog)} ===`);
                    this.currentLogFile = latestLog;
                    this.lastPosition = 0;
                }
                this.readNewContent();
            }
        });
    }

    /**
     * Read new content from the log file
     */
    private readNewContent(): void {
        if (!this.currentLogFile || !fs.existsSync(this.currentLogFile)) {
            return;
        }

        try {
            const stats = fs.statSync(this.currentLogFile);
            if (stats.size > this.lastPosition) {
                const stream = fs.createReadStream(this.currentLogFile, {
                    start: this.lastPosition,
                    encoding: 'utf8'
                });

                stream.on('data', (chunk) => {
                    // Split into lines and append each one
                    const text = typeof chunk === 'string' ? chunk : chunk.toString('utf8');
                    const lines = text.split('\n');
                    for (const line of lines) {
                        if (line.trim()) {
                            this.outputChannel.appendLine(line);
                        }
                    }
                });

                stream.on('end', () => {
                    this.lastPosition = stats.size;
                });
            }
        } catch (error) {
            // File might be locked or rotated, ignore
        }
    }

    /**
     * Stop tailing logs
     */
    public stopTailing(): void {
        if (this.logWatcher) {
            this.logWatcher.close();
            this.logWatcher = undefined;
        }
        this.lastPosition = 0;
        this.currentLogFile = undefined;
    }

    /**
     * Clear the output channel
     */
    public clear(): void {
        this.outputChannel.clear();
    }

    /**
     * Log a message to the output channel
     */
    public log(message: string): void {
        this.outputChannel.appendLine(`[Extension] ${message}`);
    }

    /**
     * Log an error to the output channel
     */
    public error(message: string, error?: Error): void {
        this.outputChannel.appendLine(`[Extension Error] ${message}`);
        if (error) {
            this.outputChannel.appendLine(`  ${error.message}`);
            if (error.stack) {
                this.outputChannel.appendLine(`  ${error.stack}`);
            }
        }
    }

    /**
     * Open the log file in VS Code
     */
    public async openLogFile(): Promise<void> {
        const logFile = this.findLatestLogFile();
        
        if (!logFile) {
            vscode.window.showWarningMessage('No Aura log files found.');
            return;
        }

        const doc = await vscode.workspace.openTextDocument(logFile);
        await vscode.window.showTextDocument(doc);
    }

    /**
     * Open the log folder in the file explorer
     */
    public openLogFolder(): void {
        const logDir = this.getLogDirectory();
        
        if (!fs.existsSync(logDir)) {
            vscode.window.showWarningMessage(`Log directory does not exist: ${logDir}`);
            return;
        }

        vscode.env.openExternal(vscode.Uri.file(logDir));
    }

    /**
     * Dispose the service
     */
    public dispose(): void {
        this.stopTailing();
        this.outputChannel.dispose();
    }
}
