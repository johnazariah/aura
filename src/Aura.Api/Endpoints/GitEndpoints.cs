// <copyright file="GitEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Contracts;
using Aura.Foundation.Git;

/// <summary>
/// Git-related endpoints for repository operations.
/// </summary>
public static class GitEndpoints
{
    /// <summary>
    /// Maps all git endpoints to the application.
    /// </summary>
    public static WebApplication MapGitEndpoints(this WebApplication app)
    {
        app.MapGet("/api/git/status", GetStatus);
        app.MapPost("/api/git/branch", CreateBranch);
        app.MapPost("/api/git/commit", Commit);
        app.MapGet("/api/git/worktrees", ListWorktrees);
        app.MapPost("/api/git/worktrees", CreateWorktree);
        app.MapDelete("/api/git/worktrees", RemoveWorktree);

        return app;
    }

    private static async Task<IResult> GetStatus(
        string path,
        IGitService gitService,
        CancellationToken ct)
    {
        var result = await gitService.GetStatusAsync(path, ct);

        if (result.Success)
        {
            return Results.Ok(new
            {
                success = true,
                branch = result.Value!.CurrentBranch,
                isDirty = result.Value.IsDirty,
                modifiedFiles = result.Value.ModifiedFiles,
                untrackedFiles = result.Value.UntrackedFiles,
                stagedFiles = result.Value.StagedFiles
            });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }

    private static async Task<IResult> CreateBranch(
        CreateBranchRequest request,
        IGitService gitService,
        CancellationToken ct)
    {
        var result = await gitService.CreateBranchAsync(
            request.RepoPath,
            request.BranchName,
            request.BaseBranch,
            ct);

        if (result.Success)
        {
            return Results.Ok(new
            {
                success = true,
                branch = request.BranchName,
                baseBranch = request.BaseBranch ?? "HEAD"
            });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }

    private static async Task<IResult> Commit(
        CommitRequest request,
        IGitService gitService,
        CancellationToken ct)
    {
        // Manual API commits respect hooks (skipHooks: false)
        var result = await gitService.CommitAsync(request.RepoPath, request.Message, skipHooks: false, ct);

        if (result.Success)
        {
            return Results.Ok(new { success = true, sha = result.Value });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }

    private static async Task<IResult> ListWorktrees(
        string repoPath,
        IGitWorktreeService worktreeService,
        CancellationToken ct)
    {
        var result = await worktreeService.ListAsync(repoPath, ct);

        if (result.Success)
        {
            return Results.Ok(new
            {
                success = true,
                worktrees = result.Value!.Select(w => new
                {
                    path = w.Path,
                    branch = w.Branch,
                    commitSha = w.CommitSha,
                    isMainWorktree = w.IsMainWorktree,
                    isLocked = w.IsLocked,
                    lockReason = w.LockReason
                })
            });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }

    private static async Task<IResult> CreateWorktree(
        CreateWorktreeRequest request,
        IGitWorktreeService worktreeService,
        CancellationToken ct)
    {
        var result = await worktreeService.CreateAsync(
            request.RepoPath,
            request.BranchName,
            request.WorktreePath,
            request.BaseBranch,
            ct);

        if (result.Success)
        {
            return Results.Ok(new
            {
                success = true,
                path = result.Value!.Path,
                branch = result.Value.Branch
            });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }

    private static async Task<IResult> RemoveWorktree(
        string path,
        bool? force,
        IGitWorktreeService worktreeService,
        CancellationToken ct)
    {
        var result = await worktreeService.RemoveAsync(path, force ?? false, ct);

        if (result.Success)
        {
            return Results.Ok(new { success = true, removed = path });
        }

        return Results.BadRequest(new { success = false, error = result.Error });
    }
}
