// <copyright file="RegexIngesterBase.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for regex-based language ingesters.
/// Provides common infrastructure for parsing code files using regular expressions.
/// </summary>
public abstract partial class RegexIngesterBase : IAgent
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegexIngesterBase"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    protected RegexIngesterBase(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public abstract string AgentId { get; }

    /// <inheritdoc/>
    public abstract AgentMetadata Metadata { get; }

    /// <summary>
    /// Gets the language identifier for this ingester.
    /// </summary>
    protected abstract string Language { get; }

    /// <summary>
    /// Gets the regex patterns for extracting declarations from the source code.
    /// </summary>
    /// <returns>Collection of declaration patterns.</returns>
    protected abstract IEnumerable<DeclarationPattern> GetPatterns();

    /// <inheritdoc/>
    public Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var filePath = context.Properties.GetValueOrDefault("filePath") as string
            ?? throw new ArgumentException("filePath is required");
        var content = context.Properties.GetValueOrDefault("content") as string
            ?? throw new ArgumentException("content is required");

        _logger.LogDebug("Parsing {Language} file with regex: {FilePath}", Language, filePath);

        try
        {
            var chunks = ParseContent(content, filePath);

            _logger.LogDebug("Extracted {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);

            var output = new AgentOutput(
                Content: $"Extracted {chunks.Count} semantic chunks from {Path.GetFileName(filePath)}",
                Artifacts: new Dictionary<string, string>
                {
                    ["chunks"] = JsonSerializer.Serialize(chunks),
                    ["language"] = Language,
                    ["parser"] = "regex",
                });

            return Task.FromResult(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {Language} file: {FilePath}", Language, filePath);
            throw;
        }
    }

    /// <summary>
    /// Parses the content and extracts semantic chunks.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>List of semantic chunks.</returns>
    protected virtual List<SemanticChunk> ParseContent(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();
        var lines = content.Split('\n');
        var patterns = GetPatterns().ToList();

        foreach (var pattern in patterns)
        {
            var matches = pattern.Regex.Matches(content);

            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var text = match.Value;
                var startIndex = match.Index;
                var endIndex = startIndex + match.Length;

                // Calculate line numbers
                var startLine = GetLineNumber(content, startIndex);
                var endLine = GetLineNumber(content, endIndex);

                // Extract symbol name
                var symbolName = ExtractSymbolName(match, pattern);
                if (string.IsNullOrEmpty(symbolName))
                {
                    continue;
                }

                // Extract parent symbol if present
                var parentSymbol = ExtractParentSymbol(match, pattern);

                // Build fully qualified name
                var fullyQualifiedName = BuildFullyQualifiedName(match, pattern, symbolName, parentSymbol);

                // Get signature
                var signature = ExtractSignature(match, pattern);

                // Get metadata
                var metadata = ExtractMetadata(match, pattern);

                chunks.Add(new SemanticChunk
                {
                    Text = text.Trim(),
                    FilePath = filePath,
                    ChunkType = pattern.ChunkType,
                    SymbolName = symbolName,
                    ParentSymbol = parentSymbol,
                    FullyQualifiedName = fullyQualifiedName,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = Language,
                    Signature = signature,
                    Metadata = metadata,
                });
            }
        }

        // Sort by start line for consistent ordering
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    /// <summary>
    /// Gets the 1-based line number for a character index.
    /// </summary>
    protected static int GetLineNumber(string content, int charIndex)
    {
        var line = 1;
        for (var i = 0; i < charIndex && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Extracts the symbol name from a match.
    /// </summary>
    protected virtual string? ExtractSymbolName(Match match, DeclarationPattern pattern)
    {
        return match.Groups[pattern.NameGroup].Value;
    }

    /// <summary>
    /// Extracts the parent symbol from a match (for nested declarations).
    /// </summary>
    protected virtual string? ExtractParentSymbol(Match match, DeclarationPattern pattern)
    {
        if (string.IsNullOrEmpty(pattern.ParentGroup))
        {
            return null;
        }

        var group = match.Groups[pattern.ParentGroup];
        return group.Success ? group.Value : null;
    }

    /// <summary>
    /// Builds the fully qualified name for a symbol.
    /// </summary>
    protected virtual string? BuildFullyQualifiedName(Match match, DeclarationPattern pattern, string symbolName, string? parentSymbol)
    {
        if (parentSymbol is not null)
        {
            return $"{parentSymbol}.{symbolName}";
        }

        return symbolName;
    }

    /// <summary>
    /// Extracts the signature from a match.
    /// </summary>
    protected virtual string? ExtractSignature(Match match, DeclarationPattern pattern)
    {
        if (string.IsNullOrEmpty(pattern.SignatureGroup))
        {
            return null;
        }

        var group = match.Groups[pattern.SignatureGroup];
        return group.Success ? group.Value.Trim() : null;
    }

    /// <summary>
    /// Extracts metadata from a match.
    /// </summary>
    protected virtual Dictionary<string, string> ExtractMetadata(Match match, DeclarationPattern pattern)
    {
        var metadata = new Dictionary<string, string>();

        foreach (var (key, groupName) in pattern.MetadataGroups)
        {
            var group = match.Groups[groupName];
            if (group.Success && !string.IsNullOrEmpty(group.Value))
            {
                metadata[key] = group.Value.Trim();
            }
        }

        return metadata;
    }

    /// <summary>
    /// Represents a pattern for matching declarations in source code.
    /// </summary>
    protected record DeclarationPattern
    {
        /// <summary>Gets the regex pattern.</summary>
        public required Regex Regex { get; init; }

        /// <summary>Gets the chunk type for matched declarations.</summary>
        public required string ChunkType { get; init; }

        /// <summary>Gets the regex group name containing the symbol name.</summary>
        public required string NameGroup { get; init; }

        /// <summary>Gets the regex group name containing the parent symbol (optional).</summary>
        public string? ParentGroup { get; init; }

        /// <summary>Gets the regex group name containing the signature (optional).</summary>
        public string? SignatureGroup { get; init; }

        /// <summary>Gets the mapping of metadata keys to regex group names.</summary>
        public Dictionary<string, string> MetadataGroups { get; init; } = [];
    }
}
