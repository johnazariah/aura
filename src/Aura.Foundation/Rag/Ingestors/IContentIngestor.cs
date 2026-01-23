// <copyright file="IContentIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

/// <summary>
/// Interface for content ingestors that process files into RAG-ready chunks.
/// Each ingestor understands the structure of specific file types.
/// </summary>
public interface IContentIngestor
{
    /// <summary>
    /// Gets the unique identifier for this ingestor.
    /// </summary>
    string IngestorId { get; }

    /// <summary>
    /// Gets the file extensions this ingestor can handle (e.g., ".md", ".cs").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Gets the content types this ingestor produces.
    /// </summary>
    RagContentType ContentType { get; }

    /// <summary>
    /// Determines if this ingestor can handle the given file.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if this ingestor can process the file.</returns>
    bool CanIngest(string filePath);

    /// <summary>
    /// Ingests a file and produces structured chunks for RAG indexing.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="content">The file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of content chunks with metadata.</returns>
    Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extended ingestor for code files that produces both RAG chunks and code graph nodes.
/// Modules register specialized code ingestors (e.g., RoslynCodeIngestor for C#).
/// </summary>
public interface ICodeIngestor : IContentIngestor
{
    /// <summary>
    /// Ingests a code file and produces RAG chunks, code graph nodes, and edges.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="content">The file content.</param>
    /// <param name="workspacePath">The workspace path for graph isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing chunks, nodes, and edges.</returns>
    Task<CodeIngestionResult> IngestCodeAsync(
        string filePath,
        string content,
        string workspacePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of code ingestion containing RAG chunks and code graph elements.
/// </summary>
/// <param name="Chunks">RAG chunks for embedding and semantic search.</param>
/// <param name="Nodes">Code graph nodes (types, members, etc.).</param>
/// <param name="Edges">Code graph edges (relationships between nodes).</param>
public record CodeIngestionResult(
    IReadOnlyList<IngestedChunk> Chunks,
    IReadOnlyList<Data.Entities.CodeNode> Nodes,
    IReadOnlyList<Data.Entities.CodeEdge> Edges);

/// <summary>
/// A chunk of content produced by an ingestor.
/// </summary>
/// <param name="Text">The chunk text content.</param>
/// <param name="ChunkType">The type of chunk (e.g., "header", "code-block", "function").</param>
public record IngestedChunk(string Text, string ChunkType)
{
    /// <summary>
    /// Gets or sets the title or heading for this chunk.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets the symbol name (for code: class name, method name, etc.).
    /// </summary>
    public string? SymbolName { get; init; }

    /// <summary>
    /// Gets or sets the fully qualified name (for code).
    /// </summary>
    public string? FullyQualifiedName { get; init; }

    /// <summary>
    /// Gets or sets the signature for code symbols (e.g., "public async Task ProcessAsync(string input)").
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// Gets or sets the parent symbol name (e.g., class name for a method).
    /// </summary>
    public string? ParentSymbol { get; init; }

    /// <summary>
    /// Gets or sets the language (for code chunks).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets or sets the start line number in the source file.
    /// </summary>
    public int? StartLine { get; init; }

    /// <summary>
    /// Gets or sets the end line number in the source file.
    /// </summary>
    public int? EndLine { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for this chunk.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
