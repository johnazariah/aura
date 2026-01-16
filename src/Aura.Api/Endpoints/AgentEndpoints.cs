// <copyright file="AgentEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using System.Diagnostics;
using System.Text.Json;
using Aura.Api.Contracts;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent-related endpoints for listing, executing, and chatting with agents.
/// </summary>
public static class AgentEndpoints
{
    /// <summary>
    /// Maps all agent endpoints to the application.
    /// </summary>
    public static WebApplication MapAgentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/agents", ListAgents);
        app.MapGet("/api/agents/best", GetBestAgent);
        app.MapGet("/api/agents/{agentId}", GetAgent);
        app.MapPost("/api/agents/{agentId}/execute", ExecuteAgent);
        app.MapPost("/api/agents/{agentId}/chat/stream", StreamChat);
        app.MapPost("/api/agents/{agentId}/execute/rag", ExecuteWithRag);
        app.MapPost("/api/agents/{agentId}/execute/agentic", ExecuteAgentic);

        return app;
    }

    private static object ListAgents(IAgentRegistry registry, string? capability, string? language)
    {
        IEnumerable<IAgent> agents = capability is not null
            ? registry.GetByCapability(capability, language)
            : registry.Agents.OrderBy(a => a.Metadata.Priority);

        return agents.Select(a => new
        {
            id = a.AgentId,
            name = a.Metadata.Name,
            description = a.Metadata.Description,
            capabilities = a.Metadata.Capabilities,
            priority = a.Metadata.Priority,
            languages = a.Metadata.Languages,
            provider = a.Metadata.Provider,
            model = a.Metadata.Model,
            tags = a.Metadata.Tags
        });
    }

    private static IResult GetBestAgent(IAgentRegistry registry, string capability, string? language)
    {
        var agent = registry.GetBestForCapability(capability, language);
        if (agent is null)
        {
            return Results.NotFound(new { error = "No agent found for capability '" + capability + "'" + (language is not null ? " with language '" + language + "'" : "") });
        }

        return Results.Ok(new
        {
            id = agent.AgentId,
            name = agent.Metadata.Name,
            description = agent.Metadata.Description,
            capabilities = agent.Metadata.Capabilities,
            priority = agent.Metadata.Priority,
            languages = agent.Metadata.Languages,
            provider = agent.Metadata.Provider,
            model = agent.Metadata.Model,
            tags = agent.Metadata.Tags
        });
    }

    private static IResult GetAgent(string agentId, IAgentRegistry registry)
    {
        var agent = registry.GetAgent(agentId);
        if (agent is null)
        {
            return Results.NotFound(new { error = "Agent '" + agentId + "' not found" });
        }

        return Results.Ok(new
        {
            id = agent.AgentId,
            name = agent.Metadata.Name,
            description = agent.Metadata.Description,
            capabilities = agent.Metadata.Capabilities,
            priority = agent.Metadata.Priority,
            languages = agent.Metadata.Languages,
            provider = agent.Metadata.Provider,
            model = agent.Metadata.Model,
            temperature = agent.Metadata.Temperature,
            tools = agent.Metadata.Tools,
            tags = agent.Metadata.Tags
        });
    }

    private static async Task<IResult> ExecuteAgent(
        string agentId,
        ExecuteAgentRequest request,
        IAgentRegistry registry,
        AuraDbContext db,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var agent = registry.GetAgent(agentId);
        if (agent is null)
        {
            return Results.NotFound(new { error = "Agent '" + agentId + "' not found" });
        }

        var stopwatch = Stopwatch.StartNew();
        var execution = new AgentExecution
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Prompt = request.Prompt,
            Provider = agent.Metadata.Provider,
            Model = agent.Metadata.Model,
            StartedAt = DateTimeOffset.UtcNow,
        };

        var context = new AgentContext(
            Prompt: request.Prompt,
            WorkspacePath: request.WorkspacePath);

        try
        {
            var output = await agent.ExecuteAsync(context, cancellationToken);

            stopwatch.Stop();
            execution.DurationMs = stopwatch.ElapsedMilliseconds;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.Success = true;
            execution.Response = output.Content;
            execution.TokensUsed = output.TokensUsed;

            try
            {
                db.AgentExecutions.Add(execution);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
            }

            return Results.Ok(new
            {
                content = output.Content,
                tokensUsed = output.TokensUsed,
                artifacts = output.Artifacts
            });
        }
        catch (AgentException ex)
        {
            stopwatch.Stop();
            execution.DurationMs = stopwatch.ElapsedMilliseconds;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.Success = false;
            execution.ErrorMessage = ex.Message;

            try
            {
                db.AgentExecutions.Add(execution);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
            }

            return Results.BadRequest(new
            {
                error = ex.Message,
                code = ex.Code.ToString()
            });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            execution.DurationMs = stopwatch.ElapsedMilliseconds;
            execution.CompletedAt = DateTimeOffset.UtcNow;
            execution.Success = false;
            execution.ErrorMessage = "Cancelled";

            try
            {
                db.AgentExecutions.Add(execution);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception dbEx)
            {
                logger.LogWarning(dbEx, "Failed to save execution record - database may not be initialized");
            }

            return Results.StatusCode(499);
        }
    }

    private static async Task<IResult> StreamChat(
        string agentId,
        StreamChatRequest request,
        IAgentRegistry registry,
        ILlmProviderRegistry llmRegistry,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var agent = registry.GetAgent(agentId);
        if (agent is null)
        {
            return Results.NotFound(new { error = "Agent '" + agentId + "' not found" });
        }

        // Use agent's provider, or fall back to configured default
        var provider = agent.Metadata.Provider is not null
            ? llmRegistry.GetProvider(agent.Metadata.Provider)
            : llmRegistry.GetDefaultProvider();

        if (provider is null)
        {
            return Results.BadRequest(new { error = $"No LLM provider available. Agent provider: {agent.Metadata.Provider ?? "(not set)"}" });
        }

        if (!provider.SupportsStreaming)
        {
            return Results.BadRequest(new { error = $"Provider '{provider.ProviderId}' does not support streaming" });
        }

        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var messages = new List<ChatMessage>();
        var systemPrompt = $"You are {agent.Metadata.Name}. {agent.Metadata.Description}";
        messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        if (request.History is not null)
        {
            foreach (var msg in request.History)
            {
                var role = msg.Role?.ToLowerInvariant() switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, msg.Content ?? string.Empty));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Message ?? string.Empty));

        try
        {
            await foreach (var token in provider.StreamChatAsync(
                agent.Metadata.Model,
                messages,
                agent.Metadata.Temperature,
                cancellationToken))
            {
                if (!string.IsNullOrEmpty(token.Content))
                {
                    var tokenEvent = JsonSerializer.Serialize(new { content = token.Content });
                    await httpContext.Response.WriteAsync($"event: token\ndata: {tokenEvent}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }

                if (token.IsComplete)
                {
                    var doneEvent = JsonSerializer.Serialize(new
                    {
                        totalTokens = token.TokensUsed ?? 0,
                        finishReason = token.FinishReason ?? "stop"
                    });
                    await httpContext.Response.WriteAsync($"event: done\ndata: {doneEvent}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            }

            return Results.Empty;
        }
        catch (LlmException ex)
        {
            var errorEvent = JsonSerializer.Serialize(new { message = ex.Message, code = ex.Code.ToString() });
            await httpContext.Response.WriteAsync($"event: error\ndata: {errorEvent}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            return Results.Empty;
        }
    }

    private static async Task<IResult> ExecuteWithRag(
        string agentId,
        ExecuteWithRagRequest request,
        IRagEnrichedExecutor executor,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var output = await executor.ExecuteAsync(
                agentId,
                request.Prompt,
                request.WorkspacePath,
                request.UseRag,
                request.UseCodeGraph,
                request.TopK.HasValue ? new RagQueryOptions { TopK = request.TopK.Value } : null,
                cancellationToken);

            stopwatch.Stop();

            return Results.Ok(new
            {
                content = output.Content,
                tokensUsed = output.TokensUsed,
                artifacts = output.Artifacts,
                ragEnriched = true,
                codeGraphEnriched = request.UseCodeGraph ?? true,
                durationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (AgentException ex)
        {
            return Results.BadRequest(new
            {
                error = ex.Message,
                code = ex.Code.ToString()
            });
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499);
        }
    }

    private static async Task<IResult> ExecuteAgentic(
        string agentId,
        ExecuteAgenticRequest request,
        IRagEnrichedExecutor executor,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var output = await executor.ExecuteAgenticAsync(
                agentId,
                request.Prompt,
                request.WorkspacePath,
                request.UseRag,
                request.UseCodeGraph,
                request.MaxSteps ?? 10,
                cancellationToken);

            stopwatch.Stop();

            return Results.Ok(new
            {
                content = output.Content,
                tokensUsed = output.TokensUsed,
                toolSteps = output.ToolSteps.Select(s => new
                {
                    toolId = s.ToolId,
                    input = s.Input,
                    output = s.Output,
                    success = s.Success
                }),
                stepCount = output.ToolSteps.Count,
                durationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (AgentException ex)
        {
            return Results.BadRequest(new
            {
                error = ex.Message,
                code = ex.Code.ToString()
            });
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499);
        }
    }
}
