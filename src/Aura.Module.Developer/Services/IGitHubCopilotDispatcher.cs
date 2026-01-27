// <copyright file="IGitHubCopilotDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Dispatches tasks to GitHub Copilot CLI agents running in YOLO mode.
/// </summary>
public interface IGitHubCopilotDispatcher
{
    /// <summary>
    /// Dispatches a task to a GH Copilot CLI agent.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="worktreePath">The worktree path to execute in.</param>
    /// <param name="completedTasks">Previously completed tasks for context (optional).</param>
    /// <param name="githubToken">GitHub token for authentication (optional, passed per-request).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task with execution result.</returns>
    Task<StoryTask> DispatchTaskAsync(
        StoryTask task,
        string worktreePath,
        IReadOnlyList<StoryTask>? completedTasks = null,
        string? githubToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dispatches multiple tasks in parallel.
    /// </summary>
    /// <param name="tasks">The tasks to execute.</param>
    /// <param name="worktreePath">The worktree path to execute in.</param>
    /// <param name="maxParallelism">Maximum concurrent agents.</param>
    /// <param name="completedTasks">Previously completed tasks for context (optional).</param>
    /// <param name="githubToken">GitHub token for authentication (optional, passed per-request).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated tasks with execution results.</returns>
    Task<IReadOnlyList<StoryTask>> DispatchTasksAsync(
        IReadOnlyList<StoryTask> tasks,
        string worktreePath,
        int maxParallelism,
        IReadOnlyList<StoryTask>? completedTasks = null,
        string? githubToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if GH Copilot CLI is available and authenticated.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
