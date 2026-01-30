// <copyright file="RagContent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Content to be indexed in the RAG system.
/// </summary>
/// <param name="ContentId">Unique identifier for this content (e.g., file path, URL).</param>
/// <param name="Text">The text content to index.</param>
/// <param name="ContentType">Type of content for specialized processing.</param>
public sealed record RagContent(
    string ContentId,
    string Text,
    RagContentType ContentType = RagContentType.PlainText)
{
    /// <summary>
    /// Gets or sets the source file path (if applicable).
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets or sets the workspace ID this content belongs to.
    /// This is the 16-char hex hash of the normalized workspace path.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for the content.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets or sets the language (for code content).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Creates a RagContent from a file.
    /// </summary>
    public static RagContent FromFile(string filePath, string content, RagContentType? type = null)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var detectedType = type ?? DetectContentType(extension);

        return new RagContent(filePath, content, detectedType)
        {
            SourcePath = filePath,
            Language = DetectLanguage(extension),
        };
    }

    private static RagContentType DetectContentType(string extension) => extension switch
    {
        ".cs" or ".fs" or ".vb" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" or ".h" or ".go" or ".rs" => RagContentType.Code,
        ".md" or ".markdown" => RagContentType.Markdown,
        ".txt" => RagContentType.PlainText,
        ".pdf" => RagContentType.Pdf,
        _ => RagContentType.Unknown,
    };

    private static string? DetectLanguage(string extension) => extension switch
    {
        ".cs" => "csharp",
        ".fs" => "fsharp",
        ".vb" => "vb",
        ".py" => "python",
        ".js" => "javascript",
        ".ts" => "typescript",
        ".java" => "java",
        ".cpp" or ".cc" => "cpp",
        ".c" => "c",
        ".h" => "c",
        ".go" => "go",
        ".rs" => "rust",
        _ => null,
    };
}
