// <copyright file="ITreeBuilderService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Foundation.Rag;

/// <summary>
/// Service for building hierarchical tree views from RAG chunks.
/// </summary>
public interface ITreeBuilderService
{
    /// <summary>
    /// Builds a hierarchical tree from code chunks.
    /// </summary>
    /// <param name="chunks">The chunks to build the tree from.</param>
    /// <param name="pattern">Optional filter pattern.</param>
    /// <param name="maxDepth">Maximum tree depth (default: 2).</param>
    /// <param name="detail">Level of detail for output.</param>
    /// <returns>A tree structure with nodes.</returns>
    TreeResult BuildTree(
        IReadOnlyList<TreeChunk> chunks,
        string? pattern = null,
        int maxDepth = 2,
        TreeDetail detail = TreeDetail.Min);

    /// <summary>
    /// Gets the full content for a specific node.
    /// </summary>
    /// <param name="chunks">The chunks to search.</param>
    /// <param name="nodeId">The node ID (format: "{type}:{path}:{symbol}").</param>
    /// <returns>The node with full content, or null if not found.</returns>
    TreeNodeContent? GetNode(IReadOnlyList<TreeChunk> chunks, string nodeId);
}

/// <summary>
/// Detail level for tree output.
/// </summary>
public enum TreeDetail
{
    /// <summary>Minimal output - names only.</summary>
    Min,

    /// <summary>Maximum output - includes signatures.</summary>
    Max,
}

/// <summary>
/// Result of building a tree.
/// </summary>
public sealed record TreeResult
{
    /// <summary>Gets or sets the root path.</summary>
    public required string RootPath { get; init; }

    /// <summary>Gets or sets the root nodes.</summary>
    public required IReadOnlyList<TreeNode> Nodes { get; init; }

    /// <summary>Gets or sets the total node count.</summary>
    public int TotalNodes { get; init; }

    /// <summary>Gets or sets whether the tree was truncated.</summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// A node in the tree.
/// </summary>
public sealed record TreeNode
{
    /// <summary>Gets or sets the unique node ID (format: "{type}:{path}:{symbol}").</summary>
    public required string NodeId { get; init; }

    /// <summary>Gets or sets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the node type (file, namespace, class, interface, method, property, function).</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the file path (relative to workspace).</summary>
    public required string Path { get; init; }

    /// <summary>Gets or sets the signature (for detail=max).</summary>
    public string? Signature { get; init; }

    /// <summary>Gets or sets the start line.</summary>
    public int? LineStart { get; init; }

    /// <summary>Gets or sets the end line.</summary>
    public int? LineEnd { get; init; }

    /// <summary>Gets or sets child nodes.</summary>
    public IReadOnlyList<TreeNode>? Children { get; init; }
}

/// <summary>
/// Full content for a specific node.
/// </summary>
public sealed record TreeNodeContent
{
    /// <summary>Gets or sets the node ID.</summary>
    public required string NodeId { get; init; }

    /// <summary>Gets or sets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the node type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets the file path.</summary>
    public required string Path { get; init; }

    /// <summary>Gets or sets the start line.</summary>
    public int? LineStart { get; init; }

    /// <summary>Gets or sets the end line.</summary>
    public int? LineEnd { get; init; }

    /// <summary>Gets or sets the full content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets or sets additional metadata.</summary>
    public TreeNodeMetadata? Metadata { get; init; }
}

/// <summary>
/// Metadata for a tree node.
/// </summary>
public sealed record TreeNodeMetadata
{
    /// <summary>Gets or sets the signature.</summary>
    public string? Signature { get; init; }

    /// <summary>Gets or sets the docstring or XML documentation.</summary>
    public string? Docstring { get; init; }

    /// <summary>Gets or sets the language.</summary>
    public string? Language { get; init; }
}
