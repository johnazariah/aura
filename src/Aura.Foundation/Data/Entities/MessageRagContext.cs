// <copyright file="MessageRagContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

using Aura.Foundation.Rag;

/// <summary>
/// Stores the RAG context that was used to generate a message.
/// Enables conversation memory and context replay.
/// </summary>
public sealed class MessageRagContext
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the message this context belongs to.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Gets or sets the query that was used for RAG retrieval.</summary>
    public required string Query { get; set; }

    /// <summary>Gets or sets the content ID of the source document.</summary>
    public required string ContentId { get; set; }

    /// <summary>Gets or sets the chunk index within the document.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Gets or sets the chunk content that was used.</summary>
    public required string ChunkContent { get; set; }

    /// <summary>Gets or sets the similarity score (0-1).</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets the source file path.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Gets or sets the content type.</summary>
    public RagContentType ContentType { get; set; }

    /// <summary>Gets or sets when this context was retrieved.</summary>
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the navigation property to message.</summary>
    public Message? Message { get; set; }
}
