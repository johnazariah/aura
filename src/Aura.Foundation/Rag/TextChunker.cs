// <copyright file="TextChunker.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Splits text into chunks suitable for embedding.
/// Preserves semantic boundaries like paragraphs and code blocks.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TextChunker"/> class.
/// </remarks>
/// <param name="chunkSize">Target chunk size in characters.</param>
/// <param name="chunkOverlap">Overlap between chunks in characters.</param>
public sealed class TextChunker(int chunkSize = 2000, int chunkOverlap = 200)
{
    private readonly int _chunkSize = chunkSize;
    private readonly int _chunkOverlap = chunkOverlap;

    /// <summary>
    /// Splits text into chunks.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="contentType">The content type for specialized splitting.</param>
    /// <returns>List of text chunks.</returns>
    public IReadOnlyList<string> Split(string text, RagContentType contentType = RagContentType.PlainText)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return contentType switch
        {
            RagContentType.Code => SplitCode(text),
            RagContentType.Markdown => SplitMarkdown(text),
            _ => SplitPlainText(text),
        };
    }

    private List<string> SplitCode(string text)
    {
        var chunks = new List<string>();
        var lines = text.Split('\n');
        var currentChunk = new System.Text.StringBuilder();
        var braceDepth = 0;

        foreach (var line in lines)
        {
            // Track brace depth for better code block preservation
            braceDepth += line.Count(c => c == '{') - line.Count(c => c == '}');

            currentChunk.AppendLine(line);

            // Split at natural boundaries (outside of blocks) when chunk is large enough
            if (currentChunk.Length >= _chunkSize && braceDepth == 0)
            {
                var chunk = currentChunk.ToString().Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }

                // Start new chunk with overlap
                currentChunk.Clear();
                var overlapLines = GetLastLines(chunk, _chunkOverlap);
                if (!string.IsNullOrEmpty(overlapLines))
                {
                    currentChunk.Append(overlapLines);
                }
            }
        }

        // Add remaining content
        var remaining = currentChunk.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            chunks.Add(remaining);
        }

        return chunks;
    }

    private List<string> SplitMarkdown(string text)
    {
        var chunks = new List<string>();
        var sections = SplitByHeaders(text);

        foreach (var section in sections)
        {
            if (section.Length <= _chunkSize)
            {
                chunks.Add(section);
            }
            else
            {
                // Split large sections by paragraphs
                chunks.AddRange(SplitByParagraphs(section));
            }
        }

        return chunks;
    }

    private List<string> SplitPlainText(string text)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > _chunkSize && currentChunk.Length > 0)
            {
                var chunk = currentChunk.ToString().Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }

                // Start new chunk with overlap from end of previous
                currentChunk.Clear();
                var overlap = GetLastCharacters(chunk, _chunkOverlap);
                if (!string.IsNullOrEmpty(overlap))
                {
                    currentChunk.Append(overlap);
                    currentChunk.Append("\n\n");
                }
            }

            currentChunk.Append(paragraph);
            currentChunk.Append("\n\n");
        }

        var remaining = currentChunk.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            chunks.Add(remaining);
        }

        return chunks;
    }

    private List<string> SplitByHeaders(string text)
    {
        var sections = new List<string>();
        var lines = text.Split('\n');
        var currentSection = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            // Check for markdown headers
            if (line.StartsWith('#') && currentSection.Length > 0)
            {
                var section = currentSection.ToString().Trim();
                if (!string.IsNullOrEmpty(section))
                {
                    sections.Add(section);
                }
                currentSection.Clear();
            }

            currentSection.AppendLine(line);
        }

        var remaining = currentSection.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            sections.Add(remaining);
        }

        return sections;
    }

    private List<string> SplitByParagraphs(string text)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new System.Text.StringBuilder();

        foreach (var para in paragraphs)
        {
            if (currentChunk.Length + para.Length > _chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            currentChunk.AppendLine(para);
            currentChunk.AppendLine();
        }

        var remaining = currentChunk.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            chunks.Add(remaining);
        }

        return chunks;
    }

    private static string GetLastLines(string text, int charLimit)
    {
        if (string.IsNullOrEmpty(text) || charLimit <= 0)
        {
            return string.Empty;
        }

        var lines = text.Split('\n');
        var result = new System.Text.StringBuilder();

        for (var i = lines.Length - 1; i >= 0 && result.Length < charLimit; i--)
        {
            result.Insert(0, lines[i] + "\n");
        }

        return result.ToString().TrimEnd();
    }

    private static string GetLastCharacters(string text, int charLimit)
    {
        if (string.IsNullOrEmpty(text) || charLimit <= 0)
        {
            return string.Empty;
        }

        return text.Length <= charLimit
            ? text
            : text.Substring(text.Length - charLimit);
    }
}
