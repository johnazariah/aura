// <copyright file="CodeGraphEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Problems;
using Aura.Foundation.Rag;

/// <summary>
/// Code Graph endpoints for querying code structure.
/// </summary>
public static class CodeGraphEndpoints
{
    /// <summary>
    /// Maps all code graph endpoints to the application.
    /// </summary>
    public static WebApplication MapCodeGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/api/graph/stats", GetStats);
        app.MapGet("/api/graph/implementations/{interfaceName}", FindImplementations);
        app.MapGet("/api/graph/callers/{methodName}", FindCallers);
        app.MapGet("/api/graph/members/{typeName}", GetMembers);
        app.MapGet("/api/graph/namespace/{namespaceName}", GetTypesInNamespace);
        app.MapGet("/api/graph/find/{name}", FindByName);

        return app;
    }

    private static async Task<IResult> GetStats(
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            var stats = await graphService.GetStatsAsync(repositoryPath, ct);

            return Results.Ok(new
            {
                totalNodes = stats.TotalNodes,
                totalEdges = stats.TotalEdges,
                nodesByType = stats.NodesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                edgesByType = stats.EdgesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                repositoryPath = stats.RepositoryPath
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> FindImplementations(
        string interfaceName,
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            var implementations = await graphService.FindImplementationsAsync(
                Uri.UnescapeDataString(interfaceName),
                repositoryPath,
                ct);

            return Results.Ok(new
            {
                interfaceName,
                count = implementations.Count,
                implementations = implementations.Select(n => new
                {
                    name = n.Name,
                    fullName = n.FullName,
                    filePath = n.FilePath,
                    lineNumber = n.LineNumber
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> FindCallers(
        string methodName,
        string? containingType,
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            var callers = await graphService.FindCallersAsync(
                Uri.UnescapeDataString(methodName),
                containingType,
                repositoryPath,
                ct);

            return Results.Ok(new
            {
                methodName,
                containingType,
                count = callers.Count,
                callers = callers.Select(n => new
                {
                    name = n.Name,
                    fullName = n.FullName,
                    signature = n.Signature,
                    filePath = n.FilePath,
                    lineNumber = n.LineNumber
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> GetMembers(
        string typeName,
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            var members = await graphService.GetTypeMembersAsync(
                Uri.UnescapeDataString(typeName),
                repositoryPath,
                ct);

            return Results.Ok(new
            {
                typeName,
                count = members.Count,
                members = members.Select(n => new
                {
                    name = n.Name,
                    nodeType = n.NodeType.ToString(),
                    signature = n.Signature,
                    modifiers = n.Modifiers,
                    lineNumber = n.LineNumber
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> GetTypesInNamespace(
        string namespaceName,
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            var types = await graphService.GetTypesInNamespaceAsync(
                Uri.UnescapeDataString(namespaceName),
                repositoryPath,
                ct);

            return Results.Ok(new
            {
                namespaceName,
                count = types.Count,
                types = types.Select(n => new
                {
                    name = n.Name,
                    fullName = n.FullName,
                    nodeType = n.NodeType.ToString(),
                    filePath = n.FilePath,
                    lineNumber = n.LineNumber
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> FindByName(
        string name,
        string? nodeType,
        string? repositoryPath,
        HttpContext context,
        ICodeGraphService graphService,
        CancellationToken ct)
    {
        try
        {
            Aura.Foundation.Data.Entities.CodeNodeType? typeFilter = null;
            if (!string.IsNullOrEmpty(nodeType) &&
                Enum.TryParse<Aura.Foundation.Data.Entities.CodeNodeType>(nodeType, true, out var parsed))
            {
                typeFilter = parsed;
            }

            var nodes = await graphService.FindNodesAsync(
                Uri.UnescapeDataString(name),
                typeFilter,
                repositoryPath,
                ct);

            return Results.Ok(new
            {
                name,
                nodeType,
                count = nodes.Count,
                nodes = nodes.Select(n => new
                {
                    id = n.Id,
                    name = n.Name,
                    fullName = n.FullName,
                    nodeType = n.NodeType.ToString(),
                    filePath = n.FilePath,
                    lineNumber = n.LineNumber,
                    signature = n.Signature,
                    modifiers = n.Modifiers
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }
}
