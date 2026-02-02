// <copyright file="WebPageFetcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Fetchers;

using System.Net.Http;
using System.Text.RegularExpressions;
using Aura.Module.Researcher.Data.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fetches articles from general web pages.
/// </summary>
public partial class WebPageFetcher : ISourceFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger<WebPageFetcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPageFetcher"/> class.
    /// </summary>
    public WebPageFetcher(
        HttpClient httpClient,
        ILogger<WebPageFetcher> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "Web";

    /// <inheritdoc/>
    public bool CanHandle(string urlOrId)
    {
        return Uri.TryCreate(urlOrId, UriKind.Absolute, out var uri) &&
               (uri.Scheme == "http" || uri.Scheme == "https");
    }

    /// <inheritdoc/>
    public async Task<FetchResult> FetchAsync(
        string urlOrId,
        bool downloadPdf = true,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(urlOrId, UriKind.Absolute, out var uri))
        {
            return new FetchResult
            {
                Source = new Source { Title = "Unknown" },
                Success = false,
                Error = "Invalid URL",
            };
        }

        this.logger.LogInformation("Fetching web page: {Url}", urlOrId);

        try
        {
            var html = await this.httpClient.GetStringAsync(uri, cancellationToken);

            var title = this.ExtractTitle(html) ?? uri.Host;
            var content = this.ExtractMainContent(html);
            var author = this.ExtractAuthor(html);

            var source = new Source
            {
                Title = title,
                Authors = author != null ? [author] : [],
                Abstract = content?.Length > 500 ? content[..500] + "..." : content,
                Url = urlOrId,
                SourceType = SourceType.Article,
            };

            return new FetchResult
            {
                Source = source,
                Success = true,
            };
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to fetch web page: {Url}", urlOrId);
            return new FetchResult
            {
                Source = new Source { Title = "Unknown" },
                Success = false,
                Error = ex.Message,
            };
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // Web fetcher doesn't support search
        return Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    private string? ExtractTitle(string html)
    {
        // Try <title> tag
        var titleMatch = TitlePattern().Match(html);
        if (titleMatch.Success)
        {
            return System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
        }

        // Try og:title
        var ogMatch = OgTitlePattern().Match(html);
        if (ogMatch.Success)
        {
            return System.Net.WebUtility.HtmlDecode(ogMatch.Groups[1].Value.Trim());
        }

        // Try <h1>
        var h1Match = H1Pattern().Match(html);
        if (h1Match.Success)
        {
            return System.Net.WebUtility.HtmlDecode(StripHtml(h1Match.Groups[1].Value).Trim());
        }

        return null;
    }

    private string? ExtractAuthor(string html)
    {
        // Try author meta tag
        var authorMatch = AuthorPattern().Match(html);
        if (authorMatch.Success)
        {
            return System.Net.WebUtility.HtmlDecode(authorMatch.Groups[1].Value.Trim());
        }

        return null;
    }

    private string? ExtractMainContent(string html)
    {
        // Simple content extraction - strip HTML and take text
        // A proper implementation would use a readability algorithm

        // Remove script and style tags
        var cleaned = ScriptStylePattern().Replace(html, string.Empty);

        // Strip HTML tags
        var text = StripHtml(cleaned);

        // Collapse whitespace
        text = WhitespacePattern().Replace(text, " ");

        return text.Trim();
    }

    private static string StripHtml(string html)
    {
        return HtmlTagPattern().Replace(html, string.Empty);
    }

    [GeneratedRegex(@"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"<meta[^>]+property=[""']og:title[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    [GeneratedRegex(@"<h1[^>]*>(.+?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Pattern();

    [GeneratedRegex(@"<meta[^>]+name=[""']author[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorPattern();

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStylePattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
