// <copyright file="ILibraryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

using Aura.Module.Researcher.Data.Entities;

/// <summary>
/// Service for managing the research library.
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Gets all sources, optionally filtered.
    /// </summary>
    /// <param name="sourceType">Filter by source type.</param>
    /// <param name="status">Filter by reading status.</param>
    /// <param name="tags">Filter by tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sources.</returns>
    Task<IReadOnlyList<Source>> GetSourcesAsync(
        SourceType? sourceType = null,
        ReadingStatus? status = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a source by ID.
    /// </summary>
    /// <param name="id">The source ID.</param>
    /// <param name="includeExcerpts">Whether to include excerpts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source or null.</returns>
    Task<Source?> GetSourceAsync(
        Guid id,
        bool includeExcerpts = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new source.
    /// </summary>
    /// <param name="source">The source to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created source.</returns>
    Task<Source> CreateSourceAsync(Source source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a source.
    /// </summary>
    /// <param name="source">The source to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated source.</returns>
    Task<Source> UpdateSourceAsync(Source source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a source.
    /// </summary>
    /// <param name="id">The source ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted.</returns>
    Task<bool> DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches sources semantically.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="limit">Maximum results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching sources with relevance scores.</returns>
    Task<IReadOnlyList<(Source Source, float Score)>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an excerpt to a source.
    /// </summary>
    /// <param name="excerpt">The excerpt to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created excerpt.</returns>
    Task<Excerpt> AddExcerptAsync(Excerpt excerpt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets excerpts for a source.
    /// </summary>
    /// <param name="sourceId">The source ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of excerpts.</returns>
    Task<IReadOnlyList<Excerpt>> GetExcerptsAsync(Guid sourceId, CancellationToken cancellationToken = default);
}
