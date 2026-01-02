// <copyright file="PlainTextIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

using System.Text;

/// <summary>
/// Fallback ingestor for plain text files. Uses simple line-based chunking.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PlainTextIngestor"/> class.
/// </remarks>
/// <param name="chunkSize">Target chunk size in characters.</param>
/// <param name="overlap">Overlap between chunks in characters.</param>
public sealed class PlainTextIngestor(int chunkSize = 1500, int overlap = 200) : IContentIngestor
{
    private readonly int _chunkSize = chunkSize;
    private readonly int _overlap = overlap;

    /// <inheritdoc/>
    public string IngestorId => "plaintext";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".txt", ".text", ".log", ".cfg", ".ini", ".conf"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.PlainText;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        var ext = Path.GetExtension(filePath);

        // Handle files with supported extensions
        if (SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Handle files with no extension (like README, LICENSE, etc.)
        if (string.IsNullOrEmpty(ext))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        var currentChunk = new StringBuilder();
        int chunkStartLine = 1;
        int currentLine = 0;
        int chunkCount = 0;

        foreach (var rawLine in lines)
        {
            currentLine++;
            var line = rawLine.TrimEnd('\r');
            currentChunk.AppendLine(line);

            if (currentChunk.Length >= _chunkSize)
            {
                chunkCount++;
                chunks.Add(new IngestedChunk(currentChunk.ToString().Trim(), "block")
                {
                    Title = $"{Path.GetFileName(filePath)} (part {chunkCount})",
                    StartLine = chunkStartLine,
                    EndLine = currentLine,
                });

                // Start new chunk with overlap
                currentChunk.Clear();
                var overlapLines = Math.Min(5, currentLine);
                var overlapStart = currentLine - overlapLines;

                for (int i = overlapStart; i < currentLine && i < lines.Length; i++)
                {
                    currentChunk.AppendLine(lines[i].TrimEnd('\r'));
                }

                chunkStartLine = overlapStart + 1;
            }
        }

        // Add remaining content
        var remaining = currentChunk.ToString().Trim();
        if (remaining.Length > 0)
        {
            chunkCount++;
            chunks.Add(new IngestedChunk(remaining, chunkCount == 1 ? "document" : "block")
            {
                Title = chunkCount == 1
                    ? Path.GetFileName(filePath)
                    : $"{Path.GetFileName(filePath)} (part {chunkCount})",
                StartLine = chunkStartLine,
                EndLine = currentLine,
            });
        }

        return Task.FromResult<IReadOnlyList<IngestedChunk>>(chunks);
    }
}
