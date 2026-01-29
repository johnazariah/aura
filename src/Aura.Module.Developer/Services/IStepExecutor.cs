// <copyright file="IStepExecutor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Unified interface for executing story steps.
/// Implementations handle single-step and parallel execution.
/// </summary>
public interface IStepExecutor
{
    /// <summary>
    /// Gets the unique executor identifier (e.g., "internal", "copilot").
    /// </summary>
    string ExecutorId { get; }

    /// <summary>
    /// Gets a human-readable display name for the executor.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Checks if this executor is available (e.g., Copilot CLI installed, LLM configured).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes a single step. The step is modified in-place with results.
    /// </summary>
    /// <param name="step">The step to execute (modified in-place).</param>
    /// <param name="story">The parent story for context.</param>
    /// <param name="priorSteps">Completed steps from prior waves for context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteStepAsync(
        StoryStep step,
        Story story,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes multiple steps in parallel. Steps are modified in-place with results.
    /// </summary>
    /// <param name="steps">The steps to execute (modified in-place).</param>
    /// <param name="story">The parent story for context.</param>
    /// <param name="maxParallelism">Maximum concurrent executions.</param>
    /// <param name="priorSteps">Completed steps from prior waves for context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteStepsAsync(
        IReadOnlyList<StoryStep> steps,
        Story story,
        int maxParallelism,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default);
}
