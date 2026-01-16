// <copyright file="GitWorktreeDetector.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Git;

/// <summary>
/// Information about a git worktree detected from the filesystem.
/// Unlike <see cref="WorktreeInfo"/>, this is obtained synchronously without git CLI.
/// </summary>
/// <param name="WorktreePath">The worktree location (e.g., c:\work\aura-workflow-xyz).</param>
/// <param name="MainRepoPath">The main repository (e.g., c:\work\aura).</param>
/// <param name="GitDir">The worktree-specific git directory.</param>
/// <param name="IsWorktree">True if this is a worktree, false if it's the main repo.</param>
public readonly record struct DetectedWorktree(
    string WorktreePath,
    string MainRepoPath,
    string GitDir,
    bool IsWorktree);

/// <summary>
/// Synchronous, static utility to detect git worktrees from filesystem.
/// Does not require git CLI - parses .git file directly.
/// Use for cache key resolution and path translation.
/// </summary>
public static class GitWorktreeDetector
{
    /// <summary>
    /// Detects if a path is within a git worktree by checking for .git file vs directory.
    /// </summary>
    /// <param name="path">Any path within a potential git repository or worktree.</param>
    /// <returns>Worktree info if detected, null if not a git repo or detection fails.</returns>
    public static DetectedWorktree? Detect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Walk up to find .git
        var current = Path.GetFullPath(path);

        while (!string.IsNullOrEmpty(current))
        {
            var gitPath = Path.Combine(current, ".git");

            if (File.Exists(gitPath))
            {
                // .git is a FILE -> this is a worktree
                return ParseWorktreeGitFile(gitPath, current);
            }

            if (Directory.Exists(gitPath))
            {
                // .git is a DIRECTORY -> this is the main repo
                return new DetectedWorktree(
                    WorktreePath: current,
                    MainRepoPath: current,
                    GitDir: gitPath,
                    IsWorktree: false);
            }

            var parent = Path.GetDirectoryName(current);
            if (parent == current)
            {
                break; // Reached root
            }

            current = parent;
        }

        return null;
    }

    /// <summary>
    /// Translates a file path from the main repository to the equivalent path in a worktree.
    /// </summary>
    /// <param name="mainRepoFilePath">A file path within the main repository.</param>
    /// <param name="worktree">The worktree to translate to.</param>
    /// <returns>The equivalent path within the worktree.</returns>
    public static string TranslatePath(string mainRepoFilePath, DetectedWorktree worktree)
    {
        if (!worktree.IsWorktree)
        {
            return mainRepoFilePath; // No translation needed for main repo
        }

        // Get relative path from main repo
        var normalizedFilePath = Path.GetFullPath(mainRepoFilePath);
        var normalizedMainRepo = Path.GetFullPath(worktree.MainRepoPath);

        if (!normalizedFilePath.StartsWith(normalizedMainRepo, StringComparison.OrdinalIgnoreCase))
        {
            // Path is not within main repo - return as-is
            return mainRepoFilePath;
        }

        // Get relative portion and combine with worktree path
        var relativePath = Path.GetRelativePath(normalizedMainRepo, normalizedFilePath);
        return Path.Combine(worktree.WorktreePath, relativePath);
    }

    /// <summary>
    /// Translates a file path from a worktree to the equivalent path in the main repository.
    /// </summary>
    /// <param name="worktreeFilePath">A file path within the worktree.</param>
    /// <param name="worktree">The worktree info.</param>
    /// <returns>The equivalent path within the main repository.</returns>
    public static string TranslateToMainRepo(string worktreeFilePath, DetectedWorktree worktree)
    {
        if (!worktree.IsWorktree)
        {
            return worktreeFilePath; // Already in main repo
        }

        var normalizedFilePath = Path.GetFullPath(worktreeFilePath);
        var normalizedWorktree = Path.GetFullPath(worktree.WorktreePath);

        if (!normalizedFilePath.StartsWith(normalizedWorktree, StringComparison.OrdinalIgnoreCase))
        {
            return worktreeFilePath;
        }

        var relativePath = Path.GetRelativePath(normalizedWorktree, normalizedFilePath);
        return Path.Combine(worktree.MainRepoPath, relativePath);
    }

    /// <summary>
    /// Parses a .git file (not directory) to extract worktree information.
    /// Format: "gitdir: /path/to/.git/worktrees/worktree-name"
    /// </summary>
    private static DetectedWorktree? ParseWorktreeGitFile(string gitFilePath, string worktreePath)
    {
        try
        {
            var content = File.ReadAllText(gitFilePath).Trim();

            // Format: "gitdir: /path/to/main/.git/worktrees/worktree-name"
            const string prefix = "gitdir:";
            if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var gitDir = content[prefix.Length..].Trim();

            // Make path absolute relative to the .git file location
            if (!Path.IsPathRooted(gitDir))
            {
                gitDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(gitFilePath)!, gitDir));
            }

            // Extract main repo path from gitDir
            // gitDir looks like: /path/to/main/.git/worktrees/worktree-name
            // We need: /path/to/main
            var mainRepoGitDir = ExtractMainGitDir(gitDir);
            if (mainRepoGitDir is null)
            {
                return null;
            }

            var mainRepoPath = Path.GetDirectoryName(mainRepoGitDir);
            if (string.IsNullOrEmpty(mainRepoPath))
            {
                return null;
            }

            return new DetectedWorktree(
                WorktreePath: Path.GetFullPath(worktreePath),
                MainRepoPath: Path.GetFullPath(mainRepoPath),
                GitDir: gitDir,
                IsWorktree: true);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the main .git directory from a worktree's gitdir path.
    /// Input:  /path/to/main/.git/worktrees/worktree-name
    /// Output: /path/to/main/.git
    /// </summary>
    private static string? ExtractMainGitDir(string worktreeGitDir)
    {
        // Normalize separators
        var normalized = worktreeGitDir.Replace('\\', '/');

        // Look for "/.git/worktrees/"
        const string marker = "/.git/worktrees/";
        var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
        {
            // Try Windows-style path
            const string winMarker = "\\.git\\worktrees\\";
            idx = worktreeGitDir.IndexOf(winMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return worktreeGitDir[..(idx + 5)]; // Include ".git"
            }

            return null;
        }

        // Return path up to and including ".git"
        var mainGitDirEnd = idx + "/.git".Length;
        var result = normalized[..mainGitDirEnd];

        // Convert back to platform-native separators
        return result.Replace('/', Path.DirectorySeparatorChar);
    }
}
