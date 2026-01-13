// <copyright file="DeveloperEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using System.Text;
using Aura.Api.Contracts;
using Aura.Foundation.Git;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;

/// <summary>
/// Developer module endpoints for workflow management.
/// </summary>
public static class DeveloperEndpoints
{
    /// <summary>
    /// Maps all developer endpoints to the application.
    /// </summary>
    public static WebApplication MapDeveloperEndpoints(this WebApplication app)
    {
        // Workflow CRUD
        app.MapPost("/api/developer/workflows", CreateWorkflow);
        app.MapGet("/api/developer/workflows", ListWorkflows);
        app.MapGet("/api/developer/workflows/{id:guid}", GetWorkflow);
        app.MapDelete("/api/developer/workflows/{id:guid}", DeleteWorkflow);

        // Workflow lifecycle
        app.MapPost("/api/developer/workflows/{id:guid}/analyze", AnalyzeWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/plan", PlanWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/complete", CompleteWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/cancel", CancelWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/finalize", FinalizeWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/chat", ChatWithWorkflow);

        // Step management
        app.MapPost("/api/developer/workflows/{id:guid}/steps", AddStep);
        app.MapDelete("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}", DeleteStep);

        // Step operations
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/execute", ExecuteStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/approve", ApproveStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reject", RejectStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/skip", SkipStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reset", ResetStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/chat", ChatWithStep);
        app.MapPost("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/reassign", ReassignStep);
        app.MapPut("/api/developer/workflows/{workflowId:guid}/steps/{stepId:guid}/description", UpdateStepDescription);

        return app;
    }

    private static async Task<IResult> CreateWorkflow(
        CreateWorkflowRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Title is required. Expected: { title: string, description?: string, repositoryPath?: string }" });
        }

        try
        {
            var workflow = await workflowService.CreateAsync(
                request.Title,
                request.Description,
                request.RepositoryPath,
                ct);

            return Results.Created($"/api/developer/workflows/{workflow.Id}", new
            {
                id = workflow.Id,
                title = workflow.Title,
                description = workflow.Description,
                status = workflow.Status.ToString(),
                gitBranch = workflow.GitBranch,
                worktreePath = workflow.WorktreePath,
                repositoryPath = workflow.RepositoryPath,
                createdAt = workflow.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ListWorkflows(
        IWorkflowService workflowService,
        string? status,
        string? repositoryPath,
        CancellationToken ct)
    {
        WorkflowStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<WorkflowStatus>(status, true, out var s))
        {
            statusFilter = s;
        }

        var workflows = await workflowService.ListAsync(statusFilter, repositoryPath, ct);

        return Results.Ok(new
        {
            count = workflows.Count,
            workflows = workflows.Select(w => new
            {
                id = w.Id,
                title = w.Title,
                description = w.Description,
                status = w.Status.ToString(),
                gitBranch = w.GitBranch,
                repositoryPath = w.RepositoryPath,
                worktreePath = w.WorktreePath,
                stepCount = w.Steps.Count,
                completedSteps = w.Steps.Count(s => s.Status == StepStatus.Completed),
                createdAt = w.CreatedAt,
                updatedAt = w.UpdatedAt
            })
        });
    }

    private static async Task<IResult> GetWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        var workflow = await workflowService.GetByIdWithStepsAsync(id, ct);
        if (workflow is null)
        {
            return Results.NotFound(new { error = $"Workflow {id} not found" });
        }

        return Results.Ok(new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            analyzedContext = workflow.AnalyzedContext,
            executionPlan = workflow.ExecutionPlan,
            steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                order = s.Order,
                name = s.Name,
                capability = s.Capability,
                language = s.Language,
                description = s.Description,
                status = s.Status.ToString(),
                assignedAgentId = s.AssignedAgentId,
                attempts = s.Attempts,
                output = s.Output,
                error = s.Error,
                startedAt = s.StartedAt,
                completedAt = s.CompletedAt,
                needsRework = s.NeedsRework,
                previousOutput = s.PreviousOutput,
                approval = s.Approval?.ToString(),
                chatHistory = s.ChatHistory
            }),
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            completedAt = workflow.CompletedAt,
            pullRequestUrl = workflow.PullRequestUrl
        });
    }

    private static async Task<IResult> DeleteWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        await workflowService.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AnalyzeWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var workflow = await workflowService.AnalyzeAsync(id, ct);
            return Results.Ok(new
            {
                id = workflow.Id,
                status = workflow.Status.ToString(),
                analyzedContext = workflow.AnalyzedContext,
                message = "Workflow analyzed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> PlanWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var workflow = await workflowService.PlanAsync(id, ct);
            return Results.Ok(new
            {
                id = workflow.Id,
                status = workflow.Status.ToString(),
                stepCount = workflow.Steps.Count,
                steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
                {
                    id = s.Id,
                    order = s.Order,
                    name = s.Name,
                    capability = s.Capability,
                    language = s.Language,
                    description = s.Description
                }),
                message = "Workflow planned successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> CompleteWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var workflow = await workflowService.CompleteAsync(id, ct);
            return Results.Ok(new
            {
                id = workflow.Id,
                status = workflow.Status.ToString(),
                completedAt = workflow.CompletedAt,
                pullRequestUrl = workflow.PullRequestUrl,
                message = "Workflow completed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> CancelWorkflow(
        Guid id,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var workflow = await workflowService.CancelAsync(id, ct);
            return Results.Ok(new
            {
                id = workflow.Id,
                status = workflow.Status.ToString(),
                message = "Workflow cancelled"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> FinalizeWorkflow(
        Guid id,
        FinalizeWorkflowRequest request,
        IWorkflowService workflowService,
        IGitService gitService,
        CancellationToken ct)
    {
        try
        {
            var workflow = await workflowService.GetByIdWithStepsAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = "Workflow not found" });

            if (string.IsNullOrEmpty(workflow.WorktreePath))
                return Results.BadRequest(new { error = "Workflow has no worktree path" });

            string? commitSha = null;
            string? prUrl = null;
            int? prNumber = null;

            var statusResult = await gitService.GetStatusAsync(workflow.WorktreePath, ct);
            if (statusResult.Success && statusResult.Value?.IsDirty == true)
            {
                var commitMessage = request.CommitMessage ?? $"feat: {workflow.Title}";
                var commitResult = await gitService.CommitAsync(workflow.WorktreePath, commitMessage, skipHooks: true, ct);
                if (!commitResult.Success)
                    return Results.BadRequest(new { error = $"Commit failed: {commitResult.Error}" });

                commitSha = commitResult.Value;
            }

            var pushResult = await gitService.PushAsync(workflow.WorktreePath, setUpstream: true, ct);
            if (!pushResult.Success)
                return Results.BadRequest(new { error = $"Push failed: {pushResult.Error}" });

            if (request.CreatePullRequest)
            {
                var prTitle = request.PrTitle ?? workflow.Title;
                var prBody = request.PrBody ?? BuildPrBody(workflow);

                var prResult = await gitService.CreatePullRequestAsync(
                    workflow.WorktreePath,
                    prTitle,
                    prBody,
                    request.BaseBranch,
                    request.Draft,
                    labels: ["aura-generated"],
                    ct);

                if (!prResult.Success)
                    return Results.BadRequest(new { error = $"PR creation failed: {prResult.Error}" });

                prUrl = prResult.Value?.Url;
                prNumber = prResult.Value?.Number;
            }

            if (workflow.Status != WorkflowStatus.Completed)
            {
                await workflowService.CompleteAsync(id, ct);
            }

            return Results.Ok(new
            {
                workflowId = workflow.Id,
                commitSha,
                pushed = true,
                prNumber,
                prUrl,
                message = prUrl is not null
                    ? $"Workflow finalized. PR created: {prUrl}"
                    : "Workflow finalized and pushed."
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ChatWithWorkflow(
        Guid id,
        WorkflowChatRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var response = await workflowService.ChatAsync(id, request.Message, ct);
            return Results.Ok(new
            {
                response = response.Response,
                planModified = response.PlanModified,
                stepsAdded = response.StepsAdded.Select(s => new
                {
                    id = s.Id,
                    order = s.Order,
                    name = s.Name,
                    capability = s.Capability
                }),
                stepsRemoved = response.StepsRemoved,
                analysisUpdated = response.AnalysisUpdated
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> AddStep(
        Guid id,
        AddStepRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.AddStepAsync(
                id,
                request.Name,
                request.Capability,
                request.Description,
                request.AfterOrder,
                ct);

            return Results.Created($"/api/developer/workflows/{id}/steps/{step.Id}", new
            {
                id = step.Id,
                order = step.Order,
                name = step.Name,
                capability = step.Capability,
                description = step.Description,
                status = step.Status.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteStep(
        Guid workflowId,
        Guid stepId,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        await workflowService.RemoveStepAsync(workflowId, stepId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ExecuteStep(
        Guid workflowId,
        Guid stepId,
        ExecuteStepRequest? request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.ExecuteStepAsync(workflowId, stepId, request?.AgentId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                status = step.Status.ToString(),
                assignedAgentId = step.AssignedAgentId,
                output = step.Output,
                attempts = step.Attempts,
                startedAt = step.StartedAt,
                completedAt = step.CompletedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ApproveStep(
        Guid workflowId,
        Guid stepId,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.ApproveStepAsync(workflowId, stepId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                approval = step.Approval?.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RejectStep(
        Guid workflowId,
        Guid stepId,
        RejectStepRequest? request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.RejectStepAsync(workflowId, stepId, request?.Feedback, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                approval = step.Approval?.ToString(),
                approvalFeedback = step.ApprovalFeedback
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SkipStep(
        Guid workflowId,
        Guid stepId,
        SkipStepRequest? request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.SkipStepAsync(workflowId, stepId, request?.Reason, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                status = step.Status.ToString(),
                skipReason = step.SkipReason
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ResetStep(
        Guid workflowId,
        Guid stepId,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.ResetStepAsync(workflowId, stepId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                status = step.Status.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ChatWithStep(
        Guid workflowId,
        Guid stepId,
        StepChatRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var (step, response) = await workflowService.ChatWithStepAsync(workflowId, stepId, request.Message, ct);
            return Results.Ok(new
            {
                stepId = step.Id,
                response,
                updatedDescription = step.Description
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ReassignStep(
        Guid workflowId,
        Guid stepId,
        ReassignStepRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.ReassignStepAsync(workflowId, stepId, request.AgentId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                agentId = step.AssignedAgentId,
                needsRework = step.NeedsRework
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateStepDescription(
        Guid workflowId,
        Guid stepId,
        UpdateStepDescriptionRequest request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var step = await workflowService.UpdateStepDescriptionAsync(workflowId, stepId, request.Description, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                description = step.Description,
                needsRework = step.NeedsRework
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static string BuildPrBody(Workflow workflow)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {workflow.Title}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(workflow.Description))
        {
            sb.AppendLine(workflow.Description);
            sb.AppendLine();
        }
        sb.AppendLine("### Workflow Steps");
        sb.AppendLine();
        foreach (var step in workflow.Steps.OrderBy(s => s.Order))
        {
            var status = step.Status switch
            {
                StepStatus.Completed => "✅",
                StepStatus.Skipped => "⏭",
                StepStatus.Failed => "❌",
                _ => "⬜"
            };
            sb.AppendLine($"- {status} {step.Name}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Created by [Aura](https://github.com/johnazariah/aura)*");
        return sb.ToString();
    }
}
