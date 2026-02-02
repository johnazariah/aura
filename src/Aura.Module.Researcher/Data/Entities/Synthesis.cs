// <copyright file="Synthesis.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

/// <summary>
/// A synthesized document (literature review, comparison, summary).
/// </summary>
public class Synthesis
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the synthesis style.</summary>
    public SynthesisStyle Style { get; set; } = SynthesisStyle.Summary;

    /// <summary>Gets or sets the focus or topic.</summary>
    public string? Focus { get; set; }

    /// <summary>Gets or sets the generated content.</summary>
    public string? Content { get; set; }

    /// <summary>Gets or sets the source IDs used in this synthesis.</summary>
    public Guid[] SourceIds { get; set; } = [];

    /// <summary>Gets or sets when this synthesis was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when this synthesis was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Style of synthesis document.
/// </summary>
public enum SynthesisStyle
{
    /// <summary>Brief summary of sources.</summary>
    Summary,

    /// <summary>Comprehensive literature review.</summary>
    LiteratureReview,

    /// <summary>Side-by-side comparison.</summary>
    Comparison,

    /// <summary>Chronological overview.</summary>
    Timeline,
}
