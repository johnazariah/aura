// <copyright file="MarkdownIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Ingestor for Markdown files. Splits content by headers and preserves code blocks.
/// </summary>
public sealed partial class MarkdownIngestor : IContentIngestor
{
    /// <inheritdoc/>
    public string IngestorId => "markdown";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".md", ".markdown", ".mdx"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.Markdown;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        var currentSection = new StringBuilder();
        string? currentHeader = null;
        int currentHeaderLevel = 0;
        int sectionStartLine = 1;
        int lineNumber = 0;
        bool inCodeBlock = false;
        string? codeBlockLanguage = null;
        var codeBlockContent = new StringBuilder();
        int codeBlockStartLine = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');

            // Check for code block boundaries
            if (line.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    // Starting a code block
                    inCodeBlock = true;
                    codeBlockLanguage = line.Length > 3 ? line[3..].Trim() : null;
                    codeBlockStartLine = lineNumber;
                    codeBlockContent.Clear();
                }
                else
                {
                    // Ending a code block - emit as separate chunk if substantial
                    inCodeBlock = false;
                    var codeText = codeBlockContent.ToString().Trim();

                    if (codeText.Length > 50) // Only chunk substantial code blocks
                    {
                        chunks.Add(new IngestedChunk(codeText, "code-block")
                        {
                            Title = currentHeader,
                            Language = codeBlockLanguage,
                            StartLine = codeBlockStartLine,
                            EndLine = lineNumber,
                        });
                    }

                    // Also add to current section for context
                    currentSection.AppendLine(line);
                }

                if (!inCodeBlock)
                {
                    currentSection.AppendLine(line);
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent.AppendLine(line);
                currentSection.AppendLine(line);
                continue;
            }

            // Check for headers
            var headerMatch = HeaderRegex().Match(line);
            if (headerMatch.Success)
            {
                // Emit previous section if it has content
                var sectionText = currentSection.ToString().Trim();
                if (sectionText.Length > 0)
                {
                    chunks.Add(new IngestedChunk(sectionText, "section")
                    {
                        Title = currentHeader,
                        StartLine = sectionStartLine,
                        EndLine = lineNumber - 1,
                    });
                }

                // Start new section
                currentHeaderLevel = headerMatch.Groups[1].Value.Length;
                currentHeader = headerMatch.Groups[2].Value.Trim();
                currentSection.Clear();
                currentSection.AppendLine(line);
                sectionStartLine = lineNumber;
            }
            else
            {
                currentSection.AppendLine(line);
            }
        }

        // Emit final section
        var finalSection = currentSection.ToString().Trim();
        if (finalSection.Length > 0)
        {
            chunks.Add(new IngestedChunk(finalSection, "section")
            {
                Title = currentHeader,
                StartLine = sectionStartLine,
                EndLine = lineNumber,
            });
        }

        // If no chunks, add the whole file as one chunk
        if (chunks.Count == 0 && content.Trim().Length > 0)
        {
            chunks.Add(new IngestedChunk(content.Trim(), "document")
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                StartLine = 1,
                EndLine = lineNumber,
            });
        }

        return Task.FromResult<IReadOnlyList<IngestedChunk>>(chunks);
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeaderRegex();
}
