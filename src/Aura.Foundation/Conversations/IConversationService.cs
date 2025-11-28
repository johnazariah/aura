// <copyright file="IConversationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Conversations;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;

/// <summary>
/// Service for managing conversations with RAG context persistence.
/// Enables conversation memory across sessions.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    Task<Conversation> CreateAsync(
        string title,
        string agentId,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a conversation by ID.
    /// </summary>
    Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversations for an agent.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetByAgentAsync(
        string agentId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent conversations.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a message to a conversation.
    /// </summary>
    Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        string? model = null,
        int? tokensUsed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a message with RAG context to a conversation.
    /// Stores both the message and the RAG results that were used.
    /// </summary>
    Task<Message> AddMessageWithRagAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        string query,
        IReadOnlyList<RagResult> ragResults,
        string? model = null,
        int? tokensUsed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the RAG context that was used for a message.
    /// </summary>
    Task<IReadOnlyList<MessageRagContext>> GetMessageRagContextAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages in a conversation with their RAG context.
    /// </summary>
    Task<IReadOnlyList<(Message Message, IReadOnlyList<MessageRagContext> RagContext)>> GetConversationHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates conversation title.
    /// </summary>
    Task UpdateTitleAsync(
        Guid id,
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation and all its messages.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
