// <copyright file="WorkspaceRegistryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unified workspace registry backed by the database.
/// Replaces the previous dual-store approach (JSON file + DB).
/// </summary>
public sealed class WorkspaceRegistryService : IWorkspaceRegistryService
{
    private readonly IDbContextFactory<AuraDbContext> _dbContextFactory;
    private readonly ILogger<WorkspaceRegistryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceRegistryService"/> class.
    /// </summary>
    public WorkspaceRegistryService(
        IDbContextFactory<AuraDbContext> dbContextFactory,
        ILogger<WorkspaceRegistryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RegisteredWorkspace> ListWorkspaces()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var workspaces = db.Workspaces.OrderByDescending(w => w.LastAccessedAt).ToList();
        return workspaces.Select(w => ToRegistered(w, db)).ToList();
    }

    /// <inheritdoc/>
    public RegisteredWorkspace? GetWorkspace(string idOrAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrAlias);
        using var db = _dbContextFactory.CreateDbContext();

        var workspace = db.Workspaces.FirstOrDefault(w =>
            w.Id == idOrAlias ||
            (w.Alias != null && w.Alias.ToLower() == idOrAlias.ToLower()));

        return workspace is null ? null : ToRegistered(workspace, db);
    }

    /// <inheritdoc/>
    public RegisteredWorkspace? GetDefaultWorkspace()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var workspace = db.Workspaces.FirstOrDefault(w => w.IsDefault);
        return workspace is null ? null : ToRegistered(workspace, db);
    }

    /// <inheritdoc/>
    public RegisteredWorkspace AddWorkspace(string path, string? alias = null, IReadOnlyList<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = PathNormalizer.Normalize(path);
        var id = WorkspaceIdGenerator.GenerateId(path);

        using var db = _dbContextFactory.CreateDbContext();

        var existing = db.Workspaces.Find(id);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Workspace already registered: {path}");
        }

        if (!string.IsNullOrEmpty(alias) &&
            db.Workspaces.Any(w => w.Alias != null && w.Alias.ToLower() == alias.ToLower()))
        {
            throw new InvalidOperationException($"Alias already in use: {alias}");
        }

        var directoryName = Path.GetFileName(
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            ?? "Workspace";

        var isFirst = !db.Workspaces.Any();

        var workspace = new Workspace
        {
            Id = id,
            CanonicalPath = normalizedPath,
            Name = alias ?? directoryName,
            Alias = alias,
            Tags = tags?.ToList() ?? [],
            Status = WorkspaceStatus.Pending,
            IsDefault = isFirst,
        };

        db.Workspaces.Add(workspace);
        db.SaveChanges();

        _logger.LogInformation("Added workspace: {Path} (ID: {Id}, Alias: {Alias})", path, id, alias);
        return ToRegistered(workspace, db);
    }

    /// <inheritdoc/>
    public bool RemoveWorkspace(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var db = _dbContextFactory.CreateDbContext();
        var workspace = db.Workspaces.Find(id);
        if (workspace is null)
        {
            return false;
        }

        var wasDefault = workspace.IsDefault;
        db.Workspaces.Remove(workspace);

        if (wasDefault)
        {
            var next = db.Workspaces.FirstOrDefault(w => w.Id != id);
            if (next is not null)
            {
                next.IsDefault = true;
            }
        }

        db.SaveChanges();
        _logger.LogInformation("Removed workspace: {Id}", id);
        return true;
    }

    /// <inheritdoc/>
    public bool SetDefault(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var db = _dbContextFactory.CreateDbContext();
        var workspace = db.Workspaces.Find(id);
        if (workspace is null)
        {
            return false;
        }

        var currentDefault = db.Workspaces.FirstOrDefault(w => w.IsDefault);
        if (currentDefault is not null)
        {
            currentDefault.IsDefault = false;
        }

        workspace.IsDefault = true;
        db.SaveChanges();
        _logger.LogInformation("Set default workspace: {Id}", id);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ResolveWorkspaceIds(IReadOnlyList<string> workspaceRefs)
    {
        if (workspaceRefs.Count == 0)
        {
            return [];
        }

        using var db = _dbContextFactory.CreateDbContext();

        if (workspaceRefs.Contains("*"))
        {
            return db.Workspaces.Select(w => w.Id).ToList();
        }

        var resolved = new List<string>();
        foreach (var refStr in workspaceRefs)
        {
            var workspace = db.Workspaces.FirstOrDefault(w =>
                w.Id == refStr ||
                (w.Alias != null && w.Alias.ToLower() == refStr.ToLower()));

            if (workspace is not null)
            {
                resolved.Add(workspace.Id);
            }
            else
            {
                _logger.LogWarning("Workspace reference not found: {Ref}", refStr);
            }
        }

        return resolved.Distinct().ToList();
    }

    private static RegisteredWorkspace ToRegistered(Workspace w, AuraDbContext db)
    {
        var chunkCount = db.RagChunks
            .Where(c => c.SourcePath != null && c.SourcePath.StartsWith(w.CanonicalPath))
            .Count();

        var indexMeta = db.IndexMetadata
            .Where(m => m.WorkspacePath == w.CanonicalPath)
            .OrderByDescending(m => m.IndexedAt)
            .FirstOrDefault();

        return new RegisteredWorkspace(w.Id, w.CanonicalPath, w.Alias, w.Tags)
        {
            Indexed = chunkCount > 0,
            ChunkCount = chunkCount,
            LastIndexed = indexMeta?.IndexedAt,
        };
    }
}
