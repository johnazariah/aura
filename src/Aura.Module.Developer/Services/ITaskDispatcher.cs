// <copyright file="ITaskDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Unified interface for dispatching tasks to execution agents.
/// Implemented by both <see cref="GitHubCopilotDispatcher"/> and <see cref="InternalAgentsDispatcher"/>.
/// </summary>
public interface ITaskDispatcher
{
    /// <summary>
    /// Gets the dispatch target this dispatcher handles.
    /// </summary>
    DispatchTarget Target { get; }

    /// <summary>
    /// Dispatches a single task to an agent.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="worktreePath">The worktree path to execute in.</param>
    /// <param name="completedTasks">Previously completed tasks for context (optional).</param>
    /// <param name="githubToken">GitHub token for authentication (optional, for CopilotCli).</param>
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
    /// <param name="githubToken">GitHub token for authentication (optional, for CopilotCli).</param>
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
    /// Checks if this dispatcher is available.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
