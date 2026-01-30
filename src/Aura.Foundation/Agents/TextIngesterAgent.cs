// <copyright file="TextIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Native text ingester that chunks files by paragraphs or markdown headers.
/// Does not use LLM - purely rule-based chunking.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TextIngesterAgent"/> class.
/// </remarks>
/// <param name="logger">Optional logger.</param>
public sealed partial class TextIngesterAgent(ILogger<TextIngesterAgent>? logger = null) : IAgent
{
    private readonly ILogger<TextIngesterAgent>? _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "text-ingester";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Text Ingester",
        Description: "Chunks text files by paragraphs or markdown headers. Used for documentation and plain text.",
        Capabilities: ["ingest:txt", "ingest:md", "ingest:rst", "ingest:adoc", "ingest:log"],
        Priority: 70,  // Below LLM fallback (40), above apologetic fallback (99)
        Languages: [],
        Provider: "native",
        Model: "none",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "text", "native", "markdown"]);

    /// <inheritdoc/>
    public Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var ingesterContext = context.GetIngesterContext()
            ?? new IngesterContext("unknown", context.Prompt ?? string.Empty);
        var filePath = ingesterContext.FilePath;
        var content = ingesterContext.Content;
        var extension = ingesterContext.Extension;

        _logger?.LogDebug("Chunking text file: {FilePath}", filePath);

        var chunks = extension switch
        {
            "md" or "markdown" => ChunkMarkdown(content, filePath),
            _ => ChunkByParagraphs(content, filePath),
        };

        _logger?.LogDebug("Created {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);

        var output = new AgentOutput(
            Content: $"Created {chunks.Count} text chunks from {Path.GetFileName(filePath)}",
            Artifacts: new Dictionary<string, string>
            {
                [ArtifactKeys.Chunks] = JsonSerializer.Serialize(chunks),
                [ArtifactKeys.Language] = extension,
                [ArtifactKeys.Parser] = "text",
            });

        return Task.FromResult(output);
    }

    private static List<SemanticChunk> ChunkMarkdown(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();
        var lines = content.Split('\n');
        var currentSection = new List<string>();
        var currentHeader = string.Empty;
        var sectionStartLine = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var headerMatch = MarkdownHeaderRegex().Match(line);

            if (headerMatch.Success)
            {
                // Save previous section
                if (currentSection.Count > 0)
                {
                    chunks.Add(CreateTextChunk(
                        string.Join("\n", currentSection),
                        filePath,
                        currentHeader,
                        sectionStartLine,
                        i));
                }

                // Start new section
                currentHeader = headerMatch.Groups[2].Value.Trim();
                currentSection = [line];
                sectionStartLine = i + 1;
            }
            else
            {
                currentSection.Add(line);
            }
        }

        // Save final section
        if (currentSection.Count > 0)
        {
            chunks.Add(CreateTextChunk(
                string.Join("\n", currentSection),
                filePath,
                currentHeader,
                sectionStartLine,
                lines.Length));
        }

        return chunks;
    }

    private static List<SemanticChunk> ChunkByParagraphs(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();
        var paragraphs = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentLine = 1;
        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var lineCount = paragraph.Split('\n').Length;

            chunks.Add(CreateTextChunk(
                trimmed,
                filePath,
                GetFirstWords(trimmed, 5),
                currentLine,
                currentLine + lineCount - 1));

            currentLine += lineCount + 1; // +1 for the blank line separator
        }

        return chunks;
    }

    private static SemanticChunk CreateTextChunk(
        string text,
        string filePath,
        string symbolName,
        int startLine,
        int endLine)
    {
        return new SemanticChunk
        {
            Text = text,
            FilePath = filePath,
            ChunkType = ChunkTypes.Section,
            SymbolName = string.IsNullOrEmpty(symbolName) ? $"Section at line {startLine}" : symbolName,
            StartLine = startLine,
            EndLine = endLine,
            Language = "text",
        };
    }

    private static string GetFirstWords(string text, int wordCount)
    {
        var words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(wordCount));
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex MarkdownHeaderRegex();
}
