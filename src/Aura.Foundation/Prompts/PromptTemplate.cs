// <copyright file="PromptTemplate.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

/// <summary>
/// A prompt template with metadata.
/// </summary>
public sealed class PromptTemplate
{
    /// <summary>Gets the prompt name (derived from filename).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the prompt description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the raw template content (Handlebars format).</summary>
    public required string Template { get; init; }

    /// <summary>Gets the source file path.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Gets when the prompt was last loaded.</summary>
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the RAG queries to use for context retrieval.</summary>
    public IReadOnlyList<string> RagQueries { get; init; } = [];
}
