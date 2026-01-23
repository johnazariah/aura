// <copyright file="StoryTask.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// A parallelizable task within a story, dispatched to a GH Copilot CLI agent.
/// Tasks are organized into waves where all tasks in a wave can run in parallel.
/// </summary>
/// <param name="Id">Unique task identifier (e.g., "task-1").</param>
/// <param name="Title">Short title for the task.</param>
/// <param name="Description">Detailed prompt/instructions for the agent.</param>
/// <param name="Wave">Execution wave number (1-based). Tasks in the same wave run in parallel.</param>
/// <param name="DependsOn">IDs of tasks this task depends on (must complete first).</param>
/// <param name="Status">Current execution status.</param>
/// <param name="AgentSessionId">GH Copilot CLI session ID when running.</param>
/// <param name="Output">Agent output/result after completion.</param>
/// <param name="Error">Error message if the task failed.</param>
/// <param name="ToolImprovementProposal">Agent-proposed improvements to Aura tools if fallback was needed.</param>
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
    DateTimeOffset? CompletedAt = null);

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
