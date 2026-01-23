// <copyright file="ConversationEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Api.Problems;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Conversation endpoints for managing chat conversations.
/// </summary>
public static class ConversationEndpoints
{
    /// <summary>
    /// Maps all conversation endpoints to the application.
    /// </summary>
    public static WebApplication MapConversationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/conversations", ListConversations);
        app.MapGet("/api/conversations/{id:guid}", GetConversation);
        app.MapPost("/api/conversations", CreateConversation);
        app.MapPost("/api/conversations/{id:guid}/messages", AddMessage);
        app.MapGet("/api/executions", ListExecutions);

        return app;
    }

    private static async Task<IResult> ListConversations(AuraDbContext db, int? limit)
    {
        var query = db.Conversations
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit ?? 50);

        var conversations = await query.Select(c => new
        {
            id = c.Id,
            title = c.Title,
            agentId = c.AgentId,
            messageCount = c.Messages.Count,
            createdAt = c.CreatedAt,
            updatedAt = c.UpdatedAt
        }).ToListAsync();

        return Results.Ok(conversations);
    }

    private static async Task<IResult> GetConversation(Guid id, HttpContext context, AuraDbContext db)
    {
        var conversation = await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conversation is null)
        {
            return Problem.ConversationNotFound(id, context);
        }

        return Results.Ok(new
        {
            id = conversation.Id,
            title = conversation.Title,
            agentId = conversation.AgentId,
            workspacePath = conversation.RepositoryPath,
            createdAt = conversation.CreatedAt,
            updatedAt = conversation.UpdatedAt,
            messages = conversation.Messages.Select(m => new
            {
                id = m.Id,
                role = m.Role.ToString().ToLowerInvariant(),
                content = m.Content,
                model = m.Model,
                tokensUsed = m.TokensUsed,
                createdAt = m.CreatedAt
            })
        });
    }

    private static async Task<IResult> CreateConversation(CreateConversationRequest request, AuraDbContext db)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? "New Conversation",
            AgentId = request.AgentId,
            RepositoryPath = request.WorkspacePath,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        return Results.Created("/api/conversations/" + conversation.Id, new
        {
            id = conversation.Id,
            title = conversation.Title,
            agentId = conversation.AgentId
        });
    }

    private static async Task<IResult> AddMessage(
        Guid id,
        AddMessageRequest request,
        HttpContext httpContext,
        AuraDbContext db,
        IAgentRegistry registry,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.FindAsync([id], cancellationToken);
        if (conversation is null)
        {
            return Problem.ConversationNotFound(id, httpContext);
        }

        var userMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = id,
            Role = MessageRole.User,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Messages.Add(userMessage);

        var agent = registry.GetAgent(conversation.AgentId);
        if (agent is null)
        {
            return Problem.AgentNotFound(conversation.AgentId, httpContext);
        }

        var agentContext = new AgentContext(
            Prompt: request.Content,
            WorkspacePath: conversation.RepositoryPath);

        try
        {
            var output = await agent.ExecuteAsync(agentContext, cancellationToken);

            var assistantMessage = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = id,
                Role = MessageRole.Assistant,
                Content = output.Content,
                Model = agent.Metadata.Model,
                TokensUsed = output.TokensUsed,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Messages.Add(assistantMessage);

            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                userMessage = new { id = userMessage.Id, role = "user", content = userMessage.Content },
                assistantMessage = new
                {
                    id = assistantMessage.Id,
                    role = "assistant",
                    content = assistantMessage.Content,
                    model = assistantMessage.Model,
                    tokensUsed = assistantMessage.TokensUsed
                }
            });
        }
        catch (AgentException ex)
        {
            return Problem.LlmProviderError($"{ex.Message} ({ex.Code})", httpContext);
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499);
        }
    }

    private static async Task<IResult> ListExecutions(AuraDbContext db, int? limit, bool? failedOnly)
    {
        var query = db.AgentExecutions.AsQueryable();

        if (failedOnly == true)
        {
            query = query.Where(e => !e.Success);
        }

        var executions = await query
            .OrderByDescending(e => e.StartedAt)
            .Take(limit ?? 50)
            .Select(e => new
            {
                id = e.Id,
                agentId = e.AgentId,
                success = e.Success,
                durationMs = e.DurationMs,
                tokensUsed = e.TokensUsed,
                startedAt = e.StartedAt,
                errorMessage = e.ErrorMessage
            })
            .ToListAsync();

        return Results.Ok(executions);
    }
}
