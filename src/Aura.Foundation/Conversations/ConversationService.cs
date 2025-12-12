// <copyright file="ConversationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Conversations;

using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for managing conversations with RAG context persistence.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private readonly AuraDbContext _dbContext;
    private readonly ILogger<ConversationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationService"/> class.
    /// </summary>
    public ConversationService(AuraDbContext dbContext, ILogger<ConversationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Conversation> CreateAsync(
        string title,
        string agentId,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = title,
            AgentId = agentId,
            RepositoryPath = workspacePath,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Created conversation {Id} for agent {AgentId}", conversation.Id, agentId);
        return conversation;
    }

    /// <inheritdoc/>
    public async Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Conversation>> GetByAgentAsync(
        string agentId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .Where(c => c.AgentId == agentId)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Conversation>> GetRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Conversations
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        string? model = null,
        int? tokensUsed = null,
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Model = model,
            TokensUsed = tokensUsed,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Messages.Add(message);

        // Update conversation timestamp
        await _dbContext.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return message;
    }

    /// <inheritdoc/>
    public async Task<Message> AddMessageWithRagAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        string query,
        IReadOnlyList<RagResult> ragResults,
        string? model = null,
        int? tokensUsed = null,
        CancellationToken cancellationToken = default)
    {
        // Create the message
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Model = model,
            TokensUsed = tokensUsed,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Messages.Add(message);

        // Store RAG context for each result
        foreach (var result in ragResults)
        {
            var ragContext = new MessageRagContext
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                Query = query,
                ContentId = result.ContentId,
                ChunkIndex = result.ChunkIndex,
                ChunkContent = result.Text,
                Score = result.Score,
                SourcePath = result.SourcePath,
                ContentType = result.ContentType,
                RetrievedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.MessageRagContexts.Add(ragContext);
        }

        // Update conversation timestamp
        await _dbContext.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Added message {MessageId} with {RagCount} RAG contexts to conversation {ConversationId}",
            message.Id, ragResults.Count, conversationId);

        return message;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessageRagContext>> GetMessageRagContextAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageRagContexts
            .Where(r => r.MessageId == messageId)
            .OrderByDescending(r => r.Score)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(Message Message, IReadOnlyList<MessageRagContext> RagContext)>> GetConversationHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var messageIds = messages.Select(m => m.Id).ToList();

        var ragContexts = await _dbContext.MessageRagContexts
            .Where(r => messageIds.Contains(r.MessageId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ragContextByMessage = ragContexts
            .GroupBy(r => r.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MessageRagContext>)g.OrderByDescending(r => r.Score).ToList());

        return messages
            .Select(m => (m, ragContextByMessage.TryGetValue(m.Id, out var ctx) ? ctx : Array.Empty<MessageRagContext>()))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateTitleAsync(
        Guid id,
        string title,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Conversations
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Title, title)
                    .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Delete RAG contexts first (cascade would work too, but explicit is clearer)
        var messageIds = await _dbContext.Messages
            .Where(m => m.ConversationId == id)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (messageIds.Count > 0)
        {
            await _dbContext.MessageRagContexts
                .Where(r => messageIds.Contains(r.MessageId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // Delete messages
        await _dbContext.Messages
            .Where(m => m.ConversationId == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Delete conversation
        var deleted = await _dbContext.Conversations
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted conversation {Id}", id);
        }

        return deleted > 0;
    }
}
