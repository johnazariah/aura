// <copyright file="StoryTask.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// A dispatch task derived from a StoryStep for parallel execution.
/// This is NOT persisted - the StoryStep is the source of truth.
/// Tasks are created from Steps at dispatch time and results are written back to Steps.
/// </summary>
/// <param name="Id">Task identifier (maps to StoryStep.Id as string).</param>
/// <param name="Title">Short title for the task (from StoryStep.Name).</param>
/// <param name="Description">Detailed prompt/instructions (from StoryStep.Description).</param>
/// <param name="Wave">Execution wave number (from StoryStep.Wave).</param>
/// <param name="DependsOn">IDs of tasks this task depends on.</param>
/// <param name="Status">Current execution status.</param>
/// <param name="AgentSessionId">Agent session ID when running.</param>
/// <param name="Output">Agent output/result after completion.</param>
/// <param name="Error">Error message if the task failed.</param>
/// <param name="ToolImprovementProposal">Agent-proposed improvements to tools.</param>
/// <param name="StartedAt">When the task started execution.</param>
/// <param name="CompletedAt">When the task completed or failed.</param>
public record StoryTask(
    string Id,
    string Title,
    string Description,
    int Wave,
    string[] DependsOn,
    StoryTaskStatus Status = StoryTaskStatus.Pending,
    string? AgentSessionId = null,
    string? Output = null,
    string? Error = null,
    string? ToolImprovementProposal = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null)
{
    /// <summary>
    /// Creates a dispatch task from a StoryStep.
    /// </summary>
    public static StoryTask FromStep(StoryStep step, string[]? dependsOn = null) => new(
        Id: step.Id.ToString(),
        Title: step.Name,
        Description: step.Description ?? step.Name,
        Wave: step.Wave,
        DependsOn: dependsOn ?? [],
        Status: step.Status switch
        {
            StepStatus.Pending => StoryTaskStatus.Pending,
            StepStatus.Running => StoryTaskStatus.Running,
            StepStatus.Completed => StoryTaskStatus.Completed,
            StepStatus.Failed => StoryTaskStatus.Failed,
            StepStatus.Skipped => StoryTaskStatus.Skipped,
            _ => StoryTaskStatus.Pending,
        },
        AgentSessionId: step.AssignedAgentId,
        Output: step.Output,
        Error: step.Error,
        StartedAt: step.StartedAt,
        CompletedAt: step.CompletedAt);
}

/// <summary>
/// The execution status of a story task.
/// </summary>
public enum StoryTaskStatus
{
    /// <summary>Task is waiting to be executed.</summary>
    Pending,

    /// <summary>Task is currently being executed by an agent.</summary>
    Running,

    /// <summary>Task completed successfully.</summary>
    Completed,

    /// <summary>Task failed (may be retried).</summary>
    Failed,

    /// <summary>Task was skipped (dependency failed or user choice).</summary>
    Skipped,
}
