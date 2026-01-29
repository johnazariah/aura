// <copyright file="WorkflowStep.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// A single step in the workflow execution plan.
/// This is a Developer module entity for workflow automation.
/// </summary>
public sealed class StoryStep
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the parent story ID.</summary>
    public Guid StoryId { get; set; }

    /// <summary>Gets or sets the parent story.</summary>
    public Story Story { get; set; } = null!;

    /// <summary>Gets or sets the execution order (1-based).</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the step name (e.g., "Implement UserService").</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the required capability (e.g., "coding", "testing", "review").</summary>
    public required string Capability { get; set; }

    /// <summary>Gets or sets the preferred language (e.g., "csharp", "python"). Null means any language.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the step description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the step status.</summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>Gets or sets the assigned agent ID.</summary>
    public string? AssignedAgentId { get; set; }

    /// <summary>Gets or sets the input context as JSON.</summary>
    public string? Input { get; set; }

    /// <summary>Gets or sets the output result as JSON.</summary>
    public string? Output { get; set; }

    /// <summary>Gets or sets the error message if failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the number of execution attempts.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets when the step started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets when the step completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the approval state of the step output.</summary>
    public StepApproval? Approval { get; set; }

    /// <summary>Gets or sets feedback provided when rejecting output.</summary>
    public string? ApprovalFeedback { get; set; }

    /// <summary>Gets or sets the reason for skipping this step.</summary>
    public string? SkipReason { get; set; }

    /// <summary>Gets or sets the chat history as JSON array.</summary>
    public string? ChatHistory { get; set; }

    /// <summary>Gets or sets whether this step needs rework because a dependency was re-executed.</summary>
    public bool NeedsRework { get; set; }

    /// <summary>Gets or sets the previous output before re-execution (for comparison).</summary>
    public string? PreviousOutput { get; set; }

    /// <summary>
    /// Execution wave number (1-based). Steps in the same wave run in parallel.
    /// </summary>
    public int Wave { get; set; } = 1;

    /// <summary>
    /// Gets or sets the executor override for this specific step.
    /// Values: "internal" (ReAct agents), "copilot" (GitHub Copilot CLI), or null (use story default).
    /// </summary>
    public string? ExecutorOverride { get; set; }
}

/// <summary>
/// The approval state of a step's output.
/// </summary>
public enum StepApproval
{
    /// <summary>Output approved by user.</summary>
    Approved,

    /// <summary>Output rejected, needs revision.</summary>
    Rejected,
}

/// <summary>
/// The status of a workflow step.
/// </summary>
public enum StepStatus
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>In progress.</summary>
    Running,

    /// <summary>Success.</summary>
    Completed,

    /// <summary>Error (may retry).</summary>
    Failed,

    /// <summary>Intentionally skipped.</summary>
    Skipped,
}
