// <copyright file="GitHubActions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a GitHub Actions workflow.
/// </summary>
public sealed record GitHubWorkflow
{
    /// <summary>Gets the workflow ID.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Gets the workflow name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the workflow path in the repository.</summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>Gets the workflow state (active, deleted, etc.).</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>Gets when the workflow was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the workflow was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the workflow URL.</summary>
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }
}

/// <summary>
/// Response for listing workflows.
/// </summary>
public sealed record GitHubWorkflowsResponse
{
    /// <summary>Gets the total count of workflows.</summary>
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    /// <summary>Gets the workflows.</summary>
    [JsonPropertyName("workflows")]
    public IReadOnlyList<GitHubWorkflow> Workflows { get; init; } = [];
}

/// <summary>
/// Represents a GitHub Actions workflow run.
/// </summary>
public sealed record GitHubWorkflowRun
{
    /// <summary>Gets the run ID.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Gets the run name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Gets the run number.</summary>
    [JsonPropertyName("run_number")]
    public int RunNumber { get; init; }

    /// <summary>Gets the run status (queued, in_progress, completed).</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Gets the run conclusion (success, failure, cancelled, etc.).</summary>
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; init; }

    /// <summary>Gets the workflow ID.</summary>
    [JsonPropertyName("workflow_id")]
    public long WorkflowId { get; init; }

    /// <summary>Gets the branch or tag that triggered the run.</summary>
    [JsonPropertyName("head_branch")]
    public string? HeadBranch { get; init; }

    /// <summary>Gets the commit SHA.</summary>
    [JsonPropertyName("head_sha")]
    public required string HeadSha { get; init; }

    /// <summary>Gets the event that triggered the run.</summary>
    [JsonPropertyName("event")]
    public required string Event { get; init; }

    /// <summary>Gets when the run was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the run was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the run URL.</summary>
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    /// <summary>Gets the jobs URL.</summary>
    [JsonPropertyName("jobs_url")]
    public string? JobsUrl { get; init; }

    /// <summary>Gets the logs URL.</summary>
    [JsonPropertyName("logs_url")]
    public string? LogsUrl { get; init; }
}

/// <summary>
/// Response for listing workflow runs.
/// </summary>
public sealed record GitHubWorkflowRunsResponse
{
    /// <summary>Gets the total count of runs.</summary>
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    /// <summary>Gets the workflow runs.</summary>
    [JsonPropertyName("workflow_runs")]
    public IReadOnlyList<GitHubWorkflowRun> WorkflowRuns { get; init; } = [];
}

/// <summary>
/// Represents a job within a workflow run.
/// </summary>
public sealed record GitHubJob
{
    /// <summary>Gets the job ID.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Gets the run ID.</summary>
    [JsonPropertyName("run_id")]
    public long RunId { get; init; }

    /// <summary>Gets the job name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the job status.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Gets the job conclusion.</summary>
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; init; }

    /// <summary>Gets when the job started.</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Gets when the job completed.</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Gets the job steps.</summary>
    [JsonPropertyName("steps")]
    public IReadOnlyList<GitHubJobStep> Steps { get; init; } = [];
}

/// <summary>
/// Represents a step within a job.
/// </summary>
public sealed record GitHubJobStep
{
    /// <summary>Gets the step name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the step status.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Gets the step conclusion.</summary>
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; init; }

    /// <summary>Gets the step number.</summary>
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>Gets when the step started.</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Gets when the step completed.</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Response for listing jobs.
/// </summary>
public sealed record GitHubJobsResponse
{
    /// <summary>Gets the total count of jobs.</summary>
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    /// <summary>Gets the jobs.</summary>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<GitHubJob> Jobs { get; init; } = [];
}
