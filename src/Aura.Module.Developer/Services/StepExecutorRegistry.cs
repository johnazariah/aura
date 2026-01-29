// <copyright file="StepExecutorRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Registry for step executors. Provides executor selection based on step/story preferences.
/// </summary>
public interface IStepExecutorRegistry
{
    /// <summary>
    /// Gets all registered executors.
    /// </summary>
    IReadOnlyList<IStepExecutor> GetAll();

    /// <summary>
    /// Gets an executor by ID.
    /// </summary>
    /// <param name="executorId">The executor ID (e.g., "internal", "copilot").</param>
    /// <returns>The executor if found, null otherwise.</returns>
    IStepExecutor? GetExecutor(string executorId);

    /// <summary>
    /// Resolves the executor for a step, considering step override, story preference, and defaults.
    /// </summary>
    /// <param name="step">The step to execute.</param>
    /// <param name="story">The parent story.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved executor, or null if none available.</returns>
    Task<IStepExecutor?> ResolveExecutorAsync(StoryStep step, Story story, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="IStepExecutorRegistry"/>.
/// </summary>
public sealed class StepExecutorRegistry : IStepExecutorRegistry
{
    private readonly Dictionary<string, IStepExecutor> _executors;
    private readonly DeveloperModuleOptions _options;
    private readonly ILogger<StepExecutorRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepExecutorRegistry"/> class.
    /// </summary>
    public StepExecutorRegistry(
        IEnumerable<IStepExecutor> executors,
        IOptions<DeveloperModuleOptions> options,
        ILogger<StepExecutorRegistry> logger)
    {
        _executors = executors.ToDictionary(e => e.ExecutorId, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "Step executor registry initialized with {Count} executors: {Executors}",
            _executors.Count,
            string.Join(", ", _executors.Keys));
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStepExecutor> GetAll() => _executors.Values.ToList();

    /// <inheritdoc/>
    public IStepExecutor? GetExecutor(string executorId)
    {
        return _executors.TryGetValue(executorId, out var executor) ? executor : null;
    }

    /// <inheritdoc/>
    public async Task<IStepExecutor?> ResolveExecutorAsync(StoryStep step, Story story, CancellationToken ct = default)
    {
        // Resolution order: step override → story preference → system default
        var preferredId = step.ExecutorOverride
            ?? story.PreferredExecutor
            ?? _options.DefaultExecutor
            ?? "copilot"; // Ultimate fallback

        if (_executors.TryGetValue(preferredId, out var preferred))
        {
            if (await preferred.IsAvailableAsync(ct))
            {
                _logger.LogDebug(
                    "Using {Executor} executor for step {StepId} (source: {Source})",
                    preferred.ExecutorId,
                    step.Id,
                    step.ExecutorOverride != null ? "step override" :
                    story.PreferredExecutor != null ? "story preference" :
                    _options.DefaultExecutor != null ? "system default" : "fallback");
                return preferred;
            }

            _logger.LogWarning(
                "Preferred executor '{ExecutorId}' is not available, trying fallback",
                preferredId);
        }

        // Try to find any available executor
        foreach (var executor in _executors.Values)
        {
            if (await executor.IsAvailableAsync(ct))
            {
                _logger.LogInformation(
                    "Using fallback executor '{ExecutorId}' for step {StepId}",
                    executor.ExecutorId,
                    step.Id);
                return executor;
            }
        }

        _logger.LogError("No available executor found for step {StepId}", step.Id);
        return null;
    }
}
