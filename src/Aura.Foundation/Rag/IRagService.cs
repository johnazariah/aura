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

    /// <summary>
    /// Gets all code chunks for a workspace, suitable for building a hierarchical tree view.
    /// Returns chunks with metadata including symbolName, chunkType, signature, and parentSymbol.
    /// </summary>
    /// <param name="workspacePath">The workspace path to query.</param>
    /// <param name="pattern">Optional filter pattern for file paths or symbol names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All code chunks with their metadata for tree building.</returns>
    Task<IReadOnlyList<TreeChunk>> GetChunksForTreeAsync(
        string workspacePath,
        string? pattern = null,
        CancellationToken cancellationToken = default);
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

/// <summary>
/// A code chunk with metadata for building hierarchical tree views.
/// </summary>
/// <param name="SourcePath">The source file path (relative to workspace).</param>
/// <param name="ChunkType">The type of chunk (e.g., "type", "method", "function", "header").</param>
/// <param name="Content">The chunk content.</param>
public sealed record TreeChunk(
    string SourcePath,
    string ChunkType,
    string Content)
{
    /// <summary>Gets or sets the symbol name (e.g., class name, method name).</summary>
    public string? SymbolName { get; init; }

    /// <summary>Gets or sets the fully qualified name.</summary>
    public string? FullyQualifiedName { get; init; }

    /// <summary>Gets or sets the signature (e.g., "public async Task ProcessAsync(string input)").</summary>
    public string? Signature { get; init; }

    /// <summary>Gets or sets the parent symbol name (e.g., class name for a method).</summary>
    public string? ParentSymbol { get; init; }

    /// <summary>Gets or sets the language.</summary>
    public string? Language { get; init; }

    /// <summary>Gets or sets the start line number.</summary>
    public int? StartLine { get; init; }

    /// <summary>Gets or sets the end line number.</summary>
    public int? EndLine { get; init; }

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get; init; }
}
