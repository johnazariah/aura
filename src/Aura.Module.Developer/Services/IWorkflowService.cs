// <copyright file="IWorkflowService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Service for managing development workflows.
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Creates a new workflow.
    /// </summary>
    /// <param name="title">The workflow title.</param>
    /// <param name="description">The workflow description.</param>
    /// <param name="repositoryPath">Optional repository path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created workflow.</returns>
    Task<Workflow> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow by ID.
    /// </summary>
    /// <param name="id">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow if found, null otherwise.</returns>
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a workflow by ID with all steps.
    /// </summary>
    /// <param name="id">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow with steps if found, null otherwise.</returns>
    Task<Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all workflows, optionally filtered by status and/or repository path.
    /// </summary>
    /// <param name="status">Filter by status (optional).</param>
    /// <param name="repositoryPath">Filter by repository path (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of workflows.</returns>
    Task<IReadOnlyList<Workflow>> ListAsync(WorkflowStatus? status = null, string? repositoryPath = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a workflow and all its steps.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Enriches the workflow requirements using the issue-enrichment agent.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated workflow with analyzed context.</returns>
    Task<Workflow> AnalyzeAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Creates an execution plan using the business-analyst agent.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated workflow with steps.</returns>
    Task<Workflow> PlanAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Executes a specific step in the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="agentIdOverride">Override the agent (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The executed step.</returns>
    Task<WorkflowStep> ExecuteStepAsync(
        Guid workflowId,
        Guid stepId,
        string? agentIdOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new step to the workflow (via chat or manual).
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="name">Step name.</param>
    /// <param name="capability">Required capability.</param>
    /// <param name="description">Step description.</param>
    /// <param name="afterOrder">Insert after this order (null = end).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created step.</returns>
    Task<WorkflowStep> AddStepAsync(
        Guid workflowId,
        string name,
        string capability,
        string? description = null,
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
    Task<Workflow> CompleteAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Cancels the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cancelled workflow.</returns>
    Task<Workflow> CancelAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Sends a chat message within the workflow context to modify the plan.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="message">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chat response with any plan modifications.</returns>
    Task<WorkflowChatResponse> ChatAsync(Guid workflowId, string message, CancellationToken ct = default);

    /// <summary>
    /// Approves a step's output.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<WorkflowStep> ApproveStepAsync(Guid workflowId, Guid stepId, CancellationToken ct = default);

    /// <summary>
    /// Rejects a step's output with optional feedback.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="feedback">Feedback about why it was rejected.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<WorkflowStep> RejectStepAsync(Guid workflowId, Guid stepId, string? feedback = null, CancellationToken ct = default);

    /// <summary>
    /// Skips a step with optional reason.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="reason">Reason for skipping.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step.</returns>
    Task<WorkflowStep> SkipStepAsync(Guid workflowId, Guid stepId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Chats with an agent in the context of a specific step.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="message">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated step and agent response.</returns>
    Task<(WorkflowStep Step, string Response)> ChatWithStepAsync(Guid workflowId, Guid stepId, string message, CancellationToken ct = default);
}

/// <summary>
/// Response from a workflow chat interaction.
/// </summary>
public record WorkflowChatResponse
{
    /// <summary>Gets the assistant's response.</summary>
    public required string Response { get; init; }

    /// <summary>Gets whether the plan was modified.</summary>
    public bool PlanModified { get; init; }

    /// <summary>Gets any steps that were added.</summary>
    public IReadOnlyList<WorkflowStep> StepsAdded { get; init; } = [];

    /// <summary>Gets any steps that were removed.</summary>
    public IReadOnlyList<Guid> StepsRemoved { get; init; } = [];
    
    /// <summary>Gets whether the analysis was re-run with additional context.</summary>
    public bool AnalysisUpdated { get; init; }
}
