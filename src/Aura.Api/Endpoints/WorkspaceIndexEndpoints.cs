// <copyright file="WorkspaceIndexEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Problems;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Workspace index endpoints for managing RAG index per workspace.
/// All paths are: /api/workspaces/{id}/index/...
/// </summary>
public static class WorkspaceIndexEndpoints
{
    /// <summary>
    /// Maps workspace index endpoints.
    /// </summary>
    public static WebApplication MapWorkspaceIndexEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId}/index");

        group.MapGet("/", GetIndexStatus);
        group.MapPost("/", TriggerReindex);
        group.MapDelete("/", ClearIndex);
        group.MapGet("/jobs", ListJobs);
        group.MapGet("/jobs/{jobId:guid}", GetJobStatus);

        return app;
    }

    private static async Task<IResult> GetIndexStatus(
        string workspaceId,
        AuraDbContext db,
        IRagService ragService,
        IGitService gitService,
        IBackgroundIndexer backgroundIndexer,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        var normalizedPath = workspace.CanonicalPath;

        // Get RAG stats
        var ragStats = await ragService.GetDirectoryStatsAsync(normalizedPath, ct);

        // Get index metadata
        var ragIndex = await db.IndexMetadata
            .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == IndexTypes.Rag)
            .FirstOrDefaultAsync(ct);

        var graphIndex = await db.IndexMetadata
            .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == IndexTypes.Graph)
            .FirstOrDefaultAsync(ct);

        // Check git status for staleness
        string? currentCommitSha = null;
        DateTimeOffset? currentCommitAt = null;
        var isGitRepo = await gitService.IsRepositoryAsync(normalizedPath, ct);
        if (isGitRepo)
        {
            var headResult = await gitService.GetHeadCommitAsync(normalizedPath, ct);
            if (headResult.Success && headResult.Value is not null)
            {
                currentCommitSha = headResult.Value;
                var timestampResult = await gitService.GetCommitTimestampAsync(normalizedPath, currentCommitSha, ct);
                if (timestampResult.Success)
                {
                    currentCommitAt = timestampResult.Value;
                }
            }
        }

        // Calculate health status
        var (ragHealth, ragCommitsBehind) = await CalculateHealthAsync(ragIndex, currentCommitSha, normalizedPath, isGitRepo, gitService, ct);
        var (graphHealth, graphCommitsBehind) = await CalculateHealthAsync(graphIndex, currentCommitSha, normalizedPath, isGitRepo, gitService, ct);

        string overallStatus;
        if (ragHealth == "not-indexed" && graphHealth == "not-indexed")
        {
            overallStatus = "not-indexed";
        }
        else if (ragHealth == "stale" || graphHealth == "stale")
        {
            overallStatus = "stale";
        }
        else
        {
            overallStatus = "fresh";
        }

        // Check for active jobs
        var activeJob = backgroundIndexer.GetActiveJobs()
            .FirstOrDefault(j =>
                (j.State == IndexJobState.Queued || j.State == IndexJobState.Processing) &&
                string.Equals(
                    Path.GetFullPath(j.Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(normalizedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase));

        return Results.Ok(new
        {
            workspaceId = workspace.Id,
            workspacePath = normalizedPath,
            status = overallStatus,
            isGitRepository = isGitRepo,
            currentCommitSha = currentCommitSha?[..Math.Min(7, currentCommitSha?.Length ?? 0)],
            currentCommitAt,
            rag = new
            {
                status = ragHealth,
                files = ragStats?.FileCount ?? 0,
                chunks = ragStats?.ChunkCount ?? 0,
                indexedAt = ragIndex?.IndexedAt,
                indexedCommitSha = ragIndex?.CommitSha?[..Math.Min(7, ragIndex?.CommitSha?.Length ?? 0)],
                commitsBehind = ragCommitsBehind
            },
            graph = new
            {
                status = graphHealth,
                indexedAt = graphIndex?.IndexedAt,
                indexedCommitSha = graphIndex?.CommitSha?[..Math.Min(7, graphIndex?.CommitSha?.Length ?? 0)],
                commitsBehind = graphCommitsBehind
            },
            activeJob = activeJob is null ? null : new
            {
                jobId = activeJob.JobId,
                state = activeJob.State.ToString().ToLowerInvariant(),
                processedItems = activeJob.ProcessedItems,
                totalItems = activeJob.TotalItems,
                progressPercent = activeJob.ProgressPercent
            }
        });
    }

    private static async Task<IResult> TriggerReindex(
        string workspaceId,
        AuraDbContext db,
        IBackgroundIndexer backgroundIndexer,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        var options = new RagIndexOptions { Recursive = true };
        var (jobId, isNew) = backgroundIndexer.QueueDirectory(workspace.CanonicalPath, options);

        workspace.Status = WorkspaceStatus.Indexing;
        workspace.LastAccessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Accepted($"/api/workspaces/{workspace.Id}/index/jobs/{jobId}", new
        {
            workspaceId = workspace.Id,
            jobId,
            isNewJob = isNew,
            message = isNew ? "Re-indexing started" : "Indexing already in progress"
        });
    }

    private static async Task<IResult> ClearIndex(
        string workspaceId,
        AuraDbContext db,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        var normalizedPath = workspace.CanonicalPath;

        // Clear RAG chunks
        var chunksToDelete = await db.RagChunks
            .Where(c => c.SourcePath != null && c.SourcePath.StartsWith(normalizedPath))
            .ToListAsync(ct);
        db.RagChunks.RemoveRange(chunksToDelete);

        // Clear index metadata (but keep workspace)
        var metadataToDelete = await db.IndexMetadata
            .Where(i => i.WorkspacePath == normalizedPath && i.IndexType == IndexTypes.Rag)
            .ToListAsync(ct);
        db.IndexMetadata.RemoveRange(metadataToDelete);

        workspace.Status = WorkspaceStatus.Pending;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            workspaceId = workspace.Id,
            chunksRemoved = chunksToDelete.Count,
            message = "Index cleared. Workspace preserved."
        });
    }

    private static async Task<IResult> ListJobs(
        string workspaceId,
        AuraDbContext db,
        IBackgroundIndexer backgroundIndexer,
        HttpContext context,
        CancellationToken ct)
    {
        var workspace = await FindWorkspaceAsync(workspaceId, db, ct);
        if (workspace is null)
        {
            return Problem.WorkspaceNotFound(workspaceId, context);
        }

        var normalizedPath = Path.GetFullPath(workspace.CanonicalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var jobs = backgroundIndexer.GetActiveJobs()
            .Where(j => string.Equals(
                Path.GetFullPath(j.Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedPath,
                StringComparison.OrdinalIgnoreCase))
            .Select(j => new
            {
                jobId = j.JobId,
                state = j.State.ToString().ToLowerInvariant(),
                processedItems = j.ProcessedItems,
                totalItems = j.TotalItems,
                progressPercent = j.ProgressPercent,
                startedAt = j.StartedAt,
                completedAt = j.CompletedAt,
                error = j.Error
            })
            .ToList();

        return Results.Ok(new
        {
            workspaceId = workspace.Id,
            count = jobs.Count,
            jobs
        });
    }

    private static IResult GetJobStatus(
        string workspaceId,
        Guid jobId,
        IBackgroundIndexer backgroundIndexer,
        HttpContext context)
    {
        var status = backgroundIndexer.GetJobStatus(jobId);
        if (status is null)
        {
            return Problem.NotFound("Job", jobId, context);
        }

        return Results.Ok(new
        {
            jobId = status.JobId,
            workspaceId,
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

    private static async Task<(string status, int? commitsBehind)> CalculateHealthAsync(
        IndexMetadata? index,
        string? currentCommitSha,
        string workspacePath,
        bool isGitRepo,
        IGitService gitService,
        CancellationToken ct)
    {
        if (index is null)
        {
            return ("not-indexed", null);
        }

        int? commitsBehind = null;
        bool isStale = false;

        if (isGitRepo && !string.IsNullOrEmpty(index.CommitSha) && !string.IsNullOrEmpty(currentCommitSha))
        {
            if (index.CommitSha != currentCommitSha)
            {
                var countResult = await gitService.CountCommitsSinceAsync(workspacePath, index.CommitSha, ct);
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

        return (isStale ? "stale" : "fresh", commitsBehind);
    }
}
