// <copyright file="IndexEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Foundation.Rag;

/// <summary>
/// Global background indexing status endpoints.
/// For per-workspace index operations, use /api/workspaces/{id}/index endpoints.
/// </summary>
public static class IndexEndpoints
{
    /// <summary>
    /// Maps global index endpoints.
    /// </summary>
    public static WebApplication MapIndexEndpoints(this WebApplication app)
    {
        app.MapGet("/api/index/status", GetStatus);
        app.MapGet("/api/index/jobs/{jobId:guid}", GetJobStatus);

        return app;
    }

    private static IResult GetStatus(IBackgroundIndexer backgroundIndexer)
    {
        var status = backgroundIndexer.GetStatus();
        return Results.Ok(new
        {
            queuedItems = status.QueuedItems,
            processedItems = status.ProcessedItems,
            failedItems = status.FailedItems,
            isProcessing = status.IsProcessing,
            activeJobs = status.ActiveJobs
        });
    }

    private static IResult GetJobStatus(Guid jobId, IBackgroundIndexer backgroundIndexer)
    {
        var status = backgroundIndexer.GetJobStatus(jobId);
        if (status is null)
        {
            return Results.NotFound(new { error = $"Job {jobId} not found" });
        }

        return Results.Ok(new
        {
            jobId = status.JobId,
            source = status.Source,
            state = status.State.ToString().ToLowerInvariant(),
            totalItems = status.TotalItems,
            processedItems = status.ProcessedItems,
            failedItems = status.FailedItems,
            progressPercent = status.ProgressPercent,
            startedAt = status.StartedAt,
            completedAt = status.CompletedAt,
            error = status.Error
        });
    }
}
