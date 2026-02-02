// <copyright file="ArxivFetcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Fetchers;

using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aura.Module.Researcher.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Fetches papers from arXiv.
/// </summary>
public partial class ArxivFetcher : ISourceFetcher
{
    private const string ArxivApiUrl = "http://export.arxiv.org/api/query";
    private const string ArxivPdfUrl = "https://arxiv.org/pdf";
    private const string ArxivAbsUrl = "https://arxiv.org/abs";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Arxiv = "http://arxiv.org/schemas/atom";

    private readonly HttpClient httpClient;
    private readonly ResearcherModuleOptions options;
    private readonly ILogger<ArxivFetcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArxivFetcher"/> class.
    /// </summary>
    public ArxivFetcher(
        HttpClient httpClient,
        IOptions<ResearcherModuleOptions> options,
        ILogger<ArxivFetcher> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "arXiv";

    /// <inheritdoc/>
    public bool CanHandle(string urlOrId)
    {
        return ArxivIdPattern().IsMatch(urlOrId) ||
               urlOrId.Contains("arxiv.org", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(
        string urlOrId,
        bool downloadPdf = true,
        CancellationToken cancellationToken = default)
    {
        var arxivId = this.ExtractArxivId(urlOrId);
        if (string.IsNullOrEmpty(arxivId))
        {
            return new FetchResult
            {
                Source = new Source { Title = "Unknown" },
                Success = false,
                Error = $"Could not extract arXiv ID from: {urlOrId}",
            };
        }

        this.logger.LogInformation("Fetching arXiv paper: {ArxivId}", arxivId);

        try
        {
            // Fetch metadata
            var apiUrl = $"{ArxivApiUrl}?id_list={arxivId}";
            var response = await this.httpClient.GetStringAsync(apiUrl, cancellationToken);
            var doc = XDocument.Parse(response);

            var entry = doc.Descendants(Atom + "entry").FirstOrDefault();
            if (entry == null)
            {
                return new FetchResult
                {
                    Source = new Source { Title = "Unknown" },
                    Success = false,
                    Error = $"Paper not found: {arxivId}",
                };
            }

            var source = this.ParseEntry(entry, arxivId);

            // Download PDF if requested
            string? pdfPath = null;
            if (downloadPdf)
            {
                pdfPath = await this.DownloadPdfAsync(arxivId, cancellationToken);
                source.PdfPath = pdfPath;
            }

            return new FetchResult
            {
                Source = source,
                PdfPath = pdfPath,
                Success = true,
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to fetch arXiv paper: {ArxivId}", arxivId);
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
        this.logger.LogInformation("Searching arXiv for: {Query}", query);

        var encodedQuery = Uri.EscapeDataString(query);
        var apiUrl = $"{ArxivApiUrl}?search_query=all:{encodedQuery}&start=0&max_results={limit}";

        try
        {
            var response = await this.httpClient.GetStringAsync(apiUrl, cancellationToken);
            var doc = XDocument.Parse(response);

            var results = new List<SearchResult>();
            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var id = entry.Element(Atom + "id")?.Value ?? string.Empty;
                var arxivId = this.ExtractArxivId(id);

                results.Add(new SearchResult
                {
                    Title = entry.Element(Atom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Unknown",
                    Authors = entry.Elements(Atom + "author")
                        .Select(a => a.Element(Atom + "name")?.Value ?? string.Empty)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToArray(),
                    Abstract = entry.Element(Atom + "summary")?.Value?.Trim(),
                    Url = $"{ArxivAbsUrl}/{arxivId}",
                    ArxivId = arxivId,
                    Year = this.ParseYear(entry.Element(Atom + "published")?.Value),
                    Source = this.Name,
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to search arXiv");
            return [];
        }
    }

    private Source ParseEntry(XElement entry, string arxivId)
    {
        var published = entry.Element(Atom + "published")?.Value;
        DateTime? publishedDate = null;
        if (DateTime.TryParse(published, out var parsed))
        {
            publishedDate = parsed;
        }

        return new Source
        {
            Title = entry.Element(Atom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Unknown",
            Authors = entry.Elements(Atom + "author")
                .Select(a => a.Element(Atom + "name")?.Value ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray(),
            Abstract = entry.Element(Atom + "summary")?.Value?.Trim(),
            Url = $"{ArxivAbsUrl}/{arxivId}",
            ArxivId = arxivId,
            PublishedDate = publishedDate,
            SourceType = SourceType.Paper,
        };
    }

    private async Task<string?> DownloadPdfAsync(string arxivId, CancellationToken cancellationToken)
    {
        var pdfUrl = $"{ArxivPdfUrl}/{arxivId}.pdf";
        var papersDir = this.options.PapersPath;
        var safeId = arxivId.Replace("/", "_");
        var targetDir = Path.Combine(papersDir, safeId);
        var targetPath = Path.Combine(targetDir, "original.pdf");

        if (File.Exists(targetPath))
        {
            this.logger.LogDebug("PDF already cached: {Path}", targetPath);
            return targetPath;
        }

        try
        {
            Directory.CreateDirectory(targetDir);

            this.logger.LogInformation("Downloading PDF: {Url}", pdfUrl);
            var pdfBytes = await this.httpClient.GetByteArrayAsync(pdfUrl, cancellationToken);
            await File.WriteAllBytesAsync(targetPath, pdfBytes, cancellationToken);

            this.logger.LogInformation("Downloaded PDF to: {Path}", targetPath);
            return targetPath;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to download PDF: {Url}", pdfUrl);
            return null;
        }
    }

    private string? ExtractArxivId(string urlOrId)
    {
        // Handle various formats:
        // - 2301.12345
        // - 2301.12345v2
        // - arxiv:2301.12345
        // - https://arxiv.org/abs/2301.12345
        // - https://arxiv.org/pdf/2301.12345.pdf
        // - hep-th/9901001 (old format)

        var match = ArxivIdPattern().Match(urlOrId);
        return match.Success ? match.Value : null;
    }

    private int? ParseYear(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return null;
        }

        if (DateTime.TryParse(dateStr, out var date))
        {
            return date.Year;
        }

        return null;
    }

    [GeneratedRegex(@"(\d{4}\.\d{4,5}(v\d+)?|[a-z-]+/\d{7})", RegexOptions.IgnoreCase)]
    private static partial Regex ArxivIdPattern();
}
