// <copyright file="IGitHubCopilotDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Dispatches steps to GitHub Copilot CLI agents running in YOLO mode.
/// This is the unified step dispatcher for wave-based parallel execution.
/// </summary>
public interface IGitHubCopilotDispatcher
{
    /// <summary>
    /// Dispatches a step to a GH Copilot CLI agent.
    /// The step is updated in-place with execution results.
    /// </summary>
    /// <param name="step">The step to execute (modified in place).</param>
    /// <param name="worktreePath">The worktree path to execute in.</param>
    /// <param name="completedSteps">Previously completed steps for context (optional).</param>
    /// <param name="githubToken">GitHub token for authentication (optional, passed per-request).</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchStepAsync(
        StoryStep step,
        string worktreePath,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dispatches multiple steps in parallel.
    /// Steps are updated in-place with execution results.
    /// </summary>
    /// <param name="steps">The steps to execute (modified in place).</param>
    /// <param name="worktreePath">The worktree path to execute in.</param>
    /// <param name="maxParallelism">Maximum concurrent agents.</param>
    /// <param name="completedSteps">Previously completed steps for context (optional).</param>
    /// <param name="githubToken">GitHub token for authentication (optional, passed per-request).</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchStepsAsync(
        IReadOnlyList<StoryStep> steps,
        string worktreePath,
        int maxParallelism,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if GH Copilot CLI is available and authenticated.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
