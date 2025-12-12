// <copyright file="Conversation.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents a conversation with an AI agent.
/// This is a foundation-level entity used by any Aura application.
/// </summary>
public sealed class Conversation
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the conversation title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the agent ID used in this conversation.</summary>
    public required string AgentId { get; set; }

    /// <summary>Gets or sets the repository path for RAG context.</summary>
    public string? RepositoryPath { get; set; }

    /// <summary>Gets or sets when the conversation was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the conversation was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the messages in this conversation.</summary>
    public ICollection<Message> Messages { get; set; } = [];
}
