// <copyright file="Message.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents a message in a conversation.
/// This is a foundation-level entity used by any Aura application.
/// </summary>
public sealed class Message
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the conversation this message belongs to.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Gets or sets the role (user, assistant, system).</summary>
    public required MessageRole Role { get; set; }

    /// <summary>Gets or sets the message content.</summary>
    public required string Content { get; set; }

    /// <summary>Gets or sets the model used (for assistant messages).</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets tokens used (for assistant messages).</summary>
    public int? TokensUsed { get; set; }

    /// <summary>Gets or sets when the message was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the navigation property to conversation.</summary>
    public Conversation? Conversation { get; set; }
}

/// <summary>
/// The role of a message sender.
/// </summary>
public enum MessageRole
{
    /// <summary>System message (instructions).</summary>
    System,

    /// <summary>User message (human input).</summary>
    User,

    /// <summary>Assistant message (AI response).</summary>
    Assistant,
}
