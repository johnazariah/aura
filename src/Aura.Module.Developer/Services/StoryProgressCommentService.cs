// <copyright file="StoryProgressCommentService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.GitHub;
using Microsoft.Extensions.Logging;

/// <summary>
/// Posts progress comments to linked GitHub issues during story execution.
/// </summary>
public sealed class StoryProgressCommentService : IStoryProgressCommentService
{
    private readonly IGitHubService _gitHub;
    private readonly ILogger<StoryProgressCommentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoryProgressCommentService"/> class.
    /// </summary>
    public StoryProgressCommentService(IGitHubService gitHub, ILogger<StoryProgressCommentService> logger)
    {
        _gitHub = gitHub;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PostAnalysisCompleteCommentAsync(string owner, string repo, int issueNumber, CancellationToken ct)
    {
        if (!EnsureConfigured())
        {
            return;
        }

        var body = "🔍 **Analysis complete.** Aura has finished analysing the issue and is now planning implementation steps.";
        await _gitHub.PostCommentAsync(owner, repo, issueNumber, body, ct);
    }

    /// <inheritdoc/>
    public async Task PostPlanningCompleteCommentAsync(string owner, string repo, int issueNumber, int stepCount, CancellationToken ct)
    {
        if (!EnsureConfigured())
        {
            return;
        }

        var body = $"📋 **Planning complete.** Aura has created a plan with **{stepCount} step{(stepCount == 1 ? string.Empty : "s")}** and is now executing it.";
        await _gitHub.PostCommentAsync(owner, repo, issueNumber, body, ct);
    }

    /// <inheritdoc/>
    public async Task PostWaveCompleteCommentAsync(string owner, string repo, int issueNumber, int waveNumber, int totalWaves, CancellationToken ct)
    {
        if (!EnsureConfigured())
        {
            return;
        }

        var body = $"⚡ **Wave {waveNumber}/{totalWaves} complete.** Aura has finished executing wave {waveNumber} of {totalWaves}.";
        await _gitHub.PostCommentAsync(owner, repo, issueNumber, body, ct);
    }

    /// <inheritdoc/>
    public async Task PostPRReadyCommentAsync(string owner, string repo, int issueNumber, string prLink, CancellationToken ct)
    {
        if (!EnsureConfigured())
        {
            return;
        }

        var body = $"✅ **Pull request ready for review.** Aura has finished implementing this story.\n\n👉 [View Pull Request]({prLink})";
        await _gitHub.PostCommentAsync(owner, repo, issueNumber, body, ct);
    }

    private bool EnsureConfigured()
    {
        if (_gitHub.IsConfigured)
        {
            return true;
        }

        _logger.LogWarning("GitHub integration is not configured; skipping progress comment.");
        return false;
    }
}
