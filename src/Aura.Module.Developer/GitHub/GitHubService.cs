// <copyright file="GitHubService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Service for interacting with GitHub Issues API.
/// </summary>
public sealed partial class GitHubService : IGitHubService
{
    private readonly HttpClient _http;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubService"/> class.
    /// </summary>
    public GitHubService(
        HttpClient http,
        IOptions<GitHubOptions> options,
        ILogger<GitHubService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConfigured => !string.IsNullOrEmpty(_options.Token);

    /// <inheritdoc/>
    public async Task<GitHubIssue> GetIssueAsync(string owner, string repo, int number, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/issues/{number}";
        _logger.LogDebug("Fetching issue from {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var issue = await response.Content.ReadFromJsonAsync<GitHubIssue>(ct)
            ?? throw new InvalidOperationException("Failed to parse GitHub issue response");

        _logger.LogInformation("Fetched issue #{Number} from {Owner}/{Repo}: {Title}",
            number, owner, repo, issue.Title);

        return issue;
    }

    /// <inheritdoc/>
    public async Task PostCommentAsync(string owner, string repo, int number, string body, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/issues/{number}/comments";
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var formattedBody = $"🤖 **Aura Update** ({timestamp})\n\n{body}";

        _logger.LogDebug("Posting comment to {Url}", url);

        var content = JsonContent.Create(new { body = formattedBody });
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Posted comment to {Owner}/{Repo}#{Number}", owner, repo, number);
    }

    /// <inheritdoc/>
    public async Task CloseIssueAsync(string owner, string repo, int number, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/issues/{number}";
        _logger.LogDebug("Closing issue at {Url}", url);

        var content = JsonContent.Create(new { state = "closed" });
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Closed issue {Owner}/{Repo}#{Number}", owner, repo, number);
    }

    /// <inheritdoc/>
    public (string Owner, string Repo, int Number)? ParseIssueUrl(string url)
    {
        var match = GitHubIssueUrlRegex().Match(url);
        if (!match.Success)
        {
            return null;
        }

        return (match.Groups[1].Value, match.Groups[2].Value, int.Parse(match.Groups[3].Value));
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "GitHub integration is not configured. Set the GitHub:Token in appsettings.json or environment variables.");
        }
    }

    [GeneratedRegex(@"github\.com/([^/]+)/([^/]+)/issues/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubIssueUrlRegex();

    // ========================================
    // GitHub Actions API
    // ========================================

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GitHubWorkflow>> ListWorkflowsAsync(string owner, string repo, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/workflows";
        _logger.LogDebug("Listing workflows at {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GitHubWorkflowsResponse>(ct)
            ?? throw new InvalidOperationException("Failed to parse workflows response");

        _logger.LogInformation("Found {Count} workflows in {Owner}/{Repo}", result.TotalCount, owner, repo);
        return result.Workflows;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GitHubWorkflowRun>> ListWorkflowRunsAsync(
        string owner,
        string repo,
        string? workflowIdOrName = null,
        string? branch = null,
        string? status = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = string.IsNullOrEmpty(workflowIdOrName)
            ? $"/repos/{owner}/{repo}/actions/runs"
            : $"/repos/{owner}/{repo}/actions/workflows/{Uri.EscapeDataString(workflowIdOrName)}/runs";

        var queryParams = new List<string> { $"per_page={limit}" };
        if (!string.IsNullOrEmpty(branch))
        {
            queryParams.Add($"branch={Uri.EscapeDataString(branch)}");
        }

        if (!string.IsNullOrEmpty(status))
        {
            queryParams.Add($"status={Uri.EscapeDataString(status)}");
        }

        url = $"{url}?{string.Join("&", queryParams)}";
        _logger.LogDebug("Listing workflow runs at {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GitHubWorkflowRunsResponse>(ct)
            ?? throw new InvalidOperationException("Failed to parse workflow runs response");

        _logger.LogInformation("Found {Count} workflow runs in {Owner}/{Repo}", result.WorkflowRuns.Count, owner, repo);
        return result.WorkflowRuns;
    }

    /// <inheritdoc/>
    public async Task<GitHubWorkflowRun> GetWorkflowRunAsync(string owner, string repo, long runId, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/runs/{runId}";
        _logger.LogDebug("Fetching workflow run at {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var run = await response.Content.ReadFromJsonAsync<GitHubWorkflowRun>(ct)
            ?? throw new InvalidOperationException("Failed to parse workflow run response");

        _logger.LogInformation("Fetched workflow run {RunId} ({Status}) from {Owner}/{Repo}", runId, run.Status, owner, repo);
        return run;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GitHubJob>> ListJobsAsync(string owner, string repo, long runId, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/runs/{runId}/jobs";
        _logger.LogDebug("Listing jobs at {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GitHubJobsResponse>(ct)
            ?? throw new InvalidOperationException("Failed to parse jobs response");

        _logger.LogInformation("Found {Count} jobs for run {RunId} in {Owner}/{Repo}", result.Jobs.Count, runId, owner, repo);
        return result.Jobs;
    }

    /// <inheritdoc/>
    public async Task<string> GetWorkflowRunLogsAsync(string owner, string repo, long runId, CancellationToken ct = default)
    {
        EnsureConfigured();

        // First, get the jobs to summarize logs per job
        var jobs = await ListJobsAsync(owner, repo, runId, ct);

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine($"# Workflow Run {runId} Logs");
        logBuilder.AppendLine();

        foreach (var job in jobs)
        {
            logBuilder.AppendLine($"## Job: {job.Name} ({job.Status}/{job.Conclusion ?? "pending"})");
            if (job.StartedAt.HasValue)
            {
                logBuilder.AppendLine($"Started: {job.StartedAt.Value:u}");
            }

            if (job.CompletedAt.HasValue)
            {
                logBuilder.AppendLine($"Completed: {job.CompletedAt.Value:u}");
            }

            logBuilder.AppendLine();

            foreach (var step in job.Steps)
            {
                var icon = step.Conclusion switch
                {
                    "success" => "✅",
                    "failure" => "❌",
                    "skipped" => "⏭️",
                    "cancelled" => "🚫",
                    _ => "⏳"
                };
                logBuilder.AppendLine($"  {icon} Step {step.Number}: {step.Name} ({step.Conclusion ?? step.Status})");
            }

            logBuilder.AppendLine();
        }

        // Note: Getting actual log content requires downloading a zip file and extracting it.
        // For now, we return the structured summary which is often more useful for AI agents.
        logBuilder.AppendLine("---");
        logBuilder.AppendLine("Note: This is a structured summary. For full raw logs, visit the GitHub Actions UI.");

        _logger.LogInformation("Generated log summary for run {RunId} in {Owner}/{Repo}", runId, owner, repo);
        return logBuilder.ToString();
    }

    /// <inheritdoc/>
    public async Task TriggerWorkflowAsync(
        string owner,
        string repo,
        string workflowIdOrName,
        string @ref,
        IReadOnlyDictionary<string, string>? inputs = null,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/workflows/{Uri.EscapeDataString(workflowIdOrName)}/dispatches";
        _logger.LogDebug("Triggering workflow at {Url}", url);

        var payload = new Dictionary<string, object> { ["ref"] = @ref };
        if (inputs is { Count: > 0 })
        {
            payload["inputs"] = inputs;
        }

        var content = JsonContent.Create(payload);
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Triggered workflow {Workflow} on {Ref} in {Owner}/{Repo}", workflowIdOrName, @ref, owner, repo);
    }

    /// <inheritdoc/>
    public async Task RerunWorkflowAsync(string owner, string repo, long runId, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/runs/{runId}/rerun";
        _logger.LogDebug("Re-running workflow at {Url}", url);

        var response = await _http.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Re-run triggered for workflow run {RunId} in {Owner}/{Repo}", runId, owner, repo);
    }

    /// <inheritdoc/>
    public async Task CancelWorkflowRunAsync(string owner, string repo, long runId, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"/repos/{owner}/{repo}/actions/runs/{runId}/cancel";
        _logger.LogDebug("Cancelling workflow run at {Url}", url);

        var response = await _http.PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Cancelled workflow run {RunId} in {Owner}/{Repo}", runId, owner, repo);
    }
}
