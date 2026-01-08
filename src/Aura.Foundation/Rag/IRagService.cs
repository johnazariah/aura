// <copyright file="IRagService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Service for indexing and querying content using RAG (Retrieval-Augmented Generation).
/// All data is stored locally - embeddings via Ollama, vectors in local PostgreSQL.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Indexes content for semantic search.
    /// Content is chunked, embedded via Ollama, and stored in pgvector.
    /// </summary>
    /// <param name="content">The content to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(RagContent content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple pieces of content.
    /// </summary>
    /// <param name="contents">The contents to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexManyAsync(IEnumerable<RagContent> contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple pieces of content with batched embedding generation.
    /// More efficient than calling IndexAsync multiple times as embeddings are generated in a single batch.
    /// </summary>
    /// <param name="contents">The contents to index (each should be a single chunk - no further splitting).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks indexed.</returns>
    Task<int> IndexBatchAsync(IReadOnlyList<RagContent> contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes all files in a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory.</param>
    /// <param name="options">Indexing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files indexed.</returns>
    Task<int> IndexDirectoryAsync(
        string directoryPath,
        RagIndexOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes content from the index.
    /// </summary>
    /// <param name="contentId">The content ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if content was found and removed.</returns>
    Task<bool> RemoveAsync(string contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the index for semantically similar content.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">Query options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked list of matching chunks.</returns>
    Task<IReadOnlyList<RagResult>> QueryAsync(
        string query,
        RagQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Index statistics.</returns>
    Task<RagStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all content from the index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the RAG service is healthy (Ollama available, DB connected).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if healthy.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about indexed content for a specific directory.
    /// </summary>
    /// <param name="directoryPath">The directory path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics for the directory, or null if not indexed.</returns>
    Task<RagDirectoryStats?> GetDirectoryStatsAsync(string directoryPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for a specific directory in the RAG index.
/// </summary>
/// <param name="DirectoryPath">The directory path.</param>
/// <param name="ChunkCount">Number of chunks indexed from this directory.</param>
/// <param name="FileCount">Number of files indexed from this directory.</param>
/// <param name="LastIndexedAt">When the directory was last indexed.</param>
public sealed record RagDirectoryStats(
    string DirectoryPath,
    int ChunkCount,
    int FileCount,
    DateTimeOffset? LastIndexedAt)
{
    /// <summary>Gets whether the directory has been indexed.</summary>
    public bool IsIndexed => ChunkCount > 0;
}
