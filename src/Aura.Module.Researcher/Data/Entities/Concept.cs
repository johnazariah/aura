// <copyright file="Concept.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

using Pgvector;

/// <summary>
/// A concept node in the knowledge graph.
/// </summary>
public class Concept
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the concept name.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the definition.</summary>
    public string? Definition { get; set; }

    /// <summary>Gets or sets alternative names for this concept.</summary>
    public string[] Aliases { get; set; } = [];

    /// <summary>Gets or sets the semantic embedding.</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Gets or sets when this concept was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets outgoing links from this concept.</summary>
    public ICollection<ConceptLink> OutgoingLinks { get; set; } = [];

    /// <summary>Gets or sets incoming links to this concept.</summary>
    public ICollection<ConceptLink> IncomingLinks { get; set; } = [];

    /// <summary>Gets or sets the sources that mention this concept.</summary>
    public ICollection<SourceConcept> SourceConcepts { get; set; } = [];
}
