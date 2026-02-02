// <copyright file="Excerpt.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

using Pgvector;

/// <summary>
/// A highlighted or annotated excerpt from a source.
/// </summary>
public class Excerpt
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the parent source ID.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Gets or sets the parent source.</summary>
    public Source? Source { get; set; }

    /// <summary>Gets or sets the highlighted text content.</summary>
    public required string Content { get; set; }

    /// <summary>Gets or sets the page number if from PDF.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Gets or sets the section or location within the source.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets user annotation on this excerpt.</summary>
    public string? Annotation { get; set; }

    /// <summary>Gets or sets the semantic embedding.</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Gets or sets when this excerpt was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
