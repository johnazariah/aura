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
}
