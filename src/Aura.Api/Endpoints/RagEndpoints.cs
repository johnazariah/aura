// <copyright file="RagEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Foundation.Rag;

/// <summary>
/// RAG (Retrieval-Augmented Generation) endpoints for indexing and querying.
/// </summary>
public static class RagEndpoints
{
    /// <summary>
    /// Maps all RAG endpoints to the application.
    /// </summary>
    public static WebApplication MapRagEndpoints(this WebApplication app)
    {
        app.MapPost("/api/rag/index", IndexContent);
        app.MapPost("/api/rag/query", QueryIndex);
        app.MapGet("/api/rag/stats", GetStats);
        app.MapDelete("/api/rag/{contentId}", RemoveContent);

        return app;
    }

    private static async Task<IResult> IndexContent(
        IndexContentRequest request,
        IRagService ragService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentType = Enum.TryParse<RagContentType>(request.ContentType, true, out var ct)
                ? ct
                : RagContentType.PlainText;

            var content = new RagContent(request.ContentId, request.Text, contentType)
            {
                SourcePath = request.SourcePath,
                Language = request.Language,
            };

            await ragService.IndexAsync(content, cancellationToken);

            return Results.Ok(new
            {
                success = true,
                contentId = request.ContentId,
                message = "Content indexed successfully"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> QueryIndex(
        RagQueryRequest request,
        IRagService ragService,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new RagQueryOptions
            {
                TopK = request.TopK ?? 5,
                MinScore = request.MinScore,
                SourcePathPrefix = request.SourcePathPrefix,
            };

            var results = await ragService.QueryAsync(request.Query, options, cancellationToken);

            return Results.Ok(new
            {
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
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetStats(IRagService ragService, CancellationToken cancellationToken)
    {
        try
        {
            var stats = await ragService.GetStatsAsync(cancellationToken);

            return Results.Ok(new
            {
                totalDocuments = stats.TotalDocuments,
                totalChunks = stats.TotalChunks,
                chunksByType = (stats.ByContentType ?? new Dictionary<RagContentType, int>()).ToDictionary(
                    kv => kv.Key.ToString(),
                    kv => kv.Value
                )
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RemoveContent(
        string contentId,
        IRagService ragService,
        CancellationToken cancellationToken)
    {
        try
        {
            var decodedId = Uri.UnescapeDataString(contentId);
            var removed = await ragService.RemoveAsync(decodedId, cancellationToken);

            if (!removed)
            {
                return Results.NotFound(new { success = false, error = "Content not found: " + decodedId });
            }

            return Results.Ok(new
            {
                success = true,
                contentId = decodedId,
                message = "Content removed from index"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }
}
