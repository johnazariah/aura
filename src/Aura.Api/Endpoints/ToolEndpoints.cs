// <copyright file="ToolEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool-related endpoints for listing and executing tools.
/// </summary>
public static class ToolEndpoints
{
    /// <summary>
    /// Maps all tool endpoints to the application.
    /// </summary>
    public static WebApplication MapToolEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tools", ListTools);
        app.MapPost("/api/tools/{toolId}/execute", ExecuteTool);
        app.MapPost("/api/tools/react", ExecuteReAct);

        return app;
    }

    private static IResult ListTools(IToolRegistry toolRegistry)
    {
        var tools = toolRegistry.GetAllTools();
        return Results.Ok(new
        {
            count = tools.Count,
            tools = tools.Select(t => new
            {
                toolId = t.ToolId,
                name = t.Name,
                description = t.Description,
                categories = t.Categories,
                requiresConfirmation = t.RequiresConfirmation,
                inputSchema = t.InputSchema
            })
        });
    }

    private static async Task<IResult> ExecuteTool(
        string toolId,
        ExecuteToolRequest request,
        IToolRegistry toolRegistry,
        CancellationToken ct)
    {
        var input = new ToolInput
        {
            ToolId = toolId,
            WorkingDirectory = request.WorkingDirectory,
            Parameters = request.Parameters ?? new Dictionary<string, object?>()
        };

        var result = await toolRegistry.ExecuteAsync(input, ct);

        if (result.Success)
        {
            return Results.Ok(new
            {
                success = true,
                output = result.Output,
                duration = result.Duration.TotalMilliseconds
            });
        }

        return Results.BadRequest(new
        {
            success = false,
            error = result.Error,
            duration = result.Duration.TotalMilliseconds
        });
    }

    private static async Task<IResult> ExecuteReAct(
        ReActExecuteRequest request,
        IToolRegistry toolRegistry,
        IReActExecutor reactExecutor,
        ILlmProviderRegistry llmRegistry,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        logger.LogInformation("ReAct execution starting: {Task}", request.Task);

        // Get the LLM provider - use request's provider or fall back to configured default
        var llm = request.Provider is not null
            ? llmRegistry.GetProvider(request.Provider)
            : llmRegistry.GetDefaultProvider();

        if (llm is null)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = $"No LLM provider available. Requested: {request.Provider ?? "(default)"}"
            });
        }

        // Get available tools
        var tools = request.ToolIds is not null && request.ToolIds.Count > 0
            ? toolRegistry.GetAllTools().Where(t => request.ToolIds.Contains(t.ToolId)).ToList()
            : toolRegistry.GetAllTools();

        if (tools.Count == 0)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = "No tools available for execution"
            });
        }

        var options = new ReActOptions
        {
            MaxSteps = request.MaxSteps ?? 10,
            Model = request.Model, // null = use provider's default from config
            Temperature = request.Temperature ?? 0.2,
            WorkingDirectory = request.WorkingDirectory,
            AdditionalContext = request.Context,
            RequireConfirmation = false, // API mode doesn't support confirmation
        };

        try
        {
            var result = await reactExecutor.ExecuteAsync(
                request.Task,
                tools,
                llm,
                options,
                ct);

            return Results.Ok(new
            {
                success = result.Success,
                finalAnswer = result.FinalAnswer,
                error = result.Error,
                totalSteps = result.Steps.Count,
                totalTokensUsed = result.TotalTokensUsed,
                durationMs = result.TotalDuration.TotalMilliseconds,
                steps = result.Steps.Select(s => new
                {
                    stepNumber = s.StepNumber,
                    thought = s.Thought,
                    action = s.Action,
                    actionInput = s.ActionInput,
                    observation = s.Observation.Length > 2000
                        ? s.Observation[..2000] + "... (truncated)"
                        : s.Observation,
                    durationMs = s.Duration.TotalMilliseconds
                })
            });
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReAct execution failed for task: {Task}", request.Task);
            return Results.BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
