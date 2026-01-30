// <copyright file="WorkspaceSearchEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Api.Problems;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;

/// <summary>
/// Workspace search endpoints for RAG queries scoped to a workspace.
/// All paths are: /api/workspaces/{id}/search/...
/// </summary>
public static class WorkspaceSearchEndpoints
{
    /// <summary>
    /// Maps workspace search endpoints.
    /// </summary>
    public static WebApplication MapWorkspaceSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/workspaces/{workspaceId}/search", SearchWorkspace);

        return app;
    }

    private static async Task<IResult> SearchWorkspace(
        string workspaceId,
        WorkspaceSearchRequest request,
        AuraDbContext db,
        IRagService ragService,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "query is required" });
        }

        try
        {
            var options = new RagQueryOptions
            {
                TopK = request.TopK ?? 5,
                MinScore = request.MinScore,
                SourcePathPrefix = workspace.CanonicalPath
            };

            var results = await ragService.QueryAsync(request.Query, options, ct);

            return Results.Ok(new
            {
                workspaceId = workspace.Id,
                query = request.Query,
                resultCount = results.Count,
                results = results.Select(r => new
                {
                    contentId = r.ContentId,
                    chunkIndex = r.ChunkIndex,
                    text = r.Text,
                    score = r.Score,
                    sourcePath = r.SourcePath,
                    contentType = r.ContentType.ToString()
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
