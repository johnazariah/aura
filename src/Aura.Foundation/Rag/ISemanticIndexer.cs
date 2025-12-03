// <copyright file="ISemanticIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.Text.Json.Serialization;

/// <summary>
/// Semantic indexer that dispatches to language-specific strategies.
/// </summary>
public interface ISemanticIndexer
{
    /// <summary>
    /// Indexes a directory using semantic understanding of the content.
    /// Dispatches to language-specific indexers (Roslyn for C#, TreeSitter for Python, etc.).
    /// </summary>
    /// <param name="directoryPath">The directory to index.</param>
    /// <param name="options">Indexing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexing result.</returns>
    Task<SemanticIndexResult> IndexDirectoryAsync(
        string directoryPath,
        SemanticIndexOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A semantically-aware chunk of content.
/// </summary>
public record SemanticChunk
{
    /// <summary>Gets the text content of the chunk.</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>Gets the source file path.</summary>
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    /// <summary>Gets the chunk type (e.g., "class", "method", "function", "text").</summary>
    [JsonPropertyName("chunkType")]
    public required string ChunkType { get; init; }

    /// <summary>Gets the symbol name if applicable (e.g., class name, method name).</summary>
    [JsonPropertyName("symbolName")]
    public string? SymbolName { get; init; }

    /// <summary>Gets the parent symbol if applicable (e.g., containing class).</summary>
    [JsonPropertyName("parentSymbol")]
    public string? ParentSymbol { get; init; }

    /// <summary>Gets the fully qualified name if applicable.</summary>
    [JsonPropertyName("fullyQualifiedName")]
    public string? FullyQualifiedName { get; init; }

    /// <summary>Gets the start line number (1-based).</summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; init; }

    /// <summary>Gets the end line number (1-based).</summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; init; }

    /// <summary>Gets the language of the chunk.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>Gets contextual information about this chunk.</summary>
    [JsonPropertyName("context")]
    public string? Context { get; init; }

    /// <summary>Gets the signature (for methods, functions, etc).</summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    /// <summary>Gets additional metadata.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Standard chunk types for semantic chunks.
/// </summary>
public static class ChunkTypes
{
    /// <summary>Entire file (fallback).</summary>
    public const string File = "file";

    /// <summary>Namespace declaration.</summary>
    public const string Namespace = "namespace";

    /// <summary>Class definition.</summary>
    public const string Class = "class";

    /// <summary>Interface definition.</summary>
    public const string Interface = "interface";

    /// <summary>Struct definition.</summary>
    public const string Struct = "struct";

    /// <summary>Record definition.</summary>
    public const string Record = "record";

    /// <summary>Enum definition.</summary>
    public const string Enum = "enum";

    /// <summary>Method definition.</summary>
    public const string Method = "method";

    /// <summary>Property definition.</summary>
    public const string Property = "property";

    /// <summary>Field definition.</summary>
    public const string Field = "field";

    /// <summary>Top-level function.</summary>
    public const string Function = "function";

    /// <summary>Type alias.</summary>
    public const string TypeAlias = "type";

    /// <summary>Document section.</summary>
    public const string Section = "section";

    /// <summary>Plain text chunk.</summary>
    public const string Text = "text";

    /// <summary>Constructor.</summary>
    public const string Constructor = "constructor";

    /// <summary>Delegate definition.</summary>
    public const string Delegate = "delegate";

    /// <summary>Event definition.</summary>
    public const string Event = "event";
}

/// <summary>
/// Options for semantic indexing.
/// </summary>
public record SemanticIndexOptions
{
    /// <summary>Gets or sets file patterns to include.</summary>
    public string[] IncludePatterns { get; init; } = ["*.*"];

    /// <summary>Gets or sets file patterns to exclude.</summary>
    public string[] ExcludePatterns { get; init; } = ["**/bin/**", "**/obj/**", "**/node_modules/**", "**/.git/**"];

    /// <summary>Gets or sets whether to index recursively.</summary>
    public bool Recursive { get; init; } = true;

    /// <summary>Gets or sets whether to index in parallel.</summary>
    public bool Parallel { get; init; } = true;

    /// <summary>Gets or sets the maximum degree of parallelism.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;
}

/// <summary>
/// Result of semantic indexing.
/// </summary>
public record SemanticIndexResult
{
    /// <summary>Gets whether indexing was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the number of files indexed.</summary>
    public int FilesIndexed { get; init; }

    /// <summary>Gets the number of chunks created.</summary>
    public int ChunksCreated { get; init; }

    /// <summary>Gets files indexed by language.</summary>
    public Dictionary<string, int> FilesByLanguage { get; init; } = [];

    /// <summary>Gets the duration of indexing.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets any errors encountered.</summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>Gets any warnings encountered.</summary>
    public List<string> Warnings { get; init; } = [];
}
