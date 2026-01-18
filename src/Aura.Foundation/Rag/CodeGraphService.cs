// <copyright file="CodeGraphService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for querying and managing the code graph stored in PostgreSQL.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CodeGraphService"/> class.
/// </remarks>
/// <param name="dbContext">The database context.</param>
/// <param name="logger">The logger.</param>
public class CodeGraphService(AuraDbContext dbContext, ILogger<CodeGraphService> logger) : ICodeGraphService
{
    private readonly AuraDbContext _dbContext = dbContext;
    private readonly ILogger<CodeGraphService> _logger = logger;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindImplementationsAsync(
        string interfaceName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding implementations of {InterfaceName} in repository {RepositoryPath}", interfaceName, repositoryPath);

        var query = from implementor in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on implementor.Id equals edge.SourceId
                    join iface in _dbContext.CodeNodes on edge.TargetId equals iface.Id
                    where edge.EdgeType == CodeEdgeType.Implements
                          && (iface.Name == interfaceName || iface.FullName == interfaceName)
                    select implementor;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindDerivedTypesAsync(
        string baseClassName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding types derived from {BaseClassName} in repository {RepositoryPath}", baseClassName, repositoryPath);

        var query = from derived in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on derived.Id equals edge.SourceId
                    join baseClass in _dbContext.CodeNodes on edge.TargetId equals baseClass.Id
                    where edge.EdgeType == CodeEdgeType.Inherits
                          && (baseClass.Name == baseClassName || baseClass.FullName == baseClassName)
                    select derived;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindCallersAsync(
        string methodName,
        string? containingTypeName = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Finding callers of {MethodName} in {ContainingType}, repository {RepositoryPath}",
            methodName,
            containingTypeName,
            repositoryPath);

        var calleeQuery = _dbContext.CodeNodes
            .Where(n => n.NodeType == CodeNodeType.Method && n.Name == methodName);

        if (!string.IsNullOrEmpty(containingTypeName))
        {
            // Filter by containing type through the Contains edge
            calleeQuery = from method in calleeQuery
                          join edge in _dbContext.CodeEdges on method.Id equals edge.TargetId
                          join containingType in _dbContext.CodeNodes on edge.SourceId equals containingType.Id
                          where edge.EdgeType == CodeEdgeType.Contains
                                && (containingType.Name == containingTypeName || containingType.FullName == containingTypeName)
                          select method;
        }

        var query = from caller in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on caller.Id equals edge.SourceId
                    join callee in calleeQuery on edge.TargetId equals callee.Id
                    where edge.EdgeType == CodeEdgeType.Calls
                    select caller;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindDependenciesAsync(
        string methodName,
        string? containingTypeName = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Finding dependencies of {MethodName} in {ContainingType}, repository {RepositoryPath}",
            methodName,
            containingTypeName,
            repositoryPath);

        var sourceQuery = _dbContext.CodeNodes
            .Where(n => n.NodeType == CodeNodeType.Method && n.Name == methodName);

        if (!string.IsNullOrEmpty(containingTypeName))
        {
            sourceQuery = from method in sourceQuery
                          join edge in _dbContext.CodeEdges on method.Id equals edge.TargetId
                          join containingType in _dbContext.CodeNodes on edge.SourceId equals containingType.Id
                          where edge.EdgeType == CodeEdgeType.Contains
                                && (containingType.Name == containingTypeName || containingType.FullName == containingTypeName)
                          select method;
        }

        var query = from dependency in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on dependency.Id equals edge.TargetId
                    join source in sourceQuery on edge.SourceId equals source.Id
                    where edge.EdgeType == CodeEdgeType.Calls || edge.EdgeType == CodeEdgeType.Uses
                    select dependency;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetTypeMembersAsync(
        string typeName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting members of type {TypeName} in repository {RepositoryPath}", typeName, repositoryPath);

        var query = from member in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on member.Id equals edge.TargetId
                    join type in _dbContext.CodeNodes on edge.SourceId equals type.Id
                    where edge.EdgeType == CodeEdgeType.Contains
                          && (type.Name == typeName || type.FullName == typeName)
                          && (member.NodeType == CodeNodeType.Method
                              || member.NodeType == CodeNodeType.Property
                              || member.NodeType == CodeNodeType.Field
                              || member.NodeType == CodeNodeType.Event
                              || member.NodeType == CodeNodeType.Constructor)
                    select member;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetTypesInNamespaceAsync(
        string namespaceName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting types in namespace {NamespaceName}, repository {RepositoryPath}", namespaceName, repositoryPath);

        var query = from type in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on type.Id equals edge.TargetId
                    join ns in _dbContext.CodeNodes on edge.SourceId equals ns.Id
                    where edge.EdgeType == CodeEdgeType.Declares
                          && (ns.Name == namespaceName || ns.FullName == namespaceName)
                          && (type.NodeType == CodeNodeType.Class
                              || type.NodeType == CodeNodeType.Interface
                              || type.NodeType == CodeNodeType.Record
                              || type.NodeType == CodeNodeType.Struct
                              || type.NodeType == CodeNodeType.Enum)
                    select type;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetProjectReferencesAsync(
        string projectName,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting references for project {ProjectName}, repository {RepositoryPath}", projectName, repositoryPath);

        var query = from referenced in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on referenced.Id equals edge.TargetId
                    join project in _dbContext.CodeNodes on edge.SourceId equals project.Id
                    where edge.EdgeType == CodeEdgeType.References
                          && project.NodeType == CodeNodeType.Project
                          && project.Name == projectName
                    select referenced;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindNodesAsync(
        string name,
        CodeNodeType? nodeType = null,
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding nodes with name {Name}, type {NodeType}, repository {RepositoryPath}", name, nodeType, repositoryPath);

        IQueryable<CodeNode> query = _dbContext.CodeNodes;

        // Only filter by name if a non-empty name is provided
        if (!string.IsNullOrEmpty(name))
        {
            // Use case-insensitive matching for better search results
            // Prefer exact match on Name, but also match FullName ending with the name
            query = query.Where(n =>
                EF.Functions.ILike(n.Name, name) ||
                EF.Functions.ILike(n.FullName!, name) ||
                EF.Functions.ILike(n.FullName!, $"%.{name}"));
        }

        if (nodeType.HasValue)
        {
            query = query.Where(n => n.NodeType == nodeType.Value);
        }

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            query = query.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ClearRepositoryGraphAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing code graph for repository {RepositoryPath}", repositoryPath);

        var normalizedPath = PathNormalizer.Normalize(repositoryPath);

        // Delete edges first (foreign key constraints)
        var edgesToDelete = await _dbContext.CodeEdges
            .Where(e => EF.Functions.ILike(e.Source!.RepositoryPath!, normalizedPath))
            .ToListAsync(cancellationToken);

        _dbContext.CodeEdges.RemoveRange(edgesToDelete);

        // Then delete nodes
        var nodesToDelete = await _dbContext.CodeNodes
            .Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath))
            .ToListAsync(cancellationToken);

        _dbContext.CodeNodes.RemoveRange(nodesToDelete);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cleared {EdgeCount} edges and {NodeCount} nodes from repository {RepositoryPath}",
            edgesToDelete.Count,
            nodesToDelete.Count,
            repositoryPath);
    }

    /// <inheritdoc/>
    public async Task<CodeNode> AddNodeAsync(CodeNode node, CancellationToken cancellationToken = default)
    {
        _dbContext.CodeNodes.Add(node);
        return node;
    }

    /// <inheritdoc/>
    public async Task<CodeEdge> AddEdgeAsync(CodeEdge edge, CancellationToken cancellationToken = default)
    {
        _dbContext.CodeEdges.Add(edge);
        return edge;
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CodeGraphStats> GetStatsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default)
    {
        IQueryable<CodeNode> nodesQuery = _dbContext.CodeNodes;

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            var normalizedPath = PathNormalizer.Normalize(repositoryPath);
            nodesQuery = nodesQuery.Where(n => EF.Functions.ILike(n.RepositoryPath!, normalizedPath));
        }

        var nodesByType = await nodesQuery
            .GroupBy(n => n.NodeType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);

        // For edges, filter by source node's repository path (edges don't have their own repository)
        var edgesQuery = !string.IsNullOrEmpty(repositoryPath)
            ? from e in _dbContext.CodeEdges
              join n in _dbContext.CodeNodes on e.SourceId equals n.Id
              where EF.Functions.ILike(n.RepositoryPath!, PathNormalizer.Normalize(repositoryPath))
              select e
            : _dbContext.CodeEdges.AsQueryable();

        var edgesByType = await edgesQuery
            .GroupBy(e => e.EdgeType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);

        return new CodeGraphStats
        {
            TotalNodes = nodesByType.Values.Sum(),
            TotalEdges = edgesByType.Values.Sum(),
            NodesByType = nodesByType,
            EdgesByType = edgesByType,
            RepositoryPath = repositoryPath
        };
    }
}
