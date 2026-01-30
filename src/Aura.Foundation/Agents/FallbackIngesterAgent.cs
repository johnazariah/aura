// <copyright file="FallbackIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text.Json;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fallback ingester agent that returns the whole file as a single chunk.
/// Used when no specialized ingester is available for a file type.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FallbackIngesterAgent"/> class.
/// </remarks>
/// <param name="logger">Optional logger.</param>
public sealed class FallbackIngesterAgent(ILogger<FallbackIngesterAgent>? logger = null) : IAgent
{
    private readonly ILogger<FallbackIngesterAgent>? _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "fallback-ingester";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Fallback Ingester",
        Description: "Last resort ingester when no specialized parser exists. Returns whole file as a single chunk with a warning.",
        Capabilities: ["ingest:*"],
        Priority: 99,  // Last resort - lowest priority (higher number = lower priority)
        Languages: [],  // Polyglot
        Provider: "native",
        Model: "none",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "fallback", "native"]);

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

        _logger?.LogDebug("Using fallback ingester for {FilePath} (no specialized parser for .{Extension})",
            filePath, extension);

        // Count lines for line numbers
        var lineCount = content.Split('\n').Length;

        // Create single chunk with whole file
        var chunk = new SemanticChunk
        {
            Text = content,
            FilePath = filePath,
            ChunkType = ChunkTypes.File,
            SymbolName = Path.GetFileName(filePath),
            StartLine = 1,
            EndLine = lineCount,
            Language = extension,
            Context = $"Whole file (no specialized parser for .{extension})",
            Metadata = new Dictionary<string, string>
            {
                ["warning"] = $"No specialized parser for .{extension} files. " +
                              "Content indexed as plain text. " +
                              "Consider adding an ingester agent for better semantic results.",
                ["fallback"] = "true",
            },
        };

        var chunks = new[] { chunk };

        var output = new AgentOutput(
            Content: $"⚠️ No specialized parser for .{extension}. Indexed as plain text (1 chunk, {lineCount} lines).",
            Artifacts: new Dictionary<string, string>
            {
                [ArtifactKeys.Chunks] = JsonSerializer.Serialize(chunks),
                [ArtifactKeys.Language] = extension,
                [ArtifactKeys.Parser] = "fallback",
                ["fallback"] = "true",
            });

        return Task.FromResult(output);
    }
}
