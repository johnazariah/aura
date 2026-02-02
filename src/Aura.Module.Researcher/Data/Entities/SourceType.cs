// <copyright file="SourceType.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Data.Entities;

/// <summary>
/// Type of research source.
/// </summary>
public enum SourceType
{
    /// <summary>Academic paper (arXiv, journal, conference).</summary>
    Paper,

    /// <summary>Blog post, documentation, or web article.</summary>
    Article,

    /// <summary>Book or book chapter.</summary>
    Book,

    /// <summary>User-created research note.</summary>
    Note,
}
