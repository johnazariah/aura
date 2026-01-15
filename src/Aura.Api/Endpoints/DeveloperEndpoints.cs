// <copyright file="DeveloperEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using System.Text;
using Aura.Api.Contracts;
using Aura.Foundation.Git;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
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
        app.MapGet("/api/developer/workflows/by-path", GetWorkflowByPath);
        app.MapGet("/api/developer/workflows/{id:guid}", GetWorkflow);
        app.MapDelete("/api/developer/workflows/{id:guid}", DeleteWorkflow);

        // Workflow lifecycle
        app.MapPost("/api/developer/workflows/{id:guid}/analyze", AnalyzeWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/plan", PlanWorkflow);
        app.MapPost("/api/developer/workflows/{id:guid}/execute-all", ExecuteAllSteps);
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

        // Story/Issue integration endpoints
        app.MapPost("/api/developer/stories/from-issue", CreateStoryFromIssue);
        app.MapPost("/api/developer/workflows/{id:guid}/refresh-from-issue", RefreshFromIssue);
        app.MapPost("/api/developer/workflows/{id:guid}/post-update", PostUpdateToIssue);
        app.MapPost("/api/developer/workflows/{id:guid}/close-issue", CloseLinkedIssue);

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
            // Parse mode from string
            var mode = WorkflowMode.Structured;
            if (!string.IsNullOrEmpty(request.Mode) && Enum.TryParse<WorkflowMode>(request.Mode, true, out var m))
            {
                mode = m;
            }

            // Parse automation mode from string
            var automationMode = AutomationMode.Assisted;
            if (!string.IsNullOrEmpty(request.AutomationMode) && Enum.TryParse<AutomationMode>(request.AutomationMode, true, out var am))
            {
                automationMode = am;
            }

            var workflow = await workflowService.CreateAsync(
                request.Title,
                request.Description,
                request.RepositoryPath,
                mode,
                automationMode,
                request.IssueUrl,
                ct);

            return Results.Created($"/api/developer/workflows/{workflow.Id}", new
            {
                id = workflow.Id,
                title = workflow.Title,
                description = workflow.Description,
                status = workflow.Status.ToString(),
                mode = workflow.Mode.ToString(),
                automationMode = workflow.AutomationMode.ToString(),
                gitBranch = workflow.GitBranch,
                worktreePath = workflow.WorktreePath,
                repositoryPath = workflow.RepositoryPath,
                issueUrl = workflow.IssueUrl,
                issueProvider = workflow.IssueProvider?.ToString(),
                issueNumber = workflow.IssueNumber,
                issueOwner = workflow.IssueOwner,
                issueRepo = workflow.IssueRepo,
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
                mode = w.Mode.ToString(),
                gitBranch = w.GitBranch,
                repositoryPath = w.RepositoryPath,
                worktreePath = w.WorktreePath,
                issueUrl = w.IssueUrl,
                issueNumber = w.IssueNumber,
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
            mode = workflow.Mode.ToString(),
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
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
            chatHistory = workflow.ChatHistory,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            completedAt = workflow.CompletedAt,
            pullRequestUrl = workflow.PullRequestUrl
        });
    }

    private static async Task<IResult> GetWorkflowByPath(
        string path,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Results.BadRequest(new { error = "Path query parameter is required" });
        }

        var workflow = await workflowService.GetByWorktreePathAsync(path, ct);
        if (workflow is null)
        {
            return Results.NotFound(new { error = $"No workflow found for path: {path}" });
        }

        return Results.Ok(new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            mode = workflow.Mode.ToString(),
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            chatHistory = workflow.ChatHistory,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            completedAt = workflow.CompletedAt
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

    private static async Task<IResult> ExecuteAllSteps(
        Guid id,
        ExecuteAllStepsRequest? request,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        try
        {
            var result = await workflowService.ExecuteAllStepsAsync(
                id,
                request?.StopOnError ?? true,
                ct);

            return Results.Ok(new
            {
                success = result.Success,
                executedSteps = result.ExecutedSteps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    status = s.Status.ToString(),
                    completedAt = s.CompletedAt,
                }),
                skippedSteps = result.SkippedSteps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    capability = s.Capability,
                    reason = "Requires user confirmation",
                }),
                failedStep = result.FailedStep is null ? null : new
                {
                    id = result.FailedStep.Id,
                    name = result.FailedStep.Name,
                    error = result.Error,
                },
                stoppedOnError = result.StoppedOnError,
                hasPendingConfirmations = result.HasPendingConfirmations,
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

    // =========================================================================
    // Story/Issue Integration Endpoints
    // =========================================================================

    private static async Task<IResult> CreateStoryFromIssue(
        CreateStoryFromIssueRequest request,
        IGitHubService gitHub,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IssueUrl))
        {
            return Results.BadRequest(new { error = "IssueUrl is required" });
        }

        // Parse issue URL
        var parsed = gitHub.ParseIssueUrl(request.IssueUrl);
        if (parsed is null)
        {
            return Results.BadRequest(new { error = "Invalid GitHub issue URL. Expected format: https://github.com/owner/repo/issues/123" });
        }

        if (!gitHub.IsConfigured)
        {
            return Results.BadRequest(new { error = "GitHub integration not configured. Set GitHub:Token in appsettings.json" });
        }

        try
        {
            // Fetch issue from GitHub
            var issue = await gitHub.GetIssueAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, ct);

            // Parse mode
            var mode = WorkflowMode.Conversational; // Default to conversational for issue-based stories
            if (!string.IsNullOrEmpty(request.Mode) && Enum.TryParse<WorkflowMode>(request.Mode, true, out var m))
            {
                mode = m;
            }

            // Create workflow/story
            var workflow = await workflowService.CreateAsync(
                issue.Title,
                issue.Body,
                request.RepositoryPath,
                mode,
                AutomationMode.Assisted, // Issue-based workflows default to assisted mode
                request.IssueUrl,
                ct);

            // Post a comment to the issue that work has started
            var branch = workflow.GitBranch ?? "unknown";
            await gitHub.PostCommentAsync(
                parsed.Value.Owner,
                parsed.Value.Repo,
                parsed.Value.Number,
                $"Started work in branch `{branch}`",
                ct);

            return Results.Created($"/api/developer/workflows/{workflow.Id}", new
            {
                id = workflow.Id,
                title = workflow.Title,
                description = workflow.Description,
                status = workflow.Status.ToString(),
                mode = workflow.Mode.ToString(),
                gitBranch = workflow.GitBranch,
                worktreePath = workflow.WorktreePath,
                repositoryPath = workflow.RepositoryPath,
                issueUrl = workflow.IssueUrl,
                issueProvider = workflow.IssueProvider?.ToString(),
                issueNumber = workflow.IssueNumber,
                issueOwner = workflow.IssueOwner,
                issueRepo = workflow.IssueRepo,
                createdAt = workflow.CreatedAt
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.BadRequest(new { error = $"Failed to fetch issue from GitHub: {ex.Message}" });
        }
    }

    private static async Task<IResult> RefreshFromIssue(
        Guid id,
        IGitHubService gitHub,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        var workflow = await workflowService.GetByIdAsync(id, ct);
        if (workflow is null)
        {
            return Results.NotFound(new { error = $"Workflow {id} not found" });
        }

        if (string.IsNullOrEmpty(workflow.IssueUrl) ||
            workflow.IssueOwner is null ||
            workflow.IssueRepo is null ||
            workflow.IssueNumber is null)
        {
            return Results.BadRequest(new { error = "Workflow is not linked to a GitHub issue" });
        }

        if (!gitHub.IsConfigured)
        {
            return Results.BadRequest(new { error = "GitHub integration not configured" });
        }

        try
        {
            var issue = await gitHub.GetIssueAsync(
                workflow.IssueOwner,
                workflow.IssueRepo,
                workflow.IssueNumber.Value,
                ct);

            var changes = new List<string>();

            // Update title if changed
            if (workflow.Title != issue.Title)
            {
                workflow.Title = issue.Title;
                changes.Add("title");
            }

            // Update description if changed
            if (workflow.Description != issue.Body)
            {
                workflow.Description = issue.Body;
                changes.Add("description");
            }

            if (changes.Count > 0)
            {
                workflow.UpdatedAt = DateTimeOffset.UtcNow;
                await workflowService.UpdateAsync(workflow, ct);
            }

            return Results.Ok(new
            {
                updated = changes.Count > 0,
                changes
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.BadRequest(new { error = $"Failed to fetch issue: {ex.Message}" });
        }
    }

    private static async Task<IResult> PostUpdateToIssue(
        Guid id,
        PostUpdateRequest request,
        IGitHubService gitHub,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Message is required" });
        }

        var workflow = await workflowService.GetByIdAsync(id, ct);
        if (workflow is null)
        {
            return Results.NotFound(new { error = $"Workflow {id} not found" });
        }

        if (workflow.IssueOwner is null ||
            workflow.IssueRepo is null ||
            workflow.IssueNumber is null)
        {
            return Results.BadRequest(new { error = "Workflow is not linked to a GitHub issue" });
        }

        if (!gitHub.IsConfigured)
        {
            return Results.BadRequest(new { error = "GitHub integration not configured" });
        }

        try
        {
            await gitHub.PostCommentAsync(
                workflow.IssueOwner,
                workflow.IssueRepo,
                workflow.IssueNumber.Value,
                request.Message,
                ct);

            return Results.Ok(new { posted = true });
        }
        catch (HttpRequestException ex)
        {
            return Results.BadRequest(new { error = $"Failed to post comment: {ex.Message}" });
        }
    }

    private static async Task<IResult> CloseLinkedIssue(
        Guid id,
        CloseIssueRequest? request,
        IGitHubService gitHub,
        IWorkflowService workflowService,
        CancellationToken ct)
    {
        var workflow = await workflowService.GetByIdAsync(id, ct);
        if (workflow is null)
        {
            return Results.NotFound(new { error = $"Workflow {id} not found" });
        }

        if (workflow.IssueOwner is null ||
            workflow.IssueRepo is null ||
            workflow.IssueNumber is null)
        {
            return Results.BadRequest(new { error = "Workflow is not linked to a GitHub issue" });
        }

        if (!gitHub.IsConfigured)
        {
            return Results.BadRequest(new { error = "GitHub integration not configured" });
        }

        try
        {
            // Post closing comment if provided
            if (!string.IsNullOrEmpty(request?.Comment))
            {
                await gitHub.PostCommentAsync(
                    workflow.IssueOwner,
                    workflow.IssueRepo,
                    workflow.IssueNumber.Value,
                    request.Comment,
                    ct);
            }

            // Close the issue
            await gitHub.CloseIssueAsync(
                workflow.IssueOwner,
                workflow.IssueRepo,
                workflow.IssueNumber.Value,
                ct);

            return Results.Ok(new { closed = true });
        }
        catch (HttpRequestException ex)
        {
            return Results.BadRequest(new { error = $"Failed to close issue: {ex.Message}" });
        }
    }
}
