// <copyright file="WorkflowStep.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// A single step in the workflow execution plan.
/// This is a Developer module entity for workflow automation.
/// </summary>
public sealed class WorkflowStep
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the parent workflow ID.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>Gets or sets the parent workflow.</summary>
    public Workflow Workflow { get; set; } = null!;

    /// <summary>Gets or sets the execution order (1-based).</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the step name (e.g., "Implement UserService").</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the required capability (e.g., "csharp-coding").</summary>
    public required string Capability { get; set; }

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
