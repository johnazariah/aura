// <copyright file="IndexEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Background indexing status and health endpoints.
/// </summary>
public static class IndexEndpoints
{
    /// <summary>
    /// Maps all index endpoints to the application.
    /// </summary>
    public static WebApplication MapIndexEndpoints(this WebApplication app)
    {
        app.MapGet("/api/index/status", GetStatus);
        app.MapGet("/api/index/health", GetHealth);
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

    private static async Task<IResult> GetHealth(
        [FromQuery] string? workspacePath,
        AuraDbContext db,
        IGitService gitService,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return Results.BadRequest(new { error = "workspacePath query parameter is required" });
        }

        var normalizedPath = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var ragIndex = await db.IndexMetadata
            .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == IndexTypes.Rag)
            .FirstOrDefaultAsync(ct);

        var graphIndex = await db.IndexMetadata
            .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == IndexTypes.Graph)
            .FirstOrDefaultAsync(ct);

        string? currentCommitSha = null;
        DateTimeOffset? currentCommitAt = null;
        var isGitRepo = await gitService.IsRepositoryAsync(normalizedPath, ct);
        if (isGitRepo)
        {
            var headResult = await gitService.GetHeadCommitAsync(normalizedPath, ct);
            if (headResult.Success)
            {
                currentCommitSha = headResult.Value;
                var timestampResult = await gitService.GetCommitTimestampAsync(normalizedPath, currentCommitSha!, ct);
                if (timestampResult.Success)
                {
                    currentCommitAt = timestampResult.Value;
                }
            }
        }

        async Task<IndexHealthInfo> GetHealthInfo(IndexMetadata? index, string indexType)
        {
            if (index == null)
            {
                return new IndexHealthInfo
                {
                    IndexType = indexType,
                    Status = "not-indexed",
                    IndexedAt = null,
                    IndexedCommitSha = null,
                    CommitsBehind = null,
                    IsStale = true,
                    ItemCount = 0
                };
            }

            int? commitsBehind = null;
            bool isStale = false;

            if (isGitRepo && !string.IsNullOrEmpty(index.CommitSha) && !string.IsNullOrEmpty(currentCommitSha))
            {
                if (index.CommitSha != currentCommitSha)
                {
                    var countResult = await gitService.CountCommitsSinceAsync(normalizedPath, index.CommitSha, ct);
                    if (countResult.Success && countResult.Value >= 0)
                    {
                        commitsBehind = countResult.Value;
                        isStale = commitsBehind > 0;
                    }
                    else
                    {
                        isStale = true;
                    }
                }
            }
            else if (!isGitRepo)
            {
                isStale = index.IndexedAt < DateTimeOffset.UtcNow.AddHours(-24);
            }

            var status = isStale ? "stale" : "fresh";

            return new IndexHealthInfo
            {
                IndexType = indexType,
                Status = status,
                IndexedAt = index.IndexedAt,
                IndexedCommitSha = index.CommitSha,
                CommitsBehind = commitsBehind,
                IsStale = isStale,
                ItemCount = index.ItemsCreated
            };
        }

        var ragHealth = await GetHealthInfo(ragIndex, "rag");
        var graphHealth = await GetHealthInfo(graphIndex, "graph");

        string overallStatus;
        if (ragHealth.Status == "not-indexed" && graphHealth.Status == "not-indexed")
        {
            overallStatus = "not-indexed";
        }
        else if (ragHealth.IsStale || graphHealth.IsStale)
        {
            overallStatus = "stale";
        }
        else
        {
            overallStatus = "fresh";
        }

        return Results.Ok(new
        {
            workspacePath = normalizedPath,
            isGitRepository = isGitRepo,
            currentCommitSha = currentCommitSha?[..Math.Min(7, currentCommitSha?.Length ?? 0)],
            currentCommitAt,
            overallStatus,
            rag = ragHealth,
            graph = graphHealth
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
            state = status.State.ToString(),
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
