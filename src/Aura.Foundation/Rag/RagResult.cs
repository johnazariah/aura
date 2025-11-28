// <copyright file="RagResult.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Result from a RAG query.
/// </summary>
/// <param name="ContentId">The content ID this chunk belongs to.</param>
/// <param name="ChunkIndex">The index of this chunk within the content.</param>
/// <param name="Text">The chunk text.</param>
/// <param name="Score">Relevance score (0.0 to 1.0, higher is more relevant).</param>
public sealed record RagResult(
    string ContentId,
    int ChunkIndex,
    string Text,
    double Score)
{
    /// <summary>
    /// Gets the source file path (if applicable).
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public RagContentType ContentType { get; init; }

    /// <summary>
    /// Gets additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Statistics about the RAG index.
/// </summary>
/// <param name="TotalChunks">Total number of chunks in the index.</param>
/// <param name="TotalDocuments">Total number of documents indexed.</param>
/// <param name="IndexSizeBytes">Approximate size of the index in bytes.</param>
public sealed record RagStats(
    int TotalChunks,
    int TotalDocuments,
    long IndexSizeBytes)
{
    /// <summary>
    /// Gets the breakdown by content type.
    /// </summary>
    public IReadOnlyDictionary<RagContentType, int>? ByContentType { get; init; }
}
