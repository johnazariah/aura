// <copyright file="ICodeGraphEnricher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using Aura.Foundation.Data.Entities;

/// <summary>
/// Options for Code Graph enrichment.
/// </summary>
public sealed record CodeGraphEnrichmentOptions
{
    /// <summary>Gets or sets the maximum number of nodes to include in context.</summary>
    public int MaxNodes { get; init; } = 10;

    /// <summary>Gets or sets whether to include callers/callees for methods.</summary>
    public bool IncludeCallGraph { get; init; } = true;

    /// <summary>Gets or sets whether to include type hierarchy (base/derived).</summary>
    public bool IncludeTypeHierarchy { get; init; } = true;

    /// <summary>Gets or sets whether to include implementations for interfaces.</summary>
    public bool IncludeImplementations { get; init; } = true;

    /// <summary>Gets or sets whether to include type members.</summary>
    public bool IncludeTypeMembers { get; init; } = true;
}

/// <summary>
/// Result of Code Graph enrichment.
/// </summary>
/// <param name="FormattedContext">Formatted context string for the LLM.</param>
/// <param name="Nodes">Relevant code nodes found.</param>
/// <param name="Edges">Relevant code edges found.</param>
public sealed record CodeGraphEnrichment(
    string FormattedContext,
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);

/// <summary>
/// Enriches agent context with Code Graph structural information.
/// </summary>
public interface ICodeGraphEnricher
{
    /// <summary>
    /// Extracts relevant Code Graph context for a prompt.
    /// </summary>
    /// <param name="prompt">The user prompt to analyze for symbol references.</param>
    /// <param name="workspacePath">Optional workspace path to filter by.</param>
    /// <param name="options">Enrichment options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Code Graph enrichment result.</returns>
    Task<CodeGraphEnrichment> EnrichAsync(
        string prompt,
        string? workspacePath = null,
        CodeGraphEnrichmentOptions? options = null,
        CancellationToken cancellationToken = default);
}
