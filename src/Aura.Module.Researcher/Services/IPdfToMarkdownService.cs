// <copyright file="IPdfToMarkdownService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

/// <summary>
/// Service for converting PDFs to structured Markdown.
/// </summary>
public interface IPdfToMarkdownService
{
    /// <summary>
    /// Converts a PDF to structured Markdown.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="options">Conversion options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Markdown document with metadata.</returns>
    Task<MarkdownDocument> ConvertAsync(
        string pdfPath,
        PdfConversionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enhances an existing markdown document with LLM.
    /// </summary>
    /// <param name="document">The document to enhance.</param>
    /// <param name="level">Enhancement level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enhanced document.</returns>
    Task<MarkdownDocument> EnhanceAsync(
        MarkdownDocument document,
        EnhancementLevel level = EnhancementLevel.Basic,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for PDF to Markdown conversion.
/// </summary>
public record PdfConversionOptions
{
    /// <summary>Gets whether to extract and describe figures using vision LLM.</summary>
    public bool ExtractFigures { get; init; } = false;

    /// <summary>Gets whether to extract tables to markdown tables.</summary>
    public bool ExtractTables { get; init; } = true;

    /// <summary>Gets whether to preserve section hierarchy.</summary>
    public bool PreserveStructure { get; init; } = true;

    /// <summary>Gets whether to include page number markers.</summary>
    public bool IncludePageMarkers { get; init; } = true;

    /// <summary>Gets whether to extract and link citations.</summary>
    public bool ExtractCitations { get; init; } = true;

    /// <summary>Gets the enhancement level to apply.</summary>
    public EnhancementLevel EnhancementLevel { get; init; } = EnhancementLevel.Basic;
}

/// <summary>
/// Level of LLM enhancement to apply.
/// </summary>
public enum EnhancementLevel
{
    /// <summary>Raw pdftotext output with minimal processing.</summary>
    None,

    /// <summary>Structure detection and cleanup using heuristics.</summary>
    Basic,

    /// <summary>LLM-enhanced formatting and structure.</summary>
    Full,

    /// <summary>Full enhancement plus Vision LLM for figures.</summary>
    WithFigures,
}

/// <summary>
/// A converted markdown document.
/// </summary>
public record MarkdownDocument
{
    /// <summary>Gets or sets the paper title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the authors.</summary>
    public string[] Authors { get; init; } = [];

    /// <summary>Gets or sets the abstract.</summary>
    public string? Abstract { get; init; }

    /// <summary>Gets or sets the full markdown content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets or sets the detected sections.</summary>
    public List<DocumentSection> Sections { get; init; } = [];

    /// <summary>Gets or sets extracted figures.</summary>
    public List<DocumentFigure> Figures { get; init; } = [];

    /// <summary>Gets or sets extracted tables.</summary>
    public List<DocumentTable> Tables { get; init; } = [];

    /// <summary>Gets or sets parsed citations.</summary>
    public List<DocumentCitation> Citations { get; init; } = [];

    /// <summary>Gets or sets additional metadata.</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// A section in the document.
/// </summary>
/// <param name="Title">Section title.</param>
/// <param name="Level">Heading level (1-6).</param>
/// <param name="StartPage">Starting page number.</param>
/// <param name="EndPage">Ending page number.</param>
public record DocumentSection(string Title, int Level, int? StartPage, int? EndPage);

/// <summary>
/// A figure reference in the document.
/// </summary>
/// <param name="Id">Figure identifier (e.g., "figure-1").</param>
/// <param name="Caption">Figure caption.</param>
/// <param name="Page">Page number.</param>
/// <param name="ImagePath">Path to extracted image if available.</param>
public record DocumentFigure(string Id, string Caption, int? Page, string? ImagePath);

/// <summary>
/// A table in the document.
/// </summary>
/// <param name="Id">Table identifier.</param>
/// <param name="Caption">Table caption.</param>
/// <param name="MarkdownContent">Table as markdown.</param>
/// <param name="Page">Page number.</param>
public record DocumentTable(string Id, string Caption, string MarkdownContent, int? Page);

/// <summary>
/// A citation reference.
/// </summary>
/// <param name="Key">Citation key (e.g., "1" or "Vaswani2017").</param>
/// <param name="Text">Full citation text.</param>
/// <param name="Doi">DOI if extracted.</param>
public record DocumentCitation(string Key, string Text, string? Doi);
