// <copyright file="RagChunk.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

using Aura.Foundation.Rag;
using Pgvector;

/// <summary>
/// Entity for storing RAG chunks with vector embeddings.
/// </summary>
public sealed class RagChunk
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the content identifier (e.g., file path).
    /// </summary>
    public required string ContentId { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the content.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the chunk text content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public RagContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Gets or sets the workspace ID this chunk belongs to.
    /// This is the 16-char hex hash of the normalized workspace path.
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Gets or sets the metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets when this chunk was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
