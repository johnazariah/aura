// <copyright file="SourceFetcherService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Fetchers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates multiple source fetchers and routes requests to the appropriate one.
/// </summary>
public class SourceFetcherService
{
    private readonly IReadOnlyList<ISourceFetcher> fetchers;
    private readonly ILogger<SourceFetcherService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceFetcherService"/> class.
    /// </summary>
    public SourceFetcherService(
        IEnumerable<ISourceFetcher> fetchers,
        ILogger<SourceFetcherService> logger)
    {
        // Order matters: more specific fetchers first
        this.fetchers = fetchers.ToList();
        this.logger = logger;
    }

    /// <summary>
    /// Gets all available fetchers.
    /// </summary>
    public IReadOnlyList<ISourceFetcher> Fetchers => this.fetchers;

    /// <summary>
    /// Fetches a source from the appropriate service based on the URL/ID.
    /// </summary>
    public async Task<FetchResult> FetchAsync(
        string urlOrId,
        bool downloadPdf = true,
        CancellationToken cancellationToken = default)
    {
        var fetcher = this.fetchers.FirstOrDefault(f => f.CanHandle(urlOrId));
        if (fetcher == null)
        {
            this.logger.LogWarning("No fetcher found for: {UrlOrId}", urlOrId);
            return new FetchResult
            {
                Source = new Data.Entities.Source { Title = "Unknown" },
                Success = false,
                Error = "No fetcher found for this URL or identifier",
            };
        }

        this.logger.LogInformation("Using {Fetcher} for: {UrlOrId}", fetcher.Name, urlOrId);
        return await fetcher.FetchAsync(urlOrId, downloadPdf, cancellationToken);
    }

    /// <summary>
    /// Searches across all fetchers that support search.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        string[]? sources = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();
        var fetchersToUse = sources == null
            ? this.fetchers
            : this.fetchers.Where(f => sources.Contains(f.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var fetcher in fetchersToUse)
        {
            try
            {
                var fetcherResults = await fetcher.SearchAsync(query, limit, cancellationToken);
                results.AddRange(fetcherResults);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Search failed for {Fetcher}", fetcher.Name);
            }
        }

        // Sort by citation count (descending) and return limited results
        return results
            .OrderByDescending(r => r.CitationCount ?? 0)
            .Take(limit)
            .ToList();
    }
}
