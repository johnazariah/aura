// <copyright file="ICodeGraphService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using Aura.Foundation.Data.Entities;

/// <summary>
/// Service for querying and managing the code graph for structural code understanding.
/// </summary>
public interface ICodeGraphService
{
    /// <summary>
    /// Finds all types that implement a given interface.
    /// </summary>
    /// <param name="interfaceName">The interface name (simple or fully qualified).</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The implementing types.</returns>
    Task<IReadOnlyList<CodeNode>> FindImplementationsAsync(
        string interfaceName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all types that inherit from a given base class.
    /// </summary>
    /// <param name="baseClassName">The base class name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The derived types.</returns>
    Task<IReadOnlyList<CodeNode>> FindDerivedTypesAsync(
        string baseClassName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all methods that call a given method.
    /// </summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="containingTypeName">Optional containing type name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calling methods.</returns>
    Task<IReadOnlyList<CodeNode>> FindCallersAsync(
        string methodName,
        string? containingTypeName = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all methods/types that a given method calls or uses.
    /// </summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="containingTypeName">Optional containing type name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The called methods and used types.</returns>
    Task<IReadOnlyList<CodeNode>> FindDependenciesAsync(
        string methodName,
        string? containingTypeName = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all members of a type (methods, properties, fields, etc.).
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The type's members.</returns>
    Task<IReadOnlyList<CodeNode>> GetTypeMembersAsync(
        string typeName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all types in a namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The types in the namespace.</returns>
    Task<IReadOnlyList<CodeNode>> GetTypesInNamespaceAsync(
        string namespaceName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets project references (what projects does a project depend on).
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The referenced projects.</returns>
    Task<IReadOnlyList<CodeNode>> GetProjectReferencesAsync(
        string projectName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a node by name and optional type.
    /// </summary>
    /// <param name="name">The node name (simple or fully qualified).</param>
    /// <param name="nodeType">Optional node type filter.</param>
    /// <param name="repositoryPath">Optional repository path to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching nodes.</returns>
    Task<IReadOnlyList<CodeNode>> FindNodesAsync(
        string name,
        CodeNodeType? nodeType = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the graph for a repository (before re-indexing).
    /// </summary>
    /// <param name="repositoryPath">The repository path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearRepositoryGraphAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The added node.</returns>
    Task<CodeNode> AddNodeAsync(CodeNode node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an edge to the graph.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The added edge.</returns>
    Task<CodeEdge> AddEdgeAsync(CodeEdge edge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the code graph.
    /// </summary>
    /// <param name="repositoryPath">Optional repository path to filter stats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Code graph statistics.</returns>
    Task<CodeGraphStats> GetStatsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the code graph.
/// </summary>
public record CodeGraphStats
{
    /// <summary>Gets the total number of nodes in the graph.</summary>
    public int TotalNodes { get; init; }

    /// <summary>Gets the total number of edges in the graph.</summary>
    public int TotalEdges { get; init; }

    /// <summary>Gets node counts by type.</summary>
    public Dictionary<CodeNodeType, int> NodesByType { get; init; } = new();

    /// <summary>Gets edge counts by type.</summary>
    public Dictionary<CodeEdgeType, int> EdgesByType { get; init; } = new();

    /// <summary>Gets the repository path if filtered.</summary>
    public string? RepositoryPath { get; init; }
}
