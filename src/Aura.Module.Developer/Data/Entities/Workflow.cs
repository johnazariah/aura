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

    // === Issue Integration (Story Model) ===

    /// <summary>Gets or sets the external issue URL (e.g., "https://github.com/org/repo/issues/123").</summary>
    public string? IssueUrl { get; set; }

    /// <summary>Gets or sets the issue provider type.</summary>
    public IssueProvider? IssueProvider { get; set; }

    /// <summary>Gets or sets the issue number (extracted from URL for API calls).</summary>
    public int? IssueNumber { get; set; }

    /// <summary>Gets or sets the repository owner (extracted from URL).</summary>
    public string? IssueOwner { get; set; }

    /// <summary>Gets or sets the repository name (extracted from URL).</summary>
    public string? IssueRepo { get; set; }

    // === Mode ===

    /// <summary>Gets or sets the execution mode: structured (steps) or conversational.</summary>
    public WorkflowMode Mode { get; set; } = WorkflowMode.Structured;

    /// <summary>Gets or sets the automation mode for step execution.</summary>
    public AutomationMode AutomationMode { get; set; } = AutomationMode.Assisted;
}

/// <summary>
/// The issue provider for external issue tracking.
/// </summary>
public enum IssueProvider
{
    /// <summary>GitHub Issues.</summary>
    GitHub,

    /// <summary>Azure DevOps Work Items.</summary>
    AzureDevOps,
}

/// <summary>
/// The execution mode for a workflow/story.
/// </summary>
public enum WorkflowMode
{
    /// <summary>Plan → Steps → Execute → Review (current behavior).</summary>
    Structured,

    /// <summary>Free-form conversation in worktree (GHCP Agent mode).</summary>
    Conversational,
}

/// <summary>
/// The automation mode for step execution.
/// Controls how much user approval is required during workflow execution.
/// </summary>
public enum AutomationMode
{
    /// <summary>
    /// User must approve each step before execution (default, safest).
    /// </summary>
    Assisted,

    /// <summary>
    /// Auto-approve steps that don't require confirmation.
    /// Steps with requiresConfirmation=true still require user approval.
    /// </summary>
    Autonomous,

    /// <summary>
    /// Auto-approve ALL steps including dangerous operations (YOLO mode).
    /// Use with caution - no human-in-the-loop safety checks.
    /// </summary>
    FullAutonomous,
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
