// <copyright file="Workflow.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// The root entity representing a unit of work.
/// This is the single entity for the Developer module's workflow automation.
/// </summary>
public sealed class Workflow
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the workflow title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the workflow description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the repository path.</summary>
    public string? RepositoryPath { get; set; }

    /// <summary>Gets or sets the workflow status.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Created;

    /// <summary>Gets or sets the worktree path (isolated git worktree for this workflow).</summary>
    public string? WorktreePath { get; set; }

    /// <summary>Gets or sets the git branch (e.g., "feature/workflow-123")..</summary>
    public string? GitBranch { get; set; }

    /// <summary>Gets or sets the analyzed context as JSON (from analysis agent).</summary>
    public string? AnalyzedContext { get; set; }

    /// <summary>Gets or sets the execution plan as JSON (from planning agent).</summary>
    public string? ExecutionPlan { get; set; }

    /// <summary>Gets or sets when the workflow was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the workflow was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the workflow was completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the URL of the pull request created for this workflow.</summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>Gets or sets the workflow steps.</summary>
    public ICollection<WorkflowStep> Steps { get; set; } = [];
}

/// <summary>
/// The status of a workflow.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>Just created, not yet analyzed.</summary>
    Created,

    /// <summary>Analyzing the requirements.</summary>
    Analyzing,

    /// <summary>Analysis complete, ready for planning.</summary>
    Analyzed,

    /// <summary>Creating execution plan.</summary>
    Planning,

    /// <summary>Plan created, ready for execution.</summary>
    Planned,

    /// <summary>Steps being executed.</summary>
    Executing,

    /// <summary>All steps completed successfully.</summary>
    Completed,

    /// <summary>Unrecoverable error.</summary>
    Failed,

    /// <summary>User cancelled.</summary>
    Cancelled,
}
