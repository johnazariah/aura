// <copyright file="DeveloperEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using System.Text;
using System.Text.Json;
using Aura.Api.Contracts;
using Aura.Api.Problems;
using Aura.Foundation.Git;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;

/// <summary>
/// Developer module endpoints for story management.
/// </summary>
public static class DeveloperEndpoints
{
    /// <summary>
    /// Maps all developer endpoints to the application.
    /// </summary>
    public static WebApplication MapDeveloperEndpoints(this WebApplication app)
    {
        // story CRUD
        app.MapPost("/api/developer/stories", CreateStory);
        app.MapGet("/api/developer/stories", ListStories);
        app.MapGet("/api/developer/stories/by-path", GetStoryByPath);
        app.MapGet("/api/developer/stories/{id:guid}", GetStory);
        app.MapDelete("/api/developer/stories/{id:guid}", DeleteStory);
        app.MapPatch("/api/developer/stories/{id:guid}/status", ResetStoryStatus);
        app.MapPatch("/api/developer/stories/{id:guid}/orchestrator", ResetOrchestratorStatus);

        // story lifecycle
        app.MapPost("/api/developer/stories/{id:guid}/analyze", AnalyzeStory);
        app.MapPost("/api/developer/stories/{id:guid}/plan", PlanStory);
        app.MapPost("/api/developer/stories/{id:guid}/decompose", DecomposeStory);
        app.MapPost("/api/developer/stories/{id:guid}/run", RunStory);
        app.MapGet("/api/developer/stories/{id:guid}/stream", StreamStoryExecution);
        app.MapGet("/api/developer/stories/{id:guid}/orchestrator-status", GetOrchestratorStatus);
        app.MapPost("/api/developer/stories/{id:guid}/execute-all", ExecuteAllSteps);
        app.MapPost("/api/developer/stories/{id:guid}/complete", CompleteStory);
        app.MapPost("/api/developer/stories/{id:guid}/cancel", CancelStory);
        app.MapPost("/api/developer/stories/{id:guid}/finalize", FinalizeStory);
        app.MapPost("/api/developer/stories/{id:guid}/chat", ChatWithStory);

        // Step management
        app.MapPost("/api/developer/stories/{id:guid}/steps", AddStep);
        app.MapDelete("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}", DeleteStep);

        // Step operations
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/execute", ExecuteStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/approve", ApproveStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/reject", RejectStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/skip", SkipStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/reset", ResetStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/chat", ChatWithStep);
        app.MapPost("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/reassign", ReassignStep);
        app.MapPut("/api/developer/stories/{storyId:guid}/steps/{stepId:guid}/description", UpdateStepDescription);

        // Story/Issue integration endpoints
        app.MapPost("/api/developer/stories/from-issue", CreateStoryFromIssue);
        app.MapPost("/api/developer/stories/{id:guid}/refresh-from-issue", RefreshFromIssue);
        app.MapPost("/api/developer/stories/{id:guid}/post-update", PostUpdateToIssue);
        app.MapPost("/api/developer/stories/{id:guid}/close-issue", CloseLinkedIssue);

        return app;
    }

    private static async Task<IResult> CreateStory(
        CreateStoryRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Problem.MissingRequiredField("Title", "Expected: { title: string, description?: string, repositoryPath?: string }", context);
        }

        try
        {
            // Parse automation mode from string
            var automationMode = AutomationMode.Assisted;
            if (!string.IsNullOrEmpty(request.AutomationMode) && Enum.TryParse<AutomationMode>(request.AutomationMode, true, out var am))
            {
                automationMode = am;
            }

            // Parse dispatch target from string
            var dispatchTarget = DispatchTarget.CopilotCli;
            if (!string.IsNullOrEmpty(request.DispatchTarget) && Enum.TryParse<DispatchTarget>(request.DispatchTarget, true, out var dt))
            {
                dispatchTarget = dt;
            }

            var story = await storyService.CreateAsync(
                request.Title,
                request.Description,
                request.RepositoryPath,
                automationMode,
                request.IssueUrl,
                dispatchTarget,
                ct);

            return Results.Created($"/api/developer/stories/{story.Id}", new
            {
                id = story.Id,
                title = story.Title,
                description = story.Description,
                status = story.Status.ToString(),
                automationMode = story.AutomationMode.ToString(),
                dispatchTarget = story.DispatchTarget.ToString(),
                gitBranch = story.GitBranch,
                worktreePath = story.WorktreePath,
                repositoryPath = story.RepositoryPath,
                issueUrl = story.IssueUrl,
                issueProvider = story.IssueProvider?.ToString(),
                issueNumber = story.IssueNumber,
                issueOwner = story.IssueOwner,
                issueRepo = story.IssueRepo,
                createdAt = story.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ListStories(
        IStoryService storyService,
        string? status,
        string? repositoryPath,
        CancellationToken ct)
    {
        StoryStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StoryStatus>(status, true, out var s))
        {
            statusFilter = s;
        }

        var stories = await storyService.ListAsync(statusFilter, repositoryPath, ct);

        return Results.Ok(new
        {
            count = stories.Count,
            stories = stories.Select(w => new
            {
                id = w.Id,
                title = w.Title,
                description = w.Description,
                status = GetEffectiveStatus(w),
                gitBranch = w.GitBranch,
                repositoryPath = w.RepositoryPath,
                worktreePath = w.WorktreePath,
                issueUrl = w.IssueUrl,
                issueNumber = w.IssueNumber,
                currentWave = w.CurrentWave,
                waveCount = GetWaveCount(w),
                createdAt = w.CreatedAt,
                updatedAt = w.UpdatedAt
            })
        });
    }

    private static async Task<IResult> GetStory(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        var story = await storyService.GetByIdWithStepsAsync(id, ct);
        if (story is null)
        {
            return Problem.StoryNotFound(id, context);
        }

        var waveCount = GetWaveCount(story);

        return Results.Ok(new
        {
            id = story.Id,
            title = story.Title,
            description = story.Description,
            // Use story status directly
            status = GetEffectiveStatus(story),
            automationMode = story.AutomationMode.ToString(),
            dispatchTarget = story.DispatchTarget.ToString(),
            gitBranch = story.GitBranch,
            worktreePath = story.WorktreePath,
            repositoryPath = story.RepositoryPath,
            issueUrl = story.IssueUrl,
            issueProvider = story.IssueProvider?.ToString(),
            issueNumber = story.IssueNumber,
            issueOwner = story.IssueOwner,
            issueRepo = story.IssueRepo,
            analyzedContext = story.AnalyzedContext,
            executionPlan = story.ExecutionPlan,
            // Wave-based execution state
            currentWave = story.CurrentWave,
            waveCount,
            maxParallelism = story.MaxParallelism,
            gateMode = story.GateMode.ToString(),
            gateResult = story.GateResult,
            // Steps with wave for parallel execution
            steps = story.Steps.OrderBy(s => s.Wave).ThenBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                order = s.Order,
                wave = s.Wave,
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
            chatHistory = story.ChatHistory,
            createdAt = story.CreatedAt,
            updatedAt = story.UpdatedAt,
            completedAt = story.CompletedAt,
            pullRequestUrl = story.PullRequestUrl
        });
    }

    private static async Task<IResult> GetStoryByPath(
        string path,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Problem.MissingRequiredField("Path", "Query parameter is required.", context);
        }

        var story = await storyService.GetByWorktreePathAsync(path, ct);
        if (story is null)
        {
            return Problem.StoryNotFoundByPath(path, context);
        }

        return Results.Ok(new
        {
            id = story.Id,
            title = story.Title,
            description = story.Description,
            status = story.Status.ToString(),
            gitBranch = story.GitBranch,
            worktreePath = story.WorktreePath,
            repositoryPath = story.RepositoryPath,
            issueUrl = story.IssueUrl,
            issueProvider = story.IssueProvider?.ToString(),
            issueNumber = story.IssueNumber,
            issueOwner = story.IssueOwner,
            issueRepo = story.IssueRepo,
            chatHistory = story.ChatHistory,
            createdAt = story.CreatedAt,
            updatedAt = story.UpdatedAt,
            completedAt = story.CompletedAt
        });
    }

    private static async Task<IResult> DeleteStory(
        Guid id,
        IStoryService storyService,
        CancellationToken ct)
    {
        await storyService.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetStoryStatus(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        HttpRequest request,
        CancellationToken ct)
    {
        try
        {
            var body = await request.ReadFromJsonAsync<ResetStatusRequest>(ct);
            if (body is null || string.IsNullOrEmpty(body.Status))
            {
                return Problem.MissingRequiredField("Status", null, context);
            }

            if (!Enum.TryParse<StoryStatus>(body.Status, ignoreCase: true, out var newStatus))
            {
                return Problem.InvalidStatus(body.Status, context);
            }

            var story = await storyService.ResetStatusAsync(id, newStatus, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                message = $"Status reset to {story.Status}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private record ResetStatusRequest(string Status);
    private record ResetOrchestratorRequest(bool ResetFailedTasks = false);

    private static async Task<IResult> ResetOrchestratorStatus(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        HttpRequest request,
        CancellationToken ct)
    {
        try
        {
            var body = await request.ReadFromJsonAsync<ResetOrchestratorRequest>(ct);
            var resetFailedSteps = body?.ResetFailedTasks ?? false;

            var story = await storyService.ResetOrchestratorAsync(id, resetFailedSteps, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                currentWave = story.CurrentWave,
                message = $"Orchestrator reset to {story.Status}. Call /run to retry.",
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> AnalyzeStory(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var story = await storyService.AnalyzeAsync(id, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                analyzedContext = story.AnalyzedContext,
                message = "story analyzed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> PlanStory(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var story = await storyService.PlanAsync(id, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                stepCount = story.Steps.Count,
                steps = story.Steps.OrderBy(s => s.Order).Select(s => new
                {
                    id = s.Id,
                    order = s.Order,
                    name = s.Name,
                    capability = s.Capability,
                    language = s.Language,
                    description = s.Description
                }),
                message = "story planned successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
        catch (Exception ex)
        {
            return Problem.InternalError($"Plan failed: {ex.Message}", context);
        }
    }

    private static async Task<IResult> DecomposeStory(
        Guid id,
        DecomposeStoryRequest? request,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var maxParallelism = request?.MaxParallelism ?? 4;
            var includeTests = request?.IncludeTests ?? true;

            var result = await storyService.DecomposeAsync(id, maxParallelism, includeTests, ct);

            return Results.Ok(new DecomposeStoryResponse(
                result.StoryId,
                result.Steps.Select(s => new StoryTaskDto(
                    s.Id.ToString(),
                    s.Name,
                    s.Description ?? string.Empty,
                    s.Wave,
                    [], // No DependsOn in Steps, we use Wave ordering
                    s.Status.ToString(),
                    null)).ToList(),
                result.WaveCount));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RunStory(
        Guid id,
        IStoryService storyService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        try
        {
            // Get GitHub token from header for CopilotCli dispatcher
            var githubToken = httpContext.Request.Headers["X-GitHub-Token"].FirstOrDefault();

            var result = await storyService.RunAsync(id, githubToken, ct);

            return Results.Ok(new
            {
                storyId = result.StoryId,
                status = result.Status.ToString(),
                currentWave = result.CurrentWave,
                totalWaves = result.TotalWaves,
                isComplete = result.IsComplete,
                waitingForGate = result.WaitingForGate,
                startedSteps = result.StartedSteps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    wave = s.Wave,
                    status = s.Status.ToString(),
                }),
                completedSteps = result.CompletedSteps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    wave = s.Wave,
                }),
                failedSteps = result.FailedSteps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    wave = s.Wave,
                    error = s.Error,
                }),
                gateResult = result.GateResult != null ? new
                {
                    passed = result.GateResult.Passed,
                    gateType = result.GateResult.GateType,
                    afterWave = result.GateResult.AfterWave,
                    buildOutput = result.GateResult.BuildOutput,
                    testOutput = result.GateResult.TestOutput,
                    testsPassed = result.GateResult.TestsPassed,
                    testsFailed = result.GateResult.TestsFailed,
                    wasCancelled = result.GateResult.WasCancelled,
                    error = result.GateResult.Error,
                } : null,
                error = result.Error,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task StreamStoryExecution(
        Guid id,
        IStoryService storyService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Get GitHub token from header for CopilotCli dispatcher
        var githubToken = httpContext.Request.Headers["X-GitHub-Token"].FirstOrDefault();

        try
        {
            await foreach (var evt in storyService.RunStreamAsync(id, githubToken, ct))
            {
                var eventData = JsonSerializer.Serialize(new
                {
                    type = evt.Type.ToString(),
                    storyId = evt.StoryId,
                    timestamp = evt.Timestamp,
                    wave = evt.Wave,
                    totalWaves = evt.TotalWaves,
                    stepId = evt.StepId,
                    stepName = evt.StepName,
                    output = evt.Output,
                    error = evt.Error,
                    gateResult = evt.GateResult != null ? new
                    {
                        passed = evt.GateResult.Passed,
                        gateType = evt.GateResult.GateType,
                        afterWave = evt.GateResult.AfterWave,
                        buildOutput = evt.GateResult.BuildOutput,
                        testOutput = evt.GateResult.TestOutput,
                        error = evt.GateResult.Error,
                    } : null,
                });

                var eventName = evt.Type.ToString().ToLowerInvariant();
                await httpContext.Response.WriteAsync($"event: {eventName}\ndata: {eventData}\n\n", ct);
                await httpContext.Response.Body.FlushAsync(ct);
            }

            // Send final done event
            await httpContext.Response.WriteAsync("event: done\ndata: {}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            var errorData = JsonSerializer.Serialize(new { message = ex.Message });
            await httpContext.Response.WriteAsync($"event: error\ndata: {errorData}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - this is expected
        }
    }

    private static async Task<IResult> GetOrchestratorStatus(
        Guid id,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var result = await storyService.GetOrchestratorStatusAsync(id, ct);

            return Results.Ok(new
            {
                storyId = result.StoryId,
                status = result.Status.ToString(),
                currentWave = result.CurrentWave,
                totalWaves = result.TotalWaves,
                maxParallelism = result.MaxParallelism,
                steps = result.Steps.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    description = s.Description,
                    wave = s.Wave,
                    status = s.Status.ToString(),
                    startedAt = s.StartedAt,
                    completedAt = s.CompletedAt,
                    error = s.Error,
                }),
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> CompleteStory(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            // Get GitHub token from header for push/PR operations
            var githubToken = context.Request.Headers["X-GitHub-Token"].FirstOrDefault();

            var story = await storyService.CompleteAsync(id, githubToken, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                completedAt = story.CompletedAt,
                pullRequestUrl = story.PullRequestUrl,
                message = "story completed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> CancelStory(
        Guid id,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var story = await storyService.CancelAsync(id, ct);
            return Results.Ok(new
            {
                id = story.Id,
                status = story.Status.ToString(),
                message = "story cancelled"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> FinalizeStory(
        Guid id,
        FinalizeStoryRequest request,
        HttpContext context,
        IStoryService storyService,
        IGitService gitService,
        CancellationToken ct)
    {
        try
        {
            // Get GitHub token from header for push/PR operations
            var githubToken = context.Request.Headers["X-GitHub-Token"].FirstOrDefault();

            var story = await storyService.GetByIdWithStepsAsync(id, ct);
            if (story is null)
                return Problem.StoryNotFound(id, context);

            if (string.IsNullOrEmpty(story.WorktreePath))
                return Problem.InvalidState("Story has no worktree path.", context);

            string? commitSha = null;
            string? prUrl = null;
            int? prNumber = null;

            var statusResult = await gitService.GetStatusAsync(story.WorktreePath, ct);
            if (statusResult.Success && statusResult.Value?.IsDirty == true)
            {
                var commitMessage = request.CommitMessage ?? $"feat: {story.Title}";
                var commitResult = await gitService.CommitAsync(story.WorktreePath, commitMessage, skipHooks: true, ct);
                if (!commitResult.Success)
                    return Problem.GitOperationFailed("Commit", commitResult.Error ?? "Unknown error", context);

                commitSha = commitResult.Value;
            }

            var pushResult = await gitService.PushAsync(story.WorktreePath, setUpstream: true, githubToken, ct);
            if (!pushResult.Success)
                return Problem.GitOperationFailed("Push", pushResult.Error ?? "Unknown error", context);

            if (request.CreatePullRequest)
            {
                var prTitle = request.PrTitle ?? story.Title;
                var prBody = request.PrBody ?? BuildPrBody(story);

                var prResult = await gitService.CreatePullRequestAsync(
                    story.WorktreePath,
                    prTitle,
                    prBody,
                    request.BaseBranch,
                    request.Draft,
                    labels: ["aura-generated"],
                    githubToken,
                    ct);

                if (!prResult.Success)
                    return Problem.GitOperationFailed("PR Creation", prResult.Error ?? "Unknown error", context);

                prUrl = prResult.Value?.Url;
                prNumber = prResult.Value?.Number;
            }

            if (story.Status != StoryStatus.Completed)
            {
                await storyService.CompleteAsync(id, githubToken, ct);
            }

            return Results.Ok(new
            {
                storyId = story.Id,
                commitSha,
                pushed = true,
                prNumber,
                prUrl,
                message = prUrl is not null
                    ? $"story finalized. PR created: {prUrl}"
                    : "story finalized and pushed."
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> ChatWithStory(
        Guid id,
        StoryChatRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var response = await storyService.ChatAsync(id, request.Message, ct);
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> AddStep(
        Guid id,
        AddStepRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.AddStepAsync(
                id,
                request.Name,
                request.Capability,
                request.Description,
                input: null,
                request.AfterOrder,
                ct);

            return Results.Created($"/api/developer/stories/{id}/steps/{step.Id}", new
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> DeleteStep(
        Guid storyId,
        Guid stepId,
        IStoryService storyService,
        CancellationToken ct)
    {
        await storyService.RemoveStepAsync(storyId, stepId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ExecuteStep(
        Guid storyId,
        Guid stepId,
        ExecuteStepRequest? request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.ExecuteStepAsync(storyId, stepId, request?.AgentId, ct);
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ExecuteAllSteps(
        Guid id,
        ExecuteAllStepsRequest? request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var result = await storyService.ExecuteAllStepsAsync(
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ApproveStep(
        Guid storyId,
        Guid stepId,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.ApproveStepAsync(storyId, stepId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                approval = step.Approval?.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> RejectStep(
        Guid storyId,
        Guid stepId,
        RejectStepRequest? request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.RejectStepAsync(storyId, stepId, request?.Feedback, ct);
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> SkipStep(
        Guid storyId,
        Guid stepId,
        SkipStepRequest? request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.SkipStepAsync(storyId, stepId, request?.Reason, ct);
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
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ResetStep(
        Guid storyId,
        Guid stepId,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.ResetStepAsync(storyId, stepId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                status = step.Status.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ChatWithStep(
        Guid storyId,
        Guid stepId,
        StepChatRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var (step, response) = await storyService.ChatWithStepAsync(storyId, stepId, request.Message, ct);
            return Results.Ok(new
            {
                stepId = step.Id,
                response,
                updatedDescription = step.Description
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem.InvalidState(ex.Message, context);
        }
    }

    private static async Task<IResult> ReassignStep(
        Guid storyId,
        Guid stepId,
        ReassignStepRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.ReassignStepAsync(storyId, stepId, request.AgentId, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                agentId = step.AssignedAgentId,
                needsRework = step.NeedsRework
            });
        }
        catch (KeyNotFoundException)
        {
            return Problem.NotFound("Step", stepId, context);
        }
        catch (ArgumentException ex)
        {
            return Problem.BadRequest(ex.Message, context);
        }
    }

    private static async Task<IResult> UpdateStepDescription(
        Guid storyId,
        Guid stepId,
        UpdateStepDescriptionRequest request,
        HttpContext context,
        IStoryService storyService,
        CancellationToken ct)
    {
        try
        {
            var step = await storyService.UpdateStepDescriptionAsync(storyId, stepId, request.Description, ct);
            return Results.Ok(new
            {
                id = step.Id,
                name = step.Name,
                description = step.Description,
                needsRework = step.NeedsRework
            });
        }
        catch (KeyNotFoundException)
        {
            return Problem.NotFound("Step", stepId, context);
        }
    }

    private static string BuildPrBody(Story story)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {story.Title}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(story.Description))
        {
            sb.AppendLine(story.Description);
            sb.AppendLine();
        }
        sb.AppendLine("### story Steps");
        sb.AppendLine();
        foreach (var step in story.Steps.OrderBy(s => s.Order))
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
        HttpContext context,
        IGitHubService gitHub,
        IStoryService storyService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IssueUrl))
        {
            return Problem.MissingRequiredField("IssueUrl", null, context);
        }

        // Parse issue URL
        var parsed = gitHub.ParseIssueUrl(request.IssueUrl);
        if (parsed is null)
        {
            return Problem.BadRequest("Invalid GitHub issue URL. Expected format: https://github.com/owner/repo/issues/123", context);
        }

        if (!gitHub.IsConfigured)
        {
            return Problem.GitHubNotConfigured(context);
        }

        try
        {
            // Fetch issue from GitHub
            var issue = await gitHub.GetIssueAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, ct);

            // Create story/story
            var story = await storyService.CreateAsync(
                issue.Title,
                issue.Body,
                request.RepositoryPath,
                AutomationMode.Assisted, // Issue-based workflows default to assisted mode
                request.IssueUrl,
                DispatchTarget.CopilotCli,
                ct);

            // Post a comment to the issue that work has started
            var branch = story.GitBranch ?? "unknown";
            await gitHub.PostCommentAsync(
                parsed.Value.Owner,
                parsed.Value.Repo,
                parsed.Value.Number,
                $"Started work in branch `{branch}`",
                ct);

            return Results.Created($"/api/developer/stories/{story.Id}", new
            {
                id = story.Id,
                title = story.Title,
                description = story.Description,
                status = story.Status.ToString(),
                gitBranch = story.GitBranch,
                worktreePath = story.WorktreePath,
                repositoryPath = story.RepositoryPath,
                issueUrl = story.IssueUrl,
                issueProvider = story.IssueProvider?.ToString(),
                issueNumber = story.IssueNumber,
                issueOwner = story.IssueOwner,
                issueRepo = story.IssueRepo,
                createdAt = story.CreatedAt
            });
        }
        catch (HttpRequestException ex)
        {
            return Problem.GitHubError($"Failed to fetch issue from GitHub: {ex.Message}", context);
        }
    }

    private static async Task<IResult> RefreshFromIssue(
        Guid id,
        HttpContext context,
        IGitHubService gitHub,
        IStoryService storyService,
        CancellationToken ct)
    {
        var story = await storyService.GetByIdAsync(id, ct);
        if (story is null)
        {
            return Problem.StoryNotFound(id, context);
        }

        if (string.IsNullOrEmpty(story.IssueUrl) ||
            story.IssueOwner is null ||
            story.IssueRepo is null ||
            story.IssueNumber is null)
        {
            return Problem.NotLinkedToIssue(context);
        }

        if (!gitHub.IsConfigured)
        {
            return Problem.GitHubNotConfigured(context);
        }

        try
        {
            var issue = await gitHub.GetIssueAsync(
                story.IssueOwner,
                story.IssueRepo,
                story.IssueNumber.Value,
                ct);

            var changes = new List<string>();

            // Update title if changed
            if (story.Title != issue.Title)
            {
                story.Title = issue.Title;
                changes.Add("title");
            }

            // Update description if changed
            if (story.Description != issue.Body)
            {
                story.Description = issue.Body;
                changes.Add("description");
            }

            if (changes.Count > 0)
            {
                story.UpdatedAt = DateTimeOffset.UtcNow;
                await storyService.UpdateAsync(story, ct);
            }

            return Results.Ok(new
            {
                updated = changes.Count > 0,
                changes
            });
        }
        catch (HttpRequestException ex)
        {
            return Problem.GitHubError($"Failed to fetch issue: {ex.Message}", context);
        }
    }

    private static async Task<IResult> PostUpdateToIssue(
        Guid id,
        PostUpdateRequest request,
        HttpContext context,
        IGitHubService gitHub,
        IStoryService storyService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Problem.MissingRequiredField("Message", null, context);
        }

        var story = await storyService.GetByIdAsync(id, ct);
        if (story is null)
        {
            return Problem.StoryNotFound(id, context);
        }

        if (story.IssueOwner is null ||
            story.IssueRepo is null ||
            story.IssueNumber is null)
        {
            return Problem.NotLinkedToIssue(context);
        }

        if (!gitHub.IsConfigured)
        {
            return Problem.GitHubNotConfigured(context);
        }

        try
        {
            await gitHub.PostCommentAsync(
                story.IssueOwner,
                story.IssueRepo,
                story.IssueNumber.Value,
                request.Message,
                ct);

            return Results.Ok(new { posted = true });
        }
        catch (HttpRequestException ex)
        {
            return Problem.GitHubError($"Failed to post comment: {ex.Message}", context);
        }
    }

    private static async Task<IResult> CloseLinkedIssue(
        Guid id,
        CloseIssueRequest? request,
        HttpContext context,
        IGitHubService gitHub,
        IStoryService storyService,
        CancellationToken ct)
    {
        var story = await storyService.GetByIdAsync(id, ct);
        if (story is null)
        {
            return Problem.StoryNotFound(id, context);
        }

        if (story.IssueOwner is null ||
            story.IssueRepo is null ||
            story.IssueNumber is null)
        {
            return Problem.NotLinkedToIssue(context);
        }

        if (!gitHub.IsConfigured)
        {
            return Problem.GitHubNotConfigured(context);
        }

        try
        {
            // Post closing comment if provided
            if (!string.IsNullOrEmpty(request?.Comment))
            {
                await gitHub.PostCommentAsync(
                    story.IssueOwner,
                    story.IssueRepo,
                    story.IssueNumber.Value,
                    request.Comment,
                    ct);
            }

            // Close the issue
            await gitHub.CloseIssueAsync(
                story.IssueOwner,
                story.IssueRepo,
                story.IssueNumber.Value,
                ct);

            return Results.Ok(new { closed = true });
        }
        catch (HttpRequestException ex)
        {
            return Problem.GitHubError($"Failed to close issue: {ex.Message}", context);
        }
    }

    /// <summary>
    /// Gets the effective status of a story.
    /// If decomposed (has tasks), returns OrchestratorStatus; otherwise returns Story.Status.
    /// Also maps new StoryStatus values (GatePending, GateFailed) to meaningful strings.
    /// </summary>
    private static string GetEffectiveStatus(Story story)
    {
        // Just use the story status directly
        return story.Status.ToString();
    }

    /// <summary>
    /// Gets the wave count from steps.
    /// </summary>
    private static int GetWaveCount(Story story)
    {
        if (story.Steps.Count > 0)
        {
            return story.Steps.Max(s => s.Wave);
        }

        return 0;
    }
}
