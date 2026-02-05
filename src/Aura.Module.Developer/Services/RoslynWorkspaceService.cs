// <copyright file="RoslynWorkspaceService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Collections.Concurrent;
using Aura.Foundation.Git;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages Roslyn workspaces for code analysis.
/// Thread-safe with caching for loaded solutions.
/// </summary>
public sealed class RoslynWorkspaceService : IRoslynWorkspaceService, IDisposable
{
    private readonly ILogger<RoslynWorkspaceService> _logger;
    private readonly ConcurrentDictionary<string, MSBuildWorkspace> _workspaces = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private static bool _msbuildRegistered;
    private static readonly object _registrationLock = new();

    public RoslynWorkspaceService(ILogger<RoslynWorkspaceService> logger)
    {
        _logger = logger;
        EnsureMSBuildRegistered();
    }

    private static void EnsureMSBuildRegistered()
    {
        lock (_registrationLock)
        {
            if (_msbuildRegistered) return;

            // Register the most recent MSBuild instance
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count > 0)
            {
                var instance = instances.OrderByDescending(i => i.Version).First();
                MSBuildLocator.RegisterInstance(instance);
            }
            else
            {
                // Try to register defaults if no VS instances found
                MSBuildLocator.RegisterDefaults();
            }

            _msbuildRegistered = true;
        }
    }

    /// <inheritdoc/>
    public async Task<Solution> GetSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        var normalizedPath = Path.GetFullPath(solutionPath);

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException($"Solution file not found: {normalizedPath}");
        }

        // Auto-detect if this is a .csproj file and use OpenProjectAsync instead
        var isProjectFile = normalizedPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                           normalizedPath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                           normalizedPath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase);

        if (isProjectFile)
        {
            _logger.LogDebug("Detected project file, using OpenProjectAsync: {Path}", normalizedPath);
            var project = await GetProjectAsync(normalizedPath, ct);
            return project.Solution;
        }

        // Detect if we're in a worktree - each worktree gets its own workspace
        // to ensure we see the worktree's file contents, not cached/stale data
        var worktreeInfo = GitWorktreeDetector.Detect(normalizedPath);
        var cacheKey = normalizedPath; // Use full path as cache key (unique per worktree)

        if (worktreeInfo?.IsWorktree == true)
        {
            _logger.LogDebug(
                "Worktree detected: {WorktreePath} -> Main: {MainRepoPath}",
                worktreeInfo.Value.WorktreePath,
                worktreeInfo.Value.MainRepoPath);
        }

        await _loadLock.WaitAsync(ct);
        try
        {
            if (!_workspaces.TryGetValue(cacheKey, out var workspace))
            {
                _logger.LogInformation("Loading solution: {Path}", normalizedPath);
                workspace = MSBuildWorkspace.Create();
                workspace.WorkspaceFailed += (sender, args) =>
                {
                    _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}",
                        args.Diagnostic.Kind, args.Diagnostic.Message);
                };

                var solution = await workspace.OpenSolutionAsync(normalizedPath, cancellationToken: ct);
                _workspaces[cacheKey] = workspace;
                _logger.LogInformation("Loaded solution with {ProjectCount} projects", solution.Projects.Count());
                return solution;
            }

            return workspace.CurrentSolution;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<Project> GetProjectAsync(string projectPath, CancellationToken ct = default)
    {
        var normalizedPath = Path.GetFullPath(projectPath);

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException($"Project file not found: {normalizedPath}");
        }

        await _loadLock.WaitAsync(ct);
        try
        {
            if (!_workspaces.TryGetValue(normalizedPath, out var workspace))
            {
                _logger.LogInformation("Loading project: {Path}", normalizedPath);
                workspace = MSBuildWorkspace.Create();
                workspace.WorkspaceFailed += (sender, args) =>
                {
                    _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}",
                        args.Diagnostic.Kind, args.Diagnostic.Message);
                };

                var project = await workspace.OpenProjectAsync(normalizedPath, cancellationToken: ct);
                _workspaces[normalizedPath] = workspace;
                _logger.LogInformation("Loaded project: {Name}", project.Name);
                return project;
            }

            return workspace.CurrentSolution.Projects.First();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <inheritdoc/>
    public string? FindSolutionFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var solutions = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);
        return solutions.Length switch
        {
            0 => null,
            1 => solutions[0],
            _ => solutions.OrderBy(s => Path.GetFileName(s).Length).First() // Prefer shortest name
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> FindProjectFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains("bin") && !p.Contains("obj"))
            .OrderBy(p => p)
            .ToList();
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        foreach (var workspace in _workspaces.Values)
        {
            workspace.Dispose();
        }
        _workspaces.Clear();
        _logger.LogInformation("Cleared workspace cache");
    }

    /// <inheritdoc/>
    public bool InvalidateCache(string solutionPath)
    {
        var normalizedPath = Path.GetFullPath(solutionPath);
        if (_workspaces.TryRemove(normalizedPath, out var workspace))
        {
            workspace.Dispose();
            _logger.LogInformation("Invalidated cached workspace: {Path}", normalizedPath);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        ClearCache();
        _loadLock.Dispose();
    }
}
