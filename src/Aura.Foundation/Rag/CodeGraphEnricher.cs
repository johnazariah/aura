// <copyright file="CodeGraphEnricher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using Aura.Foundation.Data.Entities;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Enriches agent context with Code Graph structural information.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CodeGraphEnricher"/> class.
/// </remarks>
public sealed partial class CodeGraphEnricher(ICodeGraphService codeGraph, ILogger<CodeGraphEnricher> logger) : ICodeGraphEnricher
{
    private readonly ICodeGraphService _codeGraph = codeGraph;
    private readonly ILogger<CodeGraphEnricher> _logger = logger;

    /// <inheritdoc/>
    public async Task<CodeGraphEnrichment> EnrichAsync(
        string prompt,
        string? workspacePath = null,
        CodeGraphEnrichmentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CodeGraphEnrichmentOptions();

        // Extract potential type/method names from prompt
        var symbols = ExtractSymbolNames(prompt).ToList();

        if (symbols.Count == 0)
        {
            _logger.LogDebug("No symbol names extracted from prompt");
            return new CodeGraphEnrichment(string.Empty, [], []);
        }

        _logger.LogDebug("Extracted {Count} potential symbols: {Symbols}", symbols.Count, string.Join(", ", symbols));

        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();
        var processedNodeIds = new HashSet<Guid>();

        // Find matching nodes for each symbol
        foreach (var symbol in symbols.Take(5)) // Limit to avoid query explosion
        {
            try
            {
                var matchingNodes = await _codeGraph.FindNodesAsync(
                    symbol,
                    nodeType: null,
                    repositoryPath: workspacePath,
                    cancellationToken);

                foreach (var node in matchingNodes.Take(options.MaxNodes))
                {
                    if (!processedNodeIds.Add(node.Id))
                    {
                        continue;
                    }

                    nodes.Add(node);

                    // Get implementations for interfaces
                    if (options.IncludeImplementations && node.NodeType == CodeNodeType.Interface)
                    {
                        var impls = await _codeGraph.FindImplementationsAsync(
                            node.Name,
                            workspacePath,
                            cancellationToken);
                        foreach (var impl in impls.Take(5))
                        {
                            if (processedNodeIds.Add(impl.Id))
                            {
                                nodes.Add(impl);
                            }
                        }
                    }

                    // Get derived types for classes
                    if (options.IncludeTypeHierarchy && node.NodeType == CodeNodeType.Class)
                    {
                        var derived = await _codeGraph.FindDerivedTypesAsync(
                            node.Name,
                            workspacePath,
                            cancellationToken);
                        foreach (var d in derived.Take(5))
                        {
                            if (processedNodeIds.Add(d.Id))
                            {
                                nodes.Add(d);
                            }
                        }
                    }

                    // Get type members
                    if (options.IncludeTypeMembers && IsTypeNode(node.NodeType))
                    {
                        var members = await _codeGraph.GetTypeMembersAsync(
                            node.Name,
                            workspacePath,
                            cancellationToken);
                        foreach (var member in members.Take(10))
                        {
                            if (processedNodeIds.Add(member.Id))
                            {
                                nodes.Add(member);
                            }
                        }
                    }

                    // Get callers for methods
                    if (options.IncludeCallGraph && node.NodeType == CodeNodeType.Method)
                    {
                        var callers = await _codeGraph.FindCallersAsync(
                            node.Name,
                            containingTypeName: null,
                            repositoryPath: workspacePath,
                            cancellationToken);
                        foreach (var caller in callers.Take(5))
                        {
                            if (processedNodeIds.Add(caller.Id))
                            {
                                nodes.Add(caller);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying Code Graph for symbol: {Symbol}", symbol);
            }
        }

        // Collect edges from the nodes we found
        foreach (var node in nodes)
        {
            edges.AddRange(node.OutgoingEdges);
            edges.AddRange(node.IncomingEdges);
        }

        // Deduplicate edges
        edges = edges.DistinctBy(e => e.Id).ToList();

        // Format as context string
        var context = FormatCodeGraphContext(nodes, edges);

        _logger.LogInformation(
            "Code Graph enrichment: {NodeCount} nodes, {EdgeCount} edges",
            nodes.Count,
            edges.Count);

        return new CodeGraphEnrichment(context, nodes, edges);
    }

    private static bool IsTypeNode(CodeNodeType nodeType) =>
        nodeType is CodeNodeType.Class or CodeNodeType.Interface or CodeNodeType.Record or CodeNodeType.Struct;

    /// <summary>
    /// Extracts potential symbol names from a prompt.
    /// Looks for PascalCase identifiers that might be type or method names.
    /// </summary>
    private static IEnumerable<string> ExtractSymbolNames(string prompt)
    {
        // Match PascalCase words (e.g., WorkflowService, ICodeGraphService)
        // Also match I-prefixed interfaces
        var matches = PascalCasePattern().Matches(prompt);

        return matches
            .Select(m => m.Value)
            .Where(s => s.Length >= 3) // Skip very short matches
            .Distinct()
            .OrderByDescending(s => s.Length); // Prefer longer, more specific names
    }

    [GeneratedRegex(@"\b(I?[A-Z][a-z]+(?:[A-Z][a-z0-9]+)+)\b")]
    private static partial Regex PascalCasePattern();

    private static string FormatCodeGraphContext(
        IReadOnlyList<CodeNode> nodes,
        IReadOnlyList<CodeEdge> edges)
    {
        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Code Structure");
        sb.AppendLine();

        // Group by type
        var types = nodes
            .Where(n => IsTypeNode(n.NodeType))
            .OrderBy(n => n.Name)
            .ToList();

        var methods = nodes
            .Where(n => n.NodeType == CodeNodeType.Method)
            .ToList();

        var properties = nodes
            .Where(n => n.NodeType == CodeNodeType.Property)
            .ToList();

        // Format types with their members
        foreach (var type in types)
        {
            sb.AppendLine($"### {type.NodeType}: {type.Name}");

            if (!string.IsNullOrEmpty(type.FullName))
            {
                sb.AppendLine($"Namespace: {GetNamespace(type.FullName)}");
            }

            if (!string.IsNullOrEmpty(type.FilePath))
            {
                var displayPath = type.FilePath;
                if (type.LineNumber.HasValue)
                {
                    displayPath += $":{type.LineNumber}";
                }

                sb.AppendLine($"File: {displayPath}");
            }

            if (!string.IsNullOrEmpty(type.Modifiers))
            {
                sb.AppendLine($"Modifiers: {type.Modifiers}");
            }

            // Find members of this type by matching file path or name pattern
            var typeMembers = methods.Concat(properties)
                .Where(m => m.FilePath == type.FilePath || (m.FullName?.Contains(type.Name + ".") ?? false))
                .ToList();

            if (typeMembers.Count != 0)
            {
                sb.AppendLine();
                sb.AppendLine("Members:");
                foreach (var member in typeMembers.Take(15))
                {
                    var sig = member.Signature ?? member.Name;
                    sb.AppendLine($"  - {member.NodeType}: {sig}");
                }
            }

            sb.AppendLine();
        }

        // Format edges (relationships)
        if (edges.Count != 0)
        {
            sb.AppendLine("### Relationships");
            sb.AppendLine();

            var groupedEdges = edges
                .GroupBy(e => e.EdgeType)
                .OrderBy(g => g.Key);

            foreach (var group in groupedEdges)
            {
                sb.AppendLine($"**{FormatEdgeType(group.Key)}:**");
                foreach (var edge in group.Take(10))
                {
                    var sourceName = nodes.FirstOrDefault(n => n.Id == edge.SourceId)?.Name ?? "?";
                    var targetName = nodes.FirstOrDefault(n => n.Id == edge.TargetId)?.Name ?? "?";
                    sb.AppendLine($"  - {sourceName} → {targetName}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string GetNamespace(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot > 0 ? fullName[..lastDot] : fullName;
    }

    private static string FormatEdgeType(CodeEdgeType edgeType) => edgeType switch
    {
        CodeEdgeType.Implements => "Implements",
        CodeEdgeType.Inherits => "Inherits From",
        CodeEdgeType.Calls => "Calls",
        CodeEdgeType.References => "References",
        CodeEdgeType.Contains => "Contains",
        CodeEdgeType.Uses => "Uses",
        CodeEdgeType.Overrides => "Overrides",
        CodeEdgeType.Declares => "Declares",
        _ => edgeType.ToString(),
    };
}
