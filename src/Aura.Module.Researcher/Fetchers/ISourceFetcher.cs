// <copyright file="ISourceFetcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Fetchers;

using Aura.Module.Researcher.Data.Entities;

/// <summary>
/// Interface for fetching sources from external services.
/// </summary>
public interface ISourceFetcher
{
    /// <summary>
    /// Gets the name of this fetcher.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines if this fetcher can handle the given URL or identifier.
    /// </summary>
    /// <param name="urlOrId">URL, DOI, or identifier.</param>
    /// <returns>True if this fetcher can handle the input.</returns>
    bool CanHandle(string urlOrId);

    /// <summary>
    /// Fetches source metadata and optionally the PDF.
    /// </summary>
    /// <param name="urlOrId">URL, DOI, or identifier.</param>
    /// <param name="downloadPdf">Whether to download the PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fetched source.</returns>
    Task<FetchResult> FetchAsync(
        string urlOrId,
        bool downloadPdf = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for papers matching the query.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="limit">Maximum results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results.</returns>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of fetching a source.
/// </summary>
public record FetchResult
{
    /// <summary>Gets the source metadata.</summary>
    public required Source Source { get; init; }

    /// <summary>Gets the path to downloaded PDF, if any.</summary>
    public string? PdfPath { get; init; }

    /// <summary>Gets whether the fetch was successful.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Gets any error message.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// A search result from an external service.
/// </summary>
public record SearchResult
{
    /// <summary>Gets the paper title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the authors.</summary>
    public string[] Authors { get; init; } = [];

    /// <summary>Gets the abstract.</summary>
    public string? Abstract { get; init; }

    /// <summary>Gets the URL to the paper.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the arXiv ID if applicable.</summary>
    public string? ArxivId { get; init; }

    /// <summary>Gets the DOI if available.</summary>
    public string? Doi { get; init; }

    /// <summary>Gets the publication year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the citation count.</summary>
    public int? CitationCount { get; init; }

    /// <summary>Gets the source service name.</summary>
    public required string Source { get; init; }
}
