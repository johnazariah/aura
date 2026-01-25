import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { execSync } from 'child_process';

export interface GitRepositoryInfo {
    /** The canonical path to the git repository root (parent for worktrees) */
    canonicalPath: string;
    /** Whether the current path is a git worktree */
    isWorktree: boolean;
    /** The parent repository path if this is a worktree */
    parentPath?: string;
    /** The worktree name if this is a worktree */
    worktreeName?: string;
}

/**
 * Service for git-related operations in the extension.
 */
export class GitService {
    /**
     * Get the git repository root information for a given path.
     * Detects if the path is a worktree and returns the parent repo path.
     */
    async getRepositoryInfo(workspacePath: string): Promise<GitRepositoryInfo | null> {
        try {
            const gitPath = path.join(workspacePath, '.git');

            // Check if .git exists
            if (!fs.existsSync(gitPath)) {
                console.log('[GitService] No .git found at:', gitPath);
                return null;
            }

            const stats = fs.statSync(gitPath);

            if (stats.isDirectory()) {
                // Normal git repository - .git is a directory
                console.log('[GitService] Normal git repository:', workspacePath);
                return {
                    canonicalPath: workspacePath,
                    isWorktree: false
                };
            }

            if (stats.isFile()) {
                // Worktree - .git is a file containing path to parent
                console.log('[GitService] Detected worktree at:', workspacePath);
                return this.parseWorktreeGitFile(workspacePath, gitPath);
            }

            return null;
        } catch (error) {
            console.error('[GitService] Error getting repository info:', error);
            return null;
        }
    }

    /**
     * Parse the .git file in a worktree to find the parent repository.
     */
    private parseWorktreeGitFile(workspacePath: string, gitFilePath: string): GitRepositoryInfo | null {
        try {
            // .git file contains: "gitdir: /path/to/parent/.git/worktrees/worktree-name"
            const content = fs.readFileSync(gitFilePath, 'utf-8').trim();
            console.log('[GitService] .git file content:', content);

            const match = content.match(/^gitdir:\s*(.+)$/);
            if (!match) {
                console.log('[GitService] Could not parse .git file');
                return null;
            }

            const gitdir = match[1].trim();
            console.log('[GitService] gitdir:', gitdir);

            // gitdir format: /path/to/parent/.git/worktrees/worktree-name
            // We need to extract /path/to/parent
            const worktreesIndex = gitdir.indexOf('.git' + path.sep + 'worktrees');
            if (worktreesIndex === -1) {
                // Try with forward slash (git often uses forward slashes)
                const worktreesIndexAlt = gitdir.indexOf('.git/worktrees');
                if (worktreesIndexAlt === -1) {
                    console.log('[GitService] Not a standard worktree path:', gitdir);
                    return null;
                }
                
                const parentPath = gitdir.substring(0, worktreesIndexAlt).replace(/\/$/, '');
                const worktreeName = path.basename(gitdir);
                
                console.log('[GitService] Parent path (alt):', parentPath);
                console.log('[GitService] Worktree name:', worktreeName);
                
                return {
                    canonicalPath: parentPath,
                    isWorktree: true,
                    parentPath: parentPath,
                    worktreeName: worktreeName
                };
            }

            const parentPath = gitdir.substring(0, worktreesIndex).replace(/[\\/]$/, '');
            const worktreeName = path.basename(gitdir);

            console.log('[GitService] Parent path:', parentPath);
            console.log('[GitService] Worktree name:', worktreeName);

            return {
                canonicalPath: parentPath,
                isWorktree: true,
                parentPath: parentPath,
                worktreeName: worktreeName
            };
        } catch (error) {
            console.error('[GitService] Error parsing .git file:', error);
            return null;
        }
    }

    /**
     * Get the git repository root using git rev-parse.
     * This is a fallback method if .git file parsing fails.
     */
    async getRepositoryRootViaGit(workspacePath: string): Promise<string | null> {
        try {
            const result = execSync('git rev-parse --show-toplevel', {
                cwd: workspacePath,
                encoding: 'utf-8',
                timeout: 5000
            });
            return result.trim();
        } catch (error) {
            console.error('[GitService] git rev-parse failed:', error);
            return null;
        }
    }
}

// Export singleton instance
export const gitService = new GitService();
