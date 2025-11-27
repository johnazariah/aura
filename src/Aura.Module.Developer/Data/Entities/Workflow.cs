// <copyright file="Workflow.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// The root entity representing a unit of work from a GitHub issue, ADO work item, or manual input.
/// This is a Developer module entity for workflow automation.
/// </summary>
public sealed class Workflow
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the work item identifier (e.g., "github:owner/repo#123").</summary>
    public required string WorkItemId { get; set; }

    /// <summary>Gets or sets the work item title.</summary>
    public required string WorkItemTitle { get; set; }

    /// <summary>Gets or sets the work item description.</summary>
    public string? WorkItemDescription { get; set; }

    /// <summary>Gets or sets the work item URL.</summary>
    public string? WorkItemUrl { get; set; }

    /// <summary>Gets or sets the workflow status.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Created;

    /// <summary>Gets or sets the workspace path (e.g., "/workspaces/repo-wt-123").</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>Gets or sets the git branch (e.g., "feature/issue-123").</summary>
    public string? GitBranch { get; set; }

    /// <summary>Gets or sets the digested context as JSON (RAG output).</summary>
    public string? DigestedContext { get; set; }

    /// <summary>Gets or sets when the workflow was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the workflow was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the workflow steps.</summary>
    public ICollection<WorkflowStep> Steps { get; set; } = [];
}

/// <summary>
/// The status of a workflow.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>Just created.</summary>
    Created,

    /// <summary>RAG ingestion in progress.</summary>
    Digesting,

    /// <summary>Context ready.</summary>
    Digested,

    /// <summary>Creating execution plan.</summary>
    Planning,

    /// <summary>Steps defined.</summary>
    Planned,

    /// <summary>Steps being executed.</summary>
    Executing,

    /// <summary>All steps done.</summary>
    Completed,

    /// <summary>Unrecoverable error.</summary>
    Failed,

    /// <summary>User cancelled.</summary>
    Cancelled,
}
