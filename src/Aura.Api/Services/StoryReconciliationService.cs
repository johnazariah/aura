// <copyright file="StoryReconciliationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Services;

using Aura.Foundation.Git;
using Aura.Foundation.Shell;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reconciles story step statuses based on git commit messages, recovering steps that were
/// left in an inconsistent state due to a service crash during story execution.
/// </summary>
public sealed class StoryReconciliationService : IStoryReconciliationService
{
    private readonly IGitService _gitService;
    private readonly IStoryService _storyService;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<StoryReconciliationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoryReconciliationService"/> class.
    /// </summary>
    /// <param name="gitService">Git service for repository operations.</param>
    /// <param name="storyService">Story service for reading and updating story data.</param>
    /// <param name="processRunner">Process runner for executing git log commands.</param>
    /// <param name="logger">Logger instance.</param>
    public StoryReconciliationService(
        IGitService gitService,
        IStoryService storyService,
        IProcessRunner processRunner,
        ILogger<StoryReconciliationService> logger)
    {
        _gitService = gitService;
        _storyService = storyService;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ReconcileStepStatusesAsync(Guid storyId, CancellationToken ct)
    {
        _logger.LogInformation("Reconciling step statuses for story {StoryId}", storyId);

        var story = await _storyService.GetByIdWithStepsAsync(storyId, ct);
        if (story is null)
        {
            _logger.LogWarning("Story {StoryId} not found during reconciliation", storyId);
            return;
        }

        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            _logger.LogDebug("Story {StoryId} has no worktree path, skipping reconciliation", storyId);
            return;
        }

        var isRepo = await _gitService.IsRepositoryAsync(story.WorktreePath, ct);
        if (!isRepo)
        {
            _logger.LogWarning(
                "Worktree path '{WorktreePath}' for story {StoryId} is not a git repository",
                story.WorktreePath,
                storyId);
            return;
        }

        var commitMessages = await GetCommitMessagesAsync(story.WorktreePath, ct);
        if (commitMessages is null)
        {
            _logger.LogWarning("Failed to retrieve git log for story {StoryId}", storyId);
            return;
        }

        var reconciledCount = 0;
        foreach (var step in story.Steps)
        {
            if (step.Status is StepStatus.Completed or StepStatus.Skipped)
            {
                continue;
            }

            if (IsStepCommitted(step, commitMessages))
            {
                _logger.LogInformation(
                    "Marking step {StepId} '{StepName}' as Completed based on git commit",
                    step.Id,
                    step.Name);

                step.Status = StepStatus.Completed;
                step.CompletedAt ??= DateTimeOffset.UtcNow;

                await _storyService.UpdateStepAsync(step, ct);
                reconciledCount++;
            }
        }

        _logger.LogInformation(
            "Reconciliation complete for story {StoryId}: {Count} step(s) recovered",
            storyId,
            reconciledCount);
    }

    /// <summary>
    /// Retrieves all commit subject lines from the git log of the given repository path.
    /// </summary>
    /// <param name="repoPath">Path to the git repository or worktree.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of commit subject lines, or <c>null</c> if the command failed.</returns>
    private async Task<IReadOnlyList<string>?> GetCommitMessagesAsync(string repoPath, CancellationToken ct)
    {
        var result = await _processRunner.RunAsync(
            "git",
            ["log", "--format=%s"],
            new ProcessOptions
            {
                WorkingDirectory = repoPath,
                Timeout = TimeSpan.FromSeconds(30),
            },
            ct);

        if (!result.Success)
        {
            _logger.LogWarning("git log failed for '{RepoPath}': {Error}", repoPath, result.StandardError);
            return null;
        }

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Determines whether a matching commit message exists for the given step.
    /// The expected commit message pattern is <c>Step {Order}: {Name}</c>.
    /// </summary>
    /// <param name="step">The story step to check.</param>
    /// <param name="commitMessages">The list of commit subject lines to search.</param>
    /// <returns><c>true</c> if a matching commit is found; otherwise, <c>false</c>.</returns>
    private static bool IsStepCommitted(StoryStep step, IReadOnlyList<string> commitMessages)
    {
        var expectedPrefix = $"Step {step.Order}: {step.Name}";

        return commitMessages.Any(msg =>
            msg.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
