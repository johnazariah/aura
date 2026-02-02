// <copyright file="ConceptLink.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

/// <summary>
/// An edge in the knowledge graph between two concepts.
/// </summary>
public class ConceptLink
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the source concept ID.</summary>
    public Guid FromConceptId { get; set; }

    /// <summary>Gets or sets the source concept.</summary>
    public Concept? FromConcept { get; set; }

    /// <summary>Gets or sets the target concept ID.</summary>
    public Guid ToConceptId { get; set; }

    /// <summary>Gets or sets the target concept.</summary>
    public Concept? ToConcept { get; set; }

    /// <summary>Gets or sets the relationship type.</summary>
    public required string Relationship { get; set; }

    /// <summary>Gets or sets the source that established this link.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>Gets or sets the source reference.</summary>
    public Source? Source { get; set; }

    /// <summary>Gets or sets the AI confidence score (0-1).</summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>Gets or sets when this link was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
