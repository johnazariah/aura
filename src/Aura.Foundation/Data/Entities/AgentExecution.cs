// <copyright file="AgentExecution.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Records a single agent execution for analytics and debugging.
/// This is a foundation-level entity used by any Aura application.
/// </summary>
public sealed class AgentExecution
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the agent that was executed.</summary>
    public required string AgentId { get; set; }

    /// <summary>Gets or sets the conversation this execution belongs to.</summary>
    public Guid? ConversationId { get; set; }

    /// <summary>Gets or sets the prompt sent to the agent.</summary>
    public required string Prompt { get; set; }

    /// <summary>Gets or sets the response from the agent.</summary>
    public string? Response { get; set; }

    /// <summary>Gets or sets the model used.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the provider used.</summary>
    public string? Provider { get; set; }

    /// <summary>Gets or sets tokens used.</summary>
    public int? TokensUsed { get; set; }

    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>Gets or sets whether the execution succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets error message if failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets when the execution started.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the execution completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the navigation property to conversation.</summary>
    public Conversation? Conversation { get; set; }
}
