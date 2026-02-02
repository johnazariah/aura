// <copyright file="SemanticScholarFetcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Fetchers;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aura.Module.Researcher.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Fetches papers from Semantic Scholar.
/// </summary>
public class SemanticScholarFetcher : ISourceFetcher
{
    private const string BaseUrl = "https://api.semanticscholar.org/graph/v1";

    private readonly HttpClient httpClient;
    private readonly ResearcherModuleOptions options;
    private readonly ILogger<SemanticScholarFetcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticScholarFetcher"/> class.
    /// </summary>
    public SemanticScholarFetcher(
        HttpClient httpClient,
        IOptions<ResearcherModuleOptions> options,
        ILogger<SemanticScholarFetcher> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;

        // Add API key if available
        if (!string.IsNullOrEmpty(this.options.SemanticScholarApiKey))
        {
            this.httpClient.DefaultRequestHeaders.Add("x-api-key", this.options.SemanticScholarApiKey);
        }
    }

    /// <inheritdoc/>
    public string Name => "Semantic Scholar";

    /// <inheritdoc/>
    public bool CanHandle(string urlOrId)
    {
        return urlOrId.Contains("semanticscholar.org", StringComparison.OrdinalIgnoreCase) ||
               urlOrId.StartsWith("10.", StringComparison.Ordinal); // DOI
    }

    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(
        string urlOrId,
        bool downloadPdf = true,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Fetching from Semantic Scholar: {Id}", urlOrId);

        try
        {
            // Extract paper ID or use DOI
            var paperId = this.ExtractPaperId(urlOrId);
            var fields = "paperId,title,abstract,authors,year,citationCount,externalIds,openAccessPdf,venue";
            var url = $"{BaseUrl}/paper/{paperId}?fields={fields}";

            var paper = await this.httpClient.GetFromJsonAsync<S2Paper>(url, cancellationToken);
            if (paper == null)
            {
                return new FetchResult
                {
                    Source = new Source { Title = "Unknown" },
                    Success = false,
                    Error = "Paper not found",
                };
            }

            var source = this.ToSource(paper);

            // Download PDF if available and requested
            string? pdfPath = null;
            if (downloadPdf && paper.OpenAccessPdf?.Url != null)
            {
                pdfPath = await this.DownloadPdfAsync(paper, cancellationToken);
                source.PdfPath = pdfPath;
            }

            return new FetchResult
            {
                Source = source,
                PdfPath = pdfPath,
                Success = true,
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new FetchResult
            {
                Source = new Source { Title = "Unknown" },
                Success = false,
                Error = "Paper not found",
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to fetch from Semantic Scholar: {Id}", urlOrId);
            return new FetchResult
            {
                Source = new Source { Title = "Unknown" },
                Success = false,
                Error = ex.Message,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Searching Semantic Scholar for: {Query}", query);

        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var fields = "paperId,title,abstract,authors,year,citationCount,externalIds,openAccessPdf";
            var url = $"{BaseUrl}/paper/search?query={encodedQuery}&limit={limit}&fields={fields}";

            var response = await this.httpClient.GetFromJsonAsync<S2SearchResponse>(url, cancellationToken);
            if (response?.Data == null)
            {
                return [];
            }

            return response.Data.Select(p => new SearchResult
            {
                Title = p.Title ?? "Unknown",
                Authors = p.Authors?.Select(a => a.Name ?? string.Empty).ToArray() ?? [],
                Abstract = p.Abstract,
                Url = $"https://www.semanticscholar.org/paper/{p.PaperId}",
                ArxivId = p.ExternalIds?.ArXiv,
                Doi = p.ExternalIds?.Doi,
                Year = p.Year,
                CitationCount = p.CitationCount,
                Source = this.Name,
            }).ToList();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to search Semantic Scholar");
            return [];
        }
    }

    private string ExtractPaperId(string urlOrId)
    {
        // Handle DOI
        if (urlOrId.StartsWith("10.", StringComparison.Ordinal))
        {
            return $"DOI:{urlOrId}";
        }

        // Handle arXiv ID
        if (urlOrId.Contains("arxiv", StringComparison.OrdinalIgnoreCase))
        {
            var arxivId = urlOrId.Split('/').Last().Replace(".pdf", string.Empty);
            return $"ARXIV:{arxivId}";
        }

        // Handle S2 URL
        if (urlOrId.Contains("semanticscholar.org"))
        {
            var parts = urlOrId.Split('/');
            return parts.Last();
        }

        return urlOrId;
    }

    private Source ToSource(S2Paper paper)
    {
        return new Source
        {
            Title = paper.Title ?? "Unknown",
            Authors = paper.Authors?.Select(a => a.Name ?? string.Empty).ToArray() ?? [],
            Abstract = paper.Abstract,
            Doi = paper.ExternalIds?.Doi,
            ArxivId = paper.ExternalIds?.ArXiv,
            PublishedDate = paper.Year.HasValue ? new DateTime(paper.Year.Value, 1, 1) : null,
            CitationCount = paper.CitationCount,
            Venue = paper.Venue,
            Url = $"https://www.semanticscholar.org/paper/{paper.PaperId}",
            SourceType = SourceType.Paper,
        };
    }

    private async Task<string?> DownloadPdfAsync(S2Paper paper, CancellationToken cancellationToken)
    {
        if (paper.OpenAccessPdf?.Url == null)
        {
            return null;
        }

        var safeId = paper.PaperId ?? Guid.NewGuid().ToString();
        var targetDir = Path.Combine(this.options.PapersPath, safeId);
        var targetPath = Path.Combine(targetDir, "original.pdf");

        if (File.Exists(targetPath))
        {
            return targetPath;
        }

        try
        {
            Directory.CreateDirectory(targetDir);

            this.logger.LogInformation("Downloading PDF: {Url}", paper.OpenAccessPdf.Url);
            var pdfBytes = await this.httpClient.GetByteArrayAsync(paper.OpenAccessPdf.Url, cancellationToken);
            await File.WriteAllBytesAsync(targetPath, pdfBytes, cancellationToken);

            return targetPath;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to download PDF");
            return null;
        }
    }

    // DTOs for Semantic Scholar API
    private sealed record S2SearchResponse(
        [property: JsonPropertyName("data")] List<S2Paper>? Data);

    private sealed record S2Paper(
        [property: JsonPropertyName("paperId")] string? PaperId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("abstract")] string? Abstract,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("citationCount")] int? CitationCount,
        [property: JsonPropertyName("venue")] string? Venue,
        [property: JsonPropertyName("authors")] List<S2Author>? Authors,
        [property: JsonPropertyName("externalIds")] S2ExternalIds? ExternalIds,
        [property: JsonPropertyName("openAccessPdf")] S2OpenAccessPdf? OpenAccessPdf);

    private sealed record S2Author(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record S2ExternalIds(
        [property: JsonPropertyName("DOI")] string? Doi,
        [property: JsonPropertyName("ArXiv")] string? ArXiv);

    private sealed record S2OpenAccessPdf(
        [property: JsonPropertyName("url")] string? Url);
}
