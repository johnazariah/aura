// <copyright file="IGitHubService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub;

/// <summary>
/// Service for interacting with GitHub Issues API.
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Gets whether the service is configured with a valid token.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Fetches an issue from GitHub.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The issue details.</returns>
    Task<GitHubIssue> GetIssueAsync(string owner, string repo, int number, CancellationToken ct = default);

    /// <summary>
    /// Posts a comment to an issue.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="body">The comment body (markdown).</param>
    /// <param name="ct">Cancellation token.</param>
    Task PostCommentAsync(string owner, string repo, int number, string body, CancellationToken ct = default);

    /// <summary>
    /// Closes an issue.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CloseIssueAsync(string owner, string repo, int number, CancellationToken ct = default);

    /// <summary>
    /// Parses a GitHub issue URL into its components.
    /// </summary>
    /// <param name="url">The issue URL (e.g., "https://github.com/owner/repo/issues/123").</param>
    /// <returns>The parsed components, or null if the URL is invalid.</returns>
    (string Owner, string Repo, int Number)? ParseIssueUrl(string url);

    // ========================================
    // GitHub Actions API
    // ========================================

    /// <summary>
    /// Lists all workflows in a repository.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of workflows.</returns>
    Task<IReadOnlyList<GitHubWorkflow>> ListWorkflowsAsync(string owner, string repo, CancellationToken ct = default);

    /// <summary>
    /// Lists workflow runs for a repository or specific workflow.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="workflowIdOrName">Optional workflow ID or file name (e.g., "ci.yml").</param>
    /// <param name="branch">Optional branch to filter by.</param>
    /// <param name="status">Optional status to filter by (queued, in_progress, completed).</param>
    /// <param name="limit">Maximum number of runs to return (default 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of workflow runs.</returns>
    Task<IReadOnlyList<GitHubWorkflowRun>> ListWorkflowRunsAsync(
        string owner,
        string repo,
        string? workflowIdOrName = null,
        string? branch = null,
        string? status = null,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow run details.</returns>
    Task<GitHubWorkflowRun> GetWorkflowRunAsync(string owner, string repo, long runId, CancellationToken ct = default);

    /// <summary>
    /// Lists jobs for a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of jobs.</returns>
    Task<IReadOnlyList<GitHubJob>> ListJobsAsync(string owner, string repo, long runId, CancellationToken ct = default);

    /// <summary>
    /// Gets logs for a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The log content as a string (may be truncated for large logs).</returns>
    Task<string> GetWorkflowRunLogsAsync(string owner, string repo, long runId, CancellationToken ct = default);

    /// <summary>
    /// Triggers a workflow dispatch event.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="workflowIdOrName">The workflow ID or file name (e.g., "ci.yml").</param>
    /// <param name="ref">The git reference (branch or tag) to run on.</param>
    /// <param name="inputs">Optional inputs for the workflow.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TriggerWorkflowAsync(
        string owner,
        string repo,
        string workflowIdOrName,
        string @ref,
        IReadOnlyDictionary<string, string>? inputs = null,
        CancellationToken ct = default);

    /// <summary>
    /// Re-runs a failed workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The run ID to re-run.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RerunWorkflowAsync(string owner, string repo, long runId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a workflow run.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="runId">The run ID to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CancelWorkflowRunAsync(string owner, string repo, long runId, CancellationToken ct = default);
}
