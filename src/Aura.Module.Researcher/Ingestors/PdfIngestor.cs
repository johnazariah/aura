// <copyright file="PdfIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Ingestors;

using System.Text;
using Aura.Foundation.Rag;
using Aura.Foundation.Rag.Ingestors;
using Aura.Module.Researcher.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Ingestor for PDF files. Extracts text via pdftotext (poppler-utils)
/// and chunks it for RAG indexing.
/// </summary>
public sealed class PdfIngestor : IContentIngestor
{
    private readonly IPdfExtractor _pdfExtractor;
    private readonly ILogger<PdfIngestor> _logger;
    private readonly int _chunkSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfIngestor"/> class.
    /// </summary>
    public PdfIngestor(
        IPdfExtractor pdfExtractor,
        ILogger<PdfIngestor> logger,
        int chunkSize = 2000)
    {
        _pdfExtractor = pdfExtractor;
        _logger = logger;
        _chunkSize = chunkSize;
    }

    /// <inheritdoc/>
    public string IngestorId => "pdf";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".pdf"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.Pdf;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        return Path.GetExtension(filePath)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        // PDFs are binary — ignore the content parameter and extract text via pdftotext
        if (!await _pdfExtractor.IsPdfToTextAvailableAsync())
        {
            _logger.LogWarning("pdftotext not available, cannot ingest PDF: {FilePath}", filePath);
            return [];
        }

        RawPdfContent rawContent;
        try
        {
            rawContent = await _pdfExtractor.ExtractRawAsync(filePath, preserveLayout: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from PDF: {FilePath}", filePath);
            return [];
        }

        if (string.IsNullOrWhiteSpace(rawContent.Text))
        {
            _logger.LogDebug("PDF has no extractable text: {FilePath}", filePath);
            return [];
        }

        var title = rawContent.Metadata.TryGetValue("Title", out var pdfTitle)
            ? pdfTitle
            : Path.GetFileNameWithoutExtension(filePath);

        var author = rawContent.Metadata.TryGetValue("Author", out var pdfAuthor)
            ? pdfAuthor
            : null;

        var metadata = new Dictionary<string, string>
        {
            ["pageCount"] = rawContent.PageCount.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(author))
        {
            metadata["author"] = author;
        }

        // Chunk the extracted text by paragraphs
        var chunks = ChunkText(rawContent.Text, filePath, title, metadata);

        _logger.LogInformation(
            "Ingested PDF {FilePath}: {PageCount} pages, {ChunkCount} chunks",
            filePath, rawContent.PageCount, chunks.Count);

        return chunks;
    }

    private List<IngestedChunk> ChunkText(
        string text,
        string filePath,
        string title,
        Dictionary<string, string> metadata)
    {
        var chunks = new List<IngestedChunk>();
        var paragraphs = text.Split(
            ["\n\n", "\r\n\r\n"],
            StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        int chunkCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (currentChunk.Length + trimmed.Length > _chunkSize && currentChunk.Length > 0)
            {
                chunkCount++;
                chunks.Add(CreateChunk(currentChunk, chunkCount, filePath, title, metadata));
                currentChunk.Clear();
            }

            currentChunk.Append(trimmed);
            currentChunk.Append("\n\n");
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunkCount++;
            chunks.Add(CreateChunk(currentChunk, chunkCount, filePath, title, metadata));
        }

        return chunks;
    }

    private static IngestedChunk CreateChunk(
        StringBuilder chunkText,
        int chunkIndex,
        string filePath,
        string title,
        Dictionary<string, string> metadata)
    {
        var chunkType = chunkIndex == 1 ? "document" : "section";
        var chunkTitle = chunkIndex == 1
            ? title
            : $"{title} (part {chunkIndex})";

        return new IngestedChunk(chunkText.ToString().Trim(), chunkType)
        {
            Title = chunkTitle,
            Metadata = new Dictionary<string, string>(metadata)
            {
                ["chunkIndex"] = chunkIndex.ToString(),
                ["sourceFile"] = Path.GetFileName(filePath),
            },
        };
    }
}
