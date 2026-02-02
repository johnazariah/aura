// <copyright file="Source.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

using Pgvector;

/// <summary>
/// A citable research source (paper, article, book, or note).
/// </summary>
public class Source
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the type of source.</summary>
    public SourceType SourceType { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the authors.</summary>
    public string[] Authors { get; set; } = [];

    /// <summary>Gets or sets the abstract or summary.</summary>
    public string? Abstract { get; set; }

    /// <summary>Gets or sets the original URL.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets the DOI if academic paper.</summary>
    public string? Doi { get; set; }

    /// <summary>Gets or sets the arXiv identifier.</summary>
    public string? ArxivId { get; set; }

    /// <summary>Gets or sets the publication date.</summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>Gets or sets the publication venue (journal, conference, etc.).</summary>
    public string? Venue { get; set; }

    /// <summary>Gets or sets the citation count if available.</summary>
    public int? CitationCount { get; set; }

    /// <summary>Gets or sets the path to cached PDF.</summary>
    public string? PdfPath { get; set; }

    /// <summary>Gets or sets the path to converted markdown.</summary>
    public string? MarkdownPath { get; set; }

    /// <summary>Gets or sets the semantic embedding of the abstract.</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Gets or sets user-assigned tags.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Gets or sets user notes about this source.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the reading status.</summary>
    public ReadingStatus ReadingStatus { get; set; } = ReadingStatus.ToRead;

    /// <summary>Gets or sets when this source was imported.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when this source was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the excerpts from this source.</summary>
    public ICollection<Excerpt> Excerpts { get; set; } = [];
}
