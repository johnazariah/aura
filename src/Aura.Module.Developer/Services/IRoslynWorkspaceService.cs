// <copyright file="IRoslynWorkspaceService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Microsoft.CodeAnalysis;

/// <summary>
/// Service for managing Roslyn workspaces for code analysis.
/// Caches loaded solutions to avoid repeated parsing.
/// </summary>
public interface IRoslynWorkspaceService
{
    /// <summary>
    /// Gets or loads a solution from the specified path.
    /// </summary>
    /// <param name="solutionPath">Path to .sln file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The loaded solution</returns>
    Task<Solution> GetSolutionAsync(string solutionPath, CancellationToken ct = default);

    /// <summary>
    /// Gets or loads a project from the specified path.
    /// </summary>
    /// <param name="projectPath">Path to .csproj file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The loaded project</returns>
    Task<Project> GetProjectAsync(string projectPath, CancellationToken ct = default);

    /// <summary>
    /// Finds the solution file in a directory.
    /// </summary>
    /// <param name="directory">Directory to search</param>
    /// <returns>Path to solution file, or null if not found</returns>
    string? FindSolutionFile(string directory);

    /// <summary>
    /// Finds all project files in a directory.
    /// </summary>
    /// <param name="directory">Directory to search</param>
    /// <returns>List of project file paths</returns>
    IReadOnlyList<string> FindProjectFiles(string directory);

    /// <summary>
    /// Clears the workspace cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Invalidates the cached workspace for a specific solution path.
    /// Use when files have changed and the workspace needs to be reloaded.
    /// </summary>
    /// <param name="solutionPath">Path to the .sln file to invalidate</param>
    /// <returns>True if a cached workspace was found and invalidated</returns>
    bool InvalidateCache(string solutionPath);
}
