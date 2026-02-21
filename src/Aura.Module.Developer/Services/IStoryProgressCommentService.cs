namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for posting progress comments to GitHub issues during story execution
/// </summary>
public interface IStoryProgressCommentService
{
    /// <summary>
    /// Posts a comment to a GitHub issue indicating that story analysis has completed
    /// </summary>
    Task PostAnalysisCompleteCommentAsync(string owner, string repo, int issueNumber, CancellationToken ct);

    /// <summary>
    /// Posts a comment to a GitHub issue indicating that story planning has completed with a summary of steps
    /// </summary>
    Task PostPlanningCompleteCommentAsync(string owner, string repo, int issueNumber, int stepCount, CancellationToken ct);

    /// <summary>
    /// Posts a comment to a GitHub issue indicating that a wave of steps has completed
    /// </summary>
    Task PostWaveCompleteCommentAsync(string owner, string repo, int issueNumber, int waveNumber, int totalWaves, CancellationToken ct);

    /// <summary>
    /// Posts a comment to a GitHub issue indicating that the story is ready for PR with a link to the pull request
    /// </summary>
    Task PostPRReadyCommentAsync(string owner, string repo, int issueNumber, string prLink, CancellationToken ct);
}
