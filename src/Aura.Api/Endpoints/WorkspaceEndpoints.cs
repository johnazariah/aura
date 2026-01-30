// <copyright file="WorkspaceEndpoints.cs" company="Aura">
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
/// Workspace management endpoints.
/// </summary>
public static class WorkspaceEndpoints
{
    /// <summary>
    /// Maps all workspace endpoints to the application.
    /// </summary>
    public static WebApplication MapWorkspaceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/workspaces", ListWorkspaces);
        app.MapGet("/api/workspaces/{idOrPath}", GetWorkspace);
        app.MapPost("/api/workspaces", CreateWorkspace);
        app.MapDelete("/api/workspaces/{id}", DeleteWorkspace);

        return app;
    }

    private static async Task<IResult> ListWorkspaces(
        AuraDbContext db,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var query = db.Workspaces.OrderByDescending(w => w.LastAccessedAt);
        var workspaces = limit.HasValue
            ? await query.Take(limit.Value).ToListAsync(ct)
            : await query.ToListAsync(ct);

        return Results.Ok(new
        {
            count = workspaces.Count,
            workspaces = workspaces.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                path = w.CanonicalPath,
                status = w.Status.ToString().ToLowerInvariant(),
                createdAt = w.CreatedAt,
                lastAccessedAt = w.LastAccessedAt,
                gitRemoteUrl = w.GitRemoteUrl,
                defaultBranch = w.DefaultBranch
            })
        });
    }

    private static async Task<IResult> GetWorkspace(
        string idOrPath,
        AuraDbContext db,
        IRagService ragService,
        ICodeGraphService codeGraphService,
        IBackgroundIndexer backgroundIndexer,
        CancellationToken ct)
    {
        string workspaceId;
        if (WorkspaceIdGenerator.IsValidId(idOrPath))
        {
            workspaceId = idOrPath;
        }
        else
        {
            var decodedPath = Uri.UnescapeDataString(idOrPath);
            workspaceId = WorkspaceIdGenerator.GenerateId(decodedPath);
        }

        var workspace = await db.Workspaces.FindAsync([workspaceId], ct);
        if (workspace is null)
        {
            return Results.NotFound(new { error = $"Workspace not found: {idOrPath}", suggestedId = workspaceId });
        }

        workspace.LastAccessedAt = DateTimeOffset.UtcNow;

        var ragStats = await ragService.GetDirectoryStatsAsync(workspace.CanonicalPath, ct);
        var graphStats = await codeGraphService.GetStatsAsync(workspace.CanonicalPath, ct);

        var activeJob = backgroundIndexer.GetActiveJobs()
            .FirstOrDefault(j =>
                (j.State == IndexJobState.Queued || j.State == IndexJobState.Processing) &&
                string.Equals(
                    Path.GetFullPath(j.Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(workspace.CanonicalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase));

        if (workspace.Status == WorkspaceStatus.Indexing && activeJob is null)
        {
            workspace.Status = WorkspaceStatus.Ready;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = workspace.Id,
            name = workspace.Name,
            path = workspace.CanonicalPath,
            status = workspace.Status.ToString().ToLowerInvariant(),
            errorMessage = workspace.ErrorMessage,
            createdAt = workspace.CreatedAt,
            lastAccessedAt = workspace.LastAccessedAt,
            gitRemoteUrl = workspace.GitRemoteUrl,
            defaultBranch = workspace.DefaultBranch,
            stats = new
            {
                files = ragStats?.FileCount ?? 0,
                chunks = ragStats?.ChunkCount ?? 0,
                graphNodes = graphStats.TotalNodes,
                graphEdges = graphStats.TotalEdges
            },
            indexingJob = activeJob is null ? null : new
            {
                jobId = activeJob.JobId,
                state = activeJob.State.ToString(),
                processedItems = activeJob.ProcessedItems,
                totalItems = activeJob.TotalItems,
                progressPercent = activeJob.ProgressPercent
            }
        });
    }

    private static async Task<IResult> CreateWorkspace(
        CreateWorkspaceRequest request,
        AuraDbContext db,
        IBackgroundIndexer backgroundIndexer,
        IGitService gitService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return Results.BadRequest(new { error = "path is required" });
        }

        if (!Directory.Exists(request.Path))
        {
            return Results.NotFound(new { error = $"Directory not found: {request.Path}" });
        }

        var normalizedPath = PathNormalizer.Normalize(Path.GetFullPath(request.Path));
        var workspaceId = WorkspaceIdGenerator.GenerateId(request.Path);
        var directoryName = Path.GetFileName(Path.GetFullPath(request.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "Workspace";

        var existing = await db.Workspaces.FindAsync([workspaceId], ct);
        if (existing is not null)
        {
            existing.LastAccessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                id = existing.Id,
                name = existing.Name,
                path = existing.CanonicalPath,
                status = existing.Status.ToString().ToLowerInvariant(),
                isNew = false,
                message = "Workspace already exists"
            });
        }

        string? gitRemoteUrl = null;
        string? defaultBranch = null;
        var isRepo = await gitService.IsRepositoryAsync(request.Path, ct);
        if (isRepo)
        {
            try
            {
                var gitResult = await gitService.GetStatusAsync(request.Path, ct);
                if (gitResult.Success && gitResult.Value is not null)
                {
                    defaultBranch = gitResult.Value.CurrentBranch;
                }
            }
            catch
            {
                // Ignore git errors
            }
        }

        var workspace = new Workspace
        {
            Id = workspaceId,
            CanonicalPath = normalizedPath,
            Name = request.Name ?? directoryName,
            Status = WorkspaceStatus.Pending,
            GitRemoteUrl = gitRemoteUrl,
            DefaultBranch = defaultBranch
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);

        Guid? jobId = null;
        if (request.StartIndexing ?? true)
        {
            var originalPath = Path.GetFullPath(request.Path);
            var options = new RagIndexOptions
            {
                IncludePatterns = request.Options?.IncludePatterns,
                ExcludePatterns = request.Options?.ExcludePatterns,
                Recursive = true,
                PreferGitTrackedFiles = true
            };

            var (id, _) = backgroundIndexer.QueueDirectory(originalPath, options);
            jobId = id;

            workspace.Status = WorkspaceStatus.Indexing;
            await db.SaveChangesAsync(ct);
        }

        return Results.Created($"/api/workspaces/{workspaceId}", new
        {
            id = workspace.Id,
            name = workspace.Name,
            path = workspace.CanonicalPath,
            status = workspace.Status.ToString().ToLowerInvariant(),
            isNew = true,
            jobId,
            message = jobId.HasValue ? "Workspace created and indexing started" : "Workspace created"
        });
    }

    private static async Task<IResult> DeleteWorkspace(
        string id,
        AuraDbContext db,
        ICodeGraphService codeGraphService,
        CancellationToken ct)
    {
        if (!WorkspaceIdGenerator.IsValidId(id))
        {
            return Results.BadRequest(new { error = "Invalid workspace ID format" });
        }

        var workspace = await db.Workspaces.FindAsync([id], ct);
        if (workspace is null)
        {
            return Results.NotFound(new { error = $"Workspace not found: {id}" });
        }

        var originalPath = workspace.CanonicalPath;

        var chunksToDelete = await db.RagChunks
            .Where(c => c.SourcePath != null && c.SourcePath.StartsWith(originalPath))
            .ToListAsync(ct);
        db.RagChunks.RemoveRange(chunksToDelete);

        await codeGraphService.ClearRepositoryGraphAsync(originalPath, ct);

        var metadataToDelete = await db.IndexMetadata
            .Where(i => i.WorkspacePath == originalPath)
            .ToListAsync(ct);
        db.IndexMetadata.RemoveRange(metadataToDelete);

        db.Workspaces.Remove(workspace);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            id,
            path = originalPath,
            message = $"Workspace deleted. Removed {chunksToDelete.Count} RAG chunks."
        });
    }
}
