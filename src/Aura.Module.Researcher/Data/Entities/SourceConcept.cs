// <copyright file="SourceConcept.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

/// <summary>
/// Join table linking sources to concepts they mention.
/// </summary>
public class SourceConcept
{
    /// <summary>Gets or sets the source ID.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Gets or sets the source.</summary>
    public Source? Source { get; set; }

    /// <summary>Gets or sets the concept ID.</summary>
    public Guid ConceptId { get; set; }

    /// <summary>Gets or sets the concept.</summary>
    public Concept? Concept { get; set; }

    /// <summary>Gets or sets the mention count in this source.</summary>
    public int MentionCount { get; set; } = 1;

    /// <summary>Gets or sets whether this concept is a primary topic of the source.</summary>
    public bool IsPrimaryTopic { get; set; }
}
