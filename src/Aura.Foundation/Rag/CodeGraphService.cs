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
public class CodeGraphService : ICodeGraphService
{
    private readonly AuraDbContext _dbContext;
    private readonly ILogger<CodeGraphService> _logger;

    /// <summary>
    /// Normalizes a path for consistent storage and lookup (lowercase, forward slashes).
    /// </summary>
    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').ToLowerInvariant();

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGraphService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    public CodeGraphService(AuraDbContext dbContext, ILogger<CodeGraphService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindImplementationsAsync(
        string interfaceName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding implementations of {InterfaceName} in workspace {WorkspacePath}", interfaceName, workspacePath);

        var query = from implementor in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on implementor.Id equals edge.SourceId
                    join iface in _dbContext.CodeNodes on edge.TargetId equals iface.Id
                    where edge.EdgeType == CodeEdgeType.Implements
                          && (iface.Name == interfaceName || iface.FullName == interfaceName)
                    select implementor;

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindDerivedTypesAsync(
        string baseClassName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding types derived from {BaseClassName} in workspace {WorkspacePath}", baseClassName, workspacePath);

        var query = from derived in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on derived.Id equals edge.SourceId
                    join baseClass in _dbContext.CodeNodes on edge.TargetId equals baseClass.Id
                    where edge.EdgeType == CodeEdgeType.Inherits
                          && (baseClass.Name == baseClassName || baseClass.FullName == baseClassName)
                    select derived;

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindCallersAsync(
        string methodName,
        string? containingTypeName = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Finding callers of {MethodName} in {ContainingType}, workspace {WorkspacePath}",
            methodName,
            containingTypeName,
            workspacePath);

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

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindDependenciesAsync(
        string methodName,
        string? containingTypeName = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Finding dependencies of {MethodName} in {ContainingType}, workspace {WorkspacePath}",
            methodName,
            containingTypeName,
            workspacePath);

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

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.Distinct().ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetTypeMembersAsync(
        string typeName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting members of type {TypeName} in workspace {WorkspacePath}", typeName, workspacePath);

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

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetTypesInNamespaceAsync(
        string namespaceName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting types in namespace {NamespaceName}, workspace {WorkspacePath}", namespaceName, workspacePath);

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

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> GetProjectReferencesAsync(
        string projectName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting references for project {ProjectName}, workspace {WorkspacePath}", projectName, workspacePath);

        var query = from referenced in _dbContext.CodeNodes
                    join edge in _dbContext.CodeEdges on referenced.Id equals edge.TargetId
                    join project in _dbContext.CodeNodes on edge.SourceId equals project.Id
                    where edge.EdgeType == CodeEdgeType.References
                          && project.NodeType == CodeNodeType.Project
                          && project.Name == projectName
                    select referenced;

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CodeNode>> FindNodesAsync(
        string name,
        CodeNodeType? nodeType = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding nodes with name {Name}, type {NodeType}, workspace {WorkspacePath}", name, nodeType, workspacePath);

        IQueryable<CodeNode> query = _dbContext.CodeNodes;

        // Only filter by name if a non-empty name is provided
        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(n => n.Name == name || n.FullName == name);
        }

        if (nodeType.HasValue)
        {
            query = query.Where(n => n.NodeType == nodeType.Value);
        }

        if (!string.IsNullOrEmpty(workspacePath))
        {
            var normalizedPath = NormalizePath(workspacePath);
            query = query.Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ClearWorkspaceGraphAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing code graph for workspace {WorkspacePath}", workspacePath);

        var normalizedPath = NormalizePath(workspacePath);

        // Delete edges first (foreign key constraints)
        var edgesToDelete = await _dbContext.CodeEdges
            .Where(e => EF.Functions.ILike(e.Source!.WorkspacePath!, normalizedPath))
            .ToListAsync(cancellationToken);

        _dbContext.CodeEdges.RemoveRange(edgesToDelete);

        // Then delete nodes
        var nodesToDelete = await _dbContext.CodeNodes
            .Where(n => EF.Functions.ILike(n.WorkspacePath!, normalizedPath))
            .ToListAsync(cancellationToken);

        _dbContext.CodeNodes.RemoveRange(nodesToDelete);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cleared {EdgeCount} edges and {NodeCount} nodes from workspace {WorkspacePath}",
            edgesToDelete.Count,
            nodesToDelete.Count,
            workspacePath);
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
}
