// <copyright file="StructuredDataIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

using System.Text;
using System.Text.Json;

/// <summary>
/// Ingestor for structured data files (JSON, YAML, XML, TOML).
/// Preserves structure-aware chunking rather than treating as plain text.
/// </summary>
public sealed class StructuredDataIngestor : IContentIngestor
{
    private const int MaxChunkSize = 2000;

    /// <inheritdoc/>
    public string IngestorId => "structured-data";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } =
        [".json", ".yaml", ".yml", ".xml", ".toml", ".env", ".properties"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.PlainText;

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
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult<IReadOnlyList<IngestedChunk>>([]);
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var chunks = ext switch
        {
            ".json" => ChunkJson(filePath, content),
            ".yaml" or ".yml" => ChunkYaml(filePath, content),
            ".xml" => ChunkXml(filePath, content),
            _ => ChunkKeyValue(filePath, content),
        };

        return Task.FromResult<IReadOnlyList<IngestedChunk>>(chunks);
    }

    private static List<IngestedChunk> ChunkJson(string filePath, string content)
    {
        var chunks = new List<IngestedChunk>();
        var fileName = Path.GetFileName(filePath);

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Chunk by top-level keys
                var currentChunk = new StringBuilder();
                var chunkIndex = 0;

                foreach (var prop in root.EnumerateObject())
                {
                    var propText = $"\"{prop.Name}\": {prop.Value.GetRawText()}";

                    if (currentChunk.Length + propText.Length > MaxChunkSize && currentChunk.Length > 0)
                    {
                        chunkIndex++;
                        chunks.Add(new IngestedChunk(currentChunk.ToString().TrimEnd(',', '\n', ' '), "json-section")
                        {
                            Title = chunkIndex == 1 ? fileName : $"{fileName} (part {chunkIndex})",
                            Language = "json",
                        });
                        currentChunk.Clear();
                    }

                    currentChunk.AppendLine(propText + ",");
                }

                if (currentChunk.Length > 0)
                {
                    chunkIndex++;
                    chunks.Add(new IngestedChunk(currentChunk.ToString().TrimEnd(',', '\n', ' '), chunkIndex == 1 ? "document" : "json-section")
                    {
                        Title = chunkIndex == 1 ? fileName : $"{fileName} (part {chunkIndex})",
                        Language = "json",
                    });
                }
            }
            else
            {
                // Array or primitive — single chunk
                chunks.Add(new IngestedChunk(content, "document")
                {
                    Title = fileName,
                    Language = "json",
                });
            }
        }
        catch (JsonException)
        {
            // Invalid JSON — fall back to single chunk
            chunks.Add(new IngestedChunk(content, "document")
            {
                Title = fileName,
                Language = "json",
            });
        }

        return chunks;
    }

    private static List<IngestedChunk> ChunkYaml(string filePath, string content)
    {
        var chunks = new List<IngestedChunk>();
        var fileName = Path.GetFileName(filePath);

        // Split YAML by top-level keys (lines that start at column 0 and end with ':')
        var sections = new List<string>();
        var currentSection = new StringBuilder();

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            // Top-level key: starts at column 0 with a non-space, non-comment char
            if (trimmed.Length > 0
                && !char.IsWhiteSpace(trimmed[0])
                && trimmed[0] != '#'
                && trimmed[0] != '-'
                && currentSection.Length > 0)
            {
                sections.Add(currentSection.ToString().Trim());
                currentSection.Clear();
            }

            currentSection.AppendLine(trimmed);
        }

        if (currentSection.Length > 0)
        {
            sections.Add(currentSection.ToString().Trim());
        }

        // Merge small sections, split large ones
        var merged = new StringBuilder();
        var chunkIndex = 0;

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section)) continue;

            if (merged.Length + section.Length > MaxChunkSize && merged.Length > 0)
            {
                chunkIndex++;
                chunks.Add(new IngestedChunk(merged.ToString().Trim(), chunkIndex == 1 ? "document" : "yaml-section")
                {
                    Title = chunkIndex == 1 ? fileName : $"{fileName} (part {chunkIndex})",
                    Language = "yaml",
                });
                merged.Clear();
            }

            merged.AppendLine(section);
        }

        if (merged.Length > 0)
        {
            chunkIndex++;
            chunks.Add(new IngestedChunk(merged.ToString().Trim(), chunkIndex == 1 ? "document" : "yaml-section")
            {
                Title = chunkIndex == 1 ? fileName : $"{fileName} (part {chunkIndex})",
                Language = "yaml",
            });
        }

        return chunks;
    }

    private static List<IngestedChunk> ChunkXml(string filePath, string content)
    {
        // XML: chunk by top-level elements under root
        var chunks = new List<IngestedChunk>();
        var fileName = Path.GetFileName(filePath);

        if (content.Length <= MaxChunkSize)
        {
            chunks.Add(new IngestedChunk(content, "document")
            {
                Title = fileName,
                Language = "xml",
            });
            return chunks;
        }

        // Simple line-based splitting for large XML
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var line in content.Split('\n'))
        {
            currentChunk.AppendLine(line.TrimEnd('\r'));

            if (currentChunk.Length >= MaxChunkSize)
            {
                chunkIndex++;
                chunks.Add(new IngestedChunk(currentChunk.ToString().Trim(), "xml-section")
                {
                    Title = $"{fileName} (part {chunkIndex})",
                    Language = "xml",
                });
                currentChunk.Clear();
            }
        }

        if (currentChunk.Length > 0)
        {
            chunkIndex++;
            chunks.Add(new IngestedChunk(currentChunk.ToString().Trim(), chunkIndex == 1 ? "document" : "xml-section")
            {
                Title = chunkIndex == 1 ? fileName : $"{fileName} (part {chunkIndex})",
                Language = "xml",
            });
        }

        return chunks;
    }

    private static List<IngestedChunk> ChunkKeyValue(string filePath, string content)
    {
        // .env, .properties, .toml — simple key=value
        return
        [
            new IngestedChunk(content, "document")
            {
                Title = Path.GetFileName(filePath),
            }
        ];
    }
}
