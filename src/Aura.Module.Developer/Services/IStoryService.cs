// <copyright file="IWorkflowService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Service for managing development workflows.
/// </summary>
public interface IStoryService
{
    /// <summary>
    /// Creates a new workflow.
    /// </summary>
    /// <param name="title">The workflow title.</param>
    /// <param name="description">The workflow description.</param>
    /// <param name="repositoryPath">Optional repository path.</param>
    /// <param name="automationMode">Automation mode (Assisted, Autonomous, FullAutonomous).</param>
    /// <param name="issueUrl">Optional external issue URL to link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created workflow.</returns>
    Task<Story> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        AutomationMode automationMode = AutomationMode.Assisted,
        string? issueUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new workflow from a guardian check.
    /// </summary>
    /// <param name="request">The guardian workflow creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created workflow.</returns>
    Task<Story> CreateFromGuardianAsync(
        GuardianWorkflowRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow by ID.
    /// </summary>
    /// <param name="id">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow if found, null otherwise.</returns>
    Task<Story?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow by ID with all steps.
    /// </summary>
    /// <param name="id">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow with steps if found, null otherwise.</returns>
    Task<Story?> GetByIdWithStepsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow by its worktree path.
    /// </summary>
    /// <param name="worktreePath">The absolute path to the worktree.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow if found, null otherwise.</returns>
    Task<Story?> GetByWorktreePathAsync(string worktreePath, CancellationToken ct = default);

    /// <summary>
    /// Lists all workflows, optionally filtered by status and/or repository path.
    /// </summary>
    /// <param name="status">Filter by status (optional).</param>
    /// <param name="repositoryPath">Filter by repository path (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of workflows.</returns>
    Task<IReadOnlyList<Story>> ListAsync(StoryStatus? status = null, string? repositoryPath = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workflow and all its steps.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Resets a workflow's status (for recovery from failed states).
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="newStatus">The new status to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated workflow.</returns>
    Task<Story> ResetStatusAsync(Guid workflowId, StoryStatus newStatus, CancellationToken ct = default);

    /// <summary>
    /// Updates a workflow.
    /// </summary>
    /// <param name="workflow">The workflow to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(Story workflow, CancellationToken ct = default);

    /// <summary>
    /// Updates a workflow step.
    /// </summary>
    /// <param name="step">The step to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateStepAsync(StoryStep step, CancellationToken ct = default);

    /// <summary>
    /// Enriches the workflow requirements using the issue-enrichment agent.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated workflow with analyzed context.</returns>
    Task<Story> AnalyzeAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Creates an execution plan using the business-analyst agent.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated workflow with steps.</returns>
    Task<Story> PlanAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Executes a specific step in the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="agentIdOverride">Override the agent (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The executed step.</returns>
    Task<StoryStep> ExecuteStepAsync(
        Guid workflowId,
        Guid stepId,
        string? agentIdOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes all pending steps in the workflow according to the automation mode.
    /// In Assisted mode, only executes safe steps.
    /// In Autonomous mode, executes steps that don't require confirmation.
    /// In FullAutonomous mode, executes all steps.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stopOnError">Whether to stop if a step fails.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing executed steps and any errors.</returns>
    Task<ExecuteAllResult> ExecuteAllStepsAsync(
        Guid workflowId,
        bool stopOnError = true,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new step to the workflow (via chat or manual).
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="name">Step name.</param>
    /// <param name="capability">Required capability.</param>
    /// <param name="description">Step description.</param>
    /// <param name="input">Input context as JSON (tool arguments).</param>
    /// <param name="afterOrder">Insert after this order (null = end).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created step.</returns>
    Task<StoryStep> AddStepAsync(
        Guid workflowId,
        string name,
        string capability,
        string? description = null,
        string? input = null,
        int? afterOrder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a step from the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default);

    /// <summary>
    /// Marks the workflow as complete.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The completed workflow.</returns>
    Task<Story> CompleteAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Cancels the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cancelled workflow.</returns>
    Task<Story> CancelAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Sends a chat message within the workflow context to modify the plan.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="message">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chat response with any plan modifications.</returns>
    Task<StoryChatResponse> ChatAsync(Guid workflowId, string message, CancellationToken ct = default);

    /// <summary>
    /// Approves a step's output.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<StoryStep> ApproveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default);

    /// <summary>
    /// Rejects a step's output with optional feedback.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="feedback">Feedback about why it was rejected.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<StoryStep> RejectStepAsync(Guid workflowId, Guid stepId, string? feedback = null, CancellationToken ct = default);

    /// <summary>
    /// Skips a step with optional reason.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="reason">Reason for skipping.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<StoryStep> SkipStepAsync(Guid workflowId, Guid stepId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Resets a step to pending status so it can be re-executed.
    /// Works for any step status (Failed, Completed, Skipped, etc.).
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reset step.</returns>
    Task<StoryStep> ResetStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default);

    /// <summary>
    /// Chats with an agent in the context of a specific step.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="message">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step and agent response.</returns>
    Task<(StoryStep Step, string Response)> ChatWithStepAsync(Guid workflowId, Guid stepId, string message, CancellationToken ct = default);

    /// <summary>
    /// Reassigns a step to a different agent.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="agentId">The new agent ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<StoryStep> ReassignStepAsync(Guid workflowId, Guid stepId, string agentId, CancellationToken ct = default);

    /// <summary>
    /// Updates a step's description.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="description">The new description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<StoryStep> UpdateStepDescriptionAsync(Guid workflowId, Guid stepId, string description, CancellationToken ct = default);
}

/// <summary>
/// Response from a workflow chat interaction.
/// </summary>
public record StoryChatResponse
{
    /// <summary>Gets the assistant's response.</summary>
    public required string Response { get; init; }

    /// <summary>Gets whether the plan was modified.</summary>
    public bool PlanModified { get; init; }

    /// <summary>Gets any steps that were added.</summary>
    public IReadOnlyList<StoryStep> StepsAdded { get; init; } = [];

    /// <summary>Gets any steps that were removed.</summary>
    public IReadOnlyList<Guid> StepsRemoved { get; init; } = [];

    /// <summary>Gets whether the analysis was re-run with additional context.</summary>
    public bool AnalysisUpdated { get; init; }
}

/// <summary>
/// Result from executing all steps in a workflow.
/// </summary>
public record ExecuteAllResult
{
    /// <summary>Gets whether all steps completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the steps that were executed.</summary>
    public required IReadOnlyList<StoryStep> ExecutedSteps { get; init; }

    /// <summary>Gets the steps that were skipped (require user confirmation in current automation mode).</summary>
    public required IReadOnlyList<StoryStep> SkippedSteps { get; init; }

    /// <summary>Gets the step that failed, if any.</summary>
    public StoryStep? FailedStep { get; init; }

    /// <summary>Gets the error message if a step failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets whether execution was stopped due to an error.</summary>
    public bool StoppedOnError { get; init; }

    /// <summary>Gets whether there are steps requiring user confirmation.</summary>
    public bool HasPendingConfirmations => SkippedSteps.Count > 0;
}

/// <summary>
/// Request to create a workflow from a guardian check.
/// </summary>
public record GuardianWorkflowRequest
{
    /// <summary>Gets the workflow title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the workflow description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the repository path.</summary>
    public string? RepositoryPath { get; init; }

    /// <summary>Gets the guardian ID that detected this issue.</summary>
    public required string GuardianId { get; init; }

    /// <summary>Gets the workflow priority.</summary>
    public StoryPriority Priority { get; init; } = StoryPriority.Medium;

    /// <summary>Gets the suggested capability/agent for this workflow.</summary>
    public string? SuggestedCapability { get; init; }

    /// <summary>Gets additional context from the guardian check.</summary>
    public string? Context { get; init; }
}
