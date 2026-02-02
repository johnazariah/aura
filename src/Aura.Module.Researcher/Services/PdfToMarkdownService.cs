// <copyright file="PdfToMarkdownService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Converts PDFs to structured Markdown using pdftotext and heuristics.
/// </summary>
public partial class PdfToMarkdownService : IPdfToMarkdownService
{
    private readonly IPdfExtractor pdfExtractor;
    private readonly ILogger<PdfToMarkdownService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfToMarkdownService"/> class.
    /// </summary>
    /// <param name="pdfExtractor">The PDF extractor.</param>
    /// <param name="logger">The logger.</param>
    public PdfToMarkdownService(
        IPdfExtractor pdfExtractor,
        ILogger<PdfToMarkdownService> logger)
    {
        this.pdfExtractor = pdfExtractor;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MarkdownDocument> ConvertAsync(
        string pdfPath,
        PdfConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PdfConversionOptions();

        // Extract raw text
        var raw = await this.pdfExtractor.ExtractRawAsync(pdfPath, preserveLayout: true, cancellationToken);

        // Parse structure
        var title = this.ExtractTitle(raw.Text, raw.Metadata);
        var authors = this.ExtractAuthors(raw.Text);
        var @abstract = this.ExtractAbstract(raw.Text);
        var sections = this.DetectSections(raw.Text);
        var citations = options.ExtractCitations ? this.ExtractCitations(raw.Text) : [];
        var figures = this.DetectFigures(raw.Text);

        // Build markdown content
        var content = this.BuildMarkdown(
            title,
            authors,
            @abstract,
            raw.Text,
            sections,
            figures,
            options);

        return new MarkdownDocument
        {
            Title = title,
            Authors = authors,
            Abstract = @abstract,
            Content = content,
            Sections = sections,
            Figures = figures,
            Citations = citations,
            Metadata = raw.Metadata,
        };
    }

    /// <inheritdoc/>
    public Task<MarkdownDocument> EnhanceAsync(
        MarkdownDocument document,
        EnhancementLevel level = EnhancementLevel.Basic,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement LLM enhancement
        // For now, return the document as-is
        this.logger.LogDebug("Enhancement level {Level} requested but not yet implemented", level);
        return Task.FromResult(document);
    }

    private string ExtractTitle(string text, Dictionary<string, string> metadata)
    {
        // Try metadata first
        if (metadata.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        // Heuristic: first non-empty line that's reasonably long
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Take(10))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 10 && trimmed.Length < 200 && !trimmed.StartsWith("arXiv", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return "Untitled";
    }

    private string[] ExtractAuthors(string text)
    {
        // Look for author patterns near the top of the document
        var lines = text.Split('\n').Take(30).ToList();
        var authorPatterns = new[]
        {
            AuthorLineWithAffiliation(),
            AuthorLineCommas(),
        };

        foreach (var line in lines)
        {
            foreach (var pattern in authorPatterns)
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    var authorLine = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                    return authorLine
                        .Split(new[] { ",", " and ", " & " }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => a.Length > 2 && a.Length < 50)
                        .ToArray();
                }
            }
        }

        return [];
    }

    private string? ExtractAbstract(string text)
    {
        var abstractMatch = AbstractPattern().Match(text);
        if (abstractMatch.Success)
        {
            var abstractText = abstractMatch.Groups[1].Value.Trim();

            // Clean up: remove line breaks within abstract, limit length
            abstractText = WhitespacePattern().Replace(abstractText, " ");
            if (abstractText.Length > 2000)
            {
                abstractText = abstractText[..2000] + "...";
            }

            return abstractText;
        }

        return null;
    }

    private List<DocumentSection> DetectSections(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentPage = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Track page breaks
            if (line.Contains('\f'))
            {
                currentPage++;
            }

            // Detect section headings
            var sectionMatch = SectionHeadingPattern().Match(line);
            if (sectionMatch.Success)
            {
                var number = sectionMatch.Groups[1].Value;
                var title = sectionMatch.Groups[2].Value.Trim();

                var level = number.Count(c => c == '.') + 1;
                if (level > 3)
                {
                    level = 3;
                }

                sections.Add(new DocumentSection(title, level, currentPage, null));
            }
        }

        return sections;
    }

    private List<DocumentCitation> ExtractCitations(string text)
    {
        var citations = new List<DocumentCitation>();

        // Find references section
        var referencesMatch = ReferencesSection().Match(text);
        if (!referencesMatch.Success)
        {
            return citations;
        }

        var referencesText = referencesMatch.Groups[1].Value;

        // Parse numbered references [1], [2], etc.
        var refMatches = NumberedReference().Matches(referencesText);
        foreach (Match match in refMatches)
        {
            var key = match.Groups[1].Value;
            var citationText = match.Groups[2].Value.Trim();

            // Try to extract DOI
            string? doi = null;
            var doiMatch = DoiPattern().Match(citationText);
            if (doiMatch.Success)
            {
                doi = doiMatch.Value;
            }

            citations.Add(new DocumentCitation(key, citationText, doi));
        }

        return citations;
    }

    private List<DocumentFigure> DetectFigures(string text)
    {
        var figures = new List<DocumentFigure>();
        var figureMatches = FigureCaptionPattern().Matches(text);

        foreach (Match match in figureMatches)
        {
            var number = match.Groups[1].Value;
            var caption = match.Groups[2].Value.Trim();

            figures.Add(new DocumentFigure(
                $"figure-{number}",
                caption,
                null,
                null));
        }

        return figures;
    }

    private string BuildMarkdown(
        string title,
        string[] authors,
        string? @abstract,
        string rawText,
        List<DocumentSection> sections,
        List<DocumentFigure> figures,
        PdfConversionOptions options)
    {
        var sb = new StringBuilder();

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{title.Replace("\"", "\\\"")}\"");
        if (authors.Length > 0)
        {
            sb.AppendLine($"authors: [{string.Join(", ", authors.Select(a => $"\"{a}\""))}]");
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // Title
        sb.AppendLine($"# {title}");
        sb.AppendLine();

        // Authors
        if (authors.Length > 0)
        {
            sb.AppendLine($"**Authors:** {string.Join(", ", authors)}");
            sb.AppendLine();
        }

        // Abstract
        if (!string.IsNullOrEmpty(@abstract))
        {
            sb.AppendLine("## Abstract");
            sb.AppendLine();
            sb.AppendLine(@abstract);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Body content with structure
        var bodyText = this.CleanBodyText(rawText);
        sb.AppendLine(bodyText);

        // Figure references
        if (figures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Figures");
            sb.AppendLine();
            foreach (var figure in figures)
            {
                sb.AppendLine($"> **{figure.Id}**: {figure.Caption}");
                sb.AppendLine("> *See original PDF*");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private string CleanBodyText(string text)
    {
        // Remove abstract section (already extracted)
        text = AbstractPattern().Replace(text, string.Empty);

        // Convert section headings to markdown
        text = SectionHeadingPattern().Replace(text, m =>
        {
            var number = m.Groups[1].Value;
            var title = m.Groups[2].Value.Trim();
            var level = Math.Min(number.Count(c => c == '.') + 2, 6);
            var hashes = new string('#', level);
            return $"\n{hashes} {number} {title}\n";
        });

        // Clean up excessive whitespace
        text = ExcessiveNewlines().Replace(text, "\n\n");

        return text.Trim();
    }

    // Regex patterns
    [GeneratedRegex(@"(?:Authors?|By)[:\s]+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorLineWithAffiliation();

    [GeneratedRegex(@"^([A-Z][a-z]+\s+[A-Z][a-z]+(?:\s*,\s*[A-Z][a-z]+\s+[A-Z][a-z]+)+)", RegexOptions.Multiline)]
    private static partial Regex AuthorLineCommas();

    [GeneratedRegex(@"(?:Abstract|ABSTRACT)\s*\n(.+?)(?=\n\s*(?:1\.?\s+Introduction|I\.?\s+Introduction|Keywords|1\s+INTRODUCTION))", RegexOptions.Singleline)]
    private static partial Regex AbstractPattern();

    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)*)\s+([A-Z][^\n]{5,80})$", RegexOptions.Multiline)]
    private static partial Regex SectionHeadingPattern();

    [GeneratedRegex(@"(?:References|REFERENCES|Bibliography)\s*\n(.+?)$", RegexOptions.Singleline)]
    private static partial Regex ReferencesSection();

    [GeneratedRegex(@"\[(\d+)\]\s*([^\[]+?)(?=\[\d+\]|$)", RegexOptions.Singleline)]
    private static partial Regex NumberedReference();

    [GeneratedRegex(@"10\.\d{4,}/[^\s]+")]
    private static partial Regex DoiPattern();

    [GeneratedRegex(@"(?:Figure|Fig\.?)\s*(\d+)[:\.]?\s*(.+?)(?=\n\n|\z)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FigureCaptionPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlines();
}
