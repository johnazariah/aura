// <copyright file="RagOptions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Configuration options for the RAG service.
/// </summary>
public sealed class RagOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Rag";

    /// <summary>
    /// Gets or sets the embedding model to use.
    /// Default: nomic-embed-text (768 dimensions).
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets the embedding dimension.
    /// Must match the model's output dimension.
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;

    /// <summary>
    /// Gets or sets the chunk size in characters.
    /// </summary>
    public int ChunkSize { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the chunk overlap in characters.
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Gets or sets the default number of results to return.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum relevance score (0.0 to 1.0).
    /// Results below this threshold are filtered out.
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.3;
}

/// <summary>
/// Options for RAG query operations.
/// </summary>
public sealed record RagQueryOptions
{
    /// <summary>
    /// Gets or sets the number of results to return.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or sets the minimum relevance score.
    /// </summary>
    public double? MinScore { get; init; }

    /// <summary>
    /// Gets or sets content types to filter by.
    /// </summary>
    public IReadOnlyList<RagContentType>? ContentTypes { get; init; }

    /// <summary>
    /// Gets or sets a source path prefix to filter by.
    /// </summary>
    public string? SourcePathPrefix { get; init; }
}

/// <summary>
/// Options for RAG index operations.
/// </summary>
public sealed record RagIndexOptions
{
    /// <summary>
    /// Default patterns to exclude from indexing.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultExcludePatterns =
    [
        "**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**",
        "**/.vs/**", "**/packages/**", "**/dist/**", "**/.nuget/**",
        "**/*.dll", "**/*.exe", "**/*.pdb", "**/*.cache",
        "**/wwwroot/lib/**", "**/.idea/**", "**/coverage/**",
    ];

    /// <summary>
    /// Default patterns to include for indexing.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultIncludePatterns =
    [
        "*.cs", "*.md", "*.txt", "*.json", "*.yaml", "*.yml",
        "*.ts", "*.tsx", "*.js", "*.jsx", "*.py", "*.rs",
    ];

    /// <summary>
    /// Gets or sets file patterns to include (e.g., "*.cs", "*.md").
    /// </summary>
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>
    /// Gets or sets file patterns to exclude.
    /// </summary>
    public IReadOnlyList<string>? ExcludePatterns { get; init; }

    /// <summary>
    /// Gets or sets whether to recursively index subdirectories.
    /// </summary>
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// Gets or sets the content type override.
    /// If null, type is auto-detected from file extension.
    /// </summary>
    public RagContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the effective include patterns (uses defaults if not specified).
    /// </summary>
    public IReadOnlyList<string> EffectiveIncludePatterns =>
        IncludePatterns ?? DefaultIncludePatterns;

    /// <summary>
    /// Gets the effective exclude patterns (uses defaults if not specified).
    /// </summary>
    public IReadOnlyList<string> EffectiveExcludePatterns =>
        ExcludePatterns ?? DefaultExcludePatterns;
}
