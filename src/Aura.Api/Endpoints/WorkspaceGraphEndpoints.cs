// <copyright file="WorkspaceGraphEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Problems;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Workspace code graph endpoints.
/// All paths are: /api/workspaces/{id}/graph/...
/// </summary>
public static class WorkspaceGraphEndpoints
{
    /// <summary>
    /// Maps workspace graph endpoints.
    /// </summary>
    public static WebApplication MapWorkspaceGraphEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId}/graph");

        group.MapGet("/", GetGraphStats);
        group.MapDelete("/", ClearGraph);
        group.MapGet("/implementations/{interfaceName}", FindImplementations);
        group.MapGet("/callers/{methodName}", FindCallers);
        group.MapGet("/members/{typeName}", GetMembers);
        group.MapGet("/namespaces/{namespaceName}", GetTypesInNamespace);
        group.MapGet("/symbols/{name}", FindSymbols);

        return app;
    }

    private static async Task<IResult> GetGraphStats(
        string workspaceId,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            var stats = await graphService.GetStatsAsync(workspace.CanonicalPath, ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                totalNodes = stats.TotalNodes,
                totalEdges = stats.TotalEdges,
                nodesByType = stats.NodesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                edgesByType = stats.EdgesByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<IResult> ClearGraph(
        string workspaceId,
        AuraDbContext db,
        ICodeGraphService codeGraphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        await codeGraphService.ClearRepositoryGraphAsync(workspace.CanonicalPath, ct);

        // Clear graph index metadata
        var metadataToDelete = await db.IndexMetadata
            .Where(i => i.WorkspacePath == workspace.CanonicalPath && i.IndexType == IndexTypes.Graph)
            .ToListAsync(ct);
        db.IndexMetadata.RemoveRange(metadataToDelete);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            workspaceId = workspace.Id,
            message = "Code graph cleared. Workspace preserved."
        });
    }

    private static async Task<IResult> FindImplementations(
        string workspaceId,
        string interfaceName,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            var implementations = await graphService.FindImplementationsAsync(
                Uri.UnescapeDataString(interfaceName),
                workspace.CanonicalPath,
                ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                interfaceName = Uri.UnescapeDataString(interfaceName),
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
        string workspaceId,
        string methodName,
        string? containingType,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            var callers = await graphService.FindCallersAsync(
                Uri.UnescapeDataString(methodName),
                containingType,
                workspace.CanonicalPath,
                ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                methodName = Uri.UnescapeDataString(methodName),
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
        string workspaceId,
        string typeName,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            var members = await graphService.GetTypeMembersAsync(
                Uri.UnescapeDataString(typeName),
                workspace.CanonicalPath,
                ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                typeName = Uri.UnescapeDataString(typeName),
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
        string workspaceId,
        string namespaceName,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            var types = await graphService.GetTypesInNamespaceAsync(
                Uri.UnescapeDataString(namespaceName),
                workspace.CanonicalPath,
                ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                namespaceName = Uri.UnescapeDataString(namespaceName),
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

    private static async Task<IResult> FindSymbols(
        string workspaceId,
        string name,
        string? nodeType,
        AuraDbContext db,
        ICodeGraphService graphService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        try
        {
            CodeNodeType? parsedNodeType = null;
            if (!string.IsNullOrEmpty(nodeType) && Enum.TryParse<CodeNodeType>(nodeType, true, out var parsed))
            {
                parsedNodeType = parsed;
            }

            var nodes = await graphService.FindNodesAsync(
                Uri.UnescapeDataString(name),
                parsedNodeType,
                workspace.CanonicalPath,
                ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                query = Uri.UnescapeDataString(name),
                nodeType,
                count = nodes.Count,
                symbols = nodes.Select(n => new
                {
                    name = n.Name,
                    fullName = n.FullName,
                    nodeType = n.NodeType.ToString(),
                    filePath = n.FilePath,
                    lineNumber = n.LineNumber,
                    signature = n.Signature
                })
            });
        }
        catch (Exception ex)
        {
            return Problem.InternalError(ex.Message, context);
        }
    }

    private static async Task<Workspace?> FindWorkspaceAsync(string idOrPath, AuraDbContext db, CancellationToken ct)
    {
        if (WorkspaceIdGenerator.IsValidId(idOrPath))
        {
            return await db.Workspaces.FindAsync([idOrPath], ct);
        }

        var decodedPath = Uri.UnescapeDataString(idOrPath);
        var workspaceId = WorkspaceIdGenerator.GenerateId(decodedPath);
        return await db.Workspaces.FindAsync([workspaceId], ct);
    }
}
