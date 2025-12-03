# Task: Multi-Workspace Registry

## Overview

Add workspace registry for managing multiple indexed repositories, enabling cross-workspace queries.

## Parent Spec

`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 5

## Goals

1. Add `WorkspaceRegistry` entity for tracking indexed workspaces
2. Support `aura workspace add <path>` CLI command
3. Enable cross-workspace queries
4. Track indexing status per workspace

## Data Model

### WorkspaceRegistry Entity

**File:** `src/Aura.Foundation/Data/Entities/WorkspaceRegistry.cs`

```csharp
namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents a registered workspace in the Aura system.
/// </summary>
public class WorkspaceRegistry
{
    /// <summary>Gets the unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name for the workspace.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the local file system path.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the normalized path for lookups.</summary>
    public required string NormalizedPath { get; init; }

    /// <summary>Gets the optional remote Git URL.</summary>
    public string? RemoteUrl { get; init; }

    /// <summary>Gets the current Git branch.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the current status.</summary>
    public WorkspaceStatus Status { get; init; } = WorkspaceStatus.Pending;

    /// <summary>Gets when the workspace was added.</summary>
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets when indexing was last started.</summary>
    public DateTimeOffset? IndexingStartedAt { get; init; }

    /// <summary>Gets when indexing was last completed.</summary>
    public DateTimeOffset? IndexingCompletedAt { get; init; }

    /// <summary>Gets the last indexing error if any.</summary>
    public string? LastError { get; init; }

    /// <summary>Gets statistics about the indexed content.</summary>
    public string? StatsJson { get; init; }
}

/// <summary>
/// Status of a workspace in the registry.
/// </summary>
public enum WorkspaceStatus
{
    /// <summary>Workspace added but not yet indexed.</summary>
    Pending,

    /// <summary>Indexing is in progress.</summary>
    Indexing,

    /// <summary>Workspace is fully indexed and ready.</summary>
    Ready,

    /// <summary>Indexing failed.</summary>
    Error,

    /// <summary>Workspace has been archived (soft delete).</summary>
    Archived,
}
```

### Workspace Statistics

**File:** `src/Aura.Foundation/Rag/WorkspaceStats.cs`

```csharp
public record WorkspaceStats
{
    public int TotalFiles { get; init; }
    public int CodeFiles { get; init; }
    public int ContentFiles { get; init; }
    public int TotalNodes { get; init; }
    public int TotalEdges { get; init; }
    public int ChunksIndexed { get; init; }
    public Dictionary<string, int> FilesByLanguage { get; init; } = new();
    public Dictionary<string, int> NodesByType { get; init; } = new();
    public TimeSpan LastIndexDuration { get; init; }
}
```

## Database Migration

```sql
CREATE TABLE workspace_registry (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    path VARCHAR(1000) NOT NULL,
    normalized_path VARCHAR(1000) NOT NULL UNIQUE,
    remote_url VARCHAR(1000),
    branch VARCHAR(255),
    status INTEGER NOT NULL DEFAULT 0,
    added_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    indexing_started_at TIMESTAMPTZ,
    indexing_completed_at TIMESTAMPTZ,
    last_error TEXT,
    stats_json JSONB
);

CREATE INDEX idx_workspace_registry_status ON workspace_registry(status);
CREATE INDEX idx_workspace_registry_normalized_path ON workspace_registry(normalized_path);
```

## DbContext Update

**File:** `src/Aura.Foundation/Data/AuraDbContext.cs`

Add:
```csharp
/// <summary>Gets the workspace registry.</summary>
public DbSet<WorkspaceRegistry> Workspaces => Set<WorkspaceRegistry>();
```

And in `ConfigureFoundationEntities`:
```csharp
modelBuilder.Entity<WorkspaceRegistry>(entity =>
{
    entity.ToTable("workspace_registry");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
    entity.Property(e => e.Path).HasColumnName("path").HasMaxLength(1000).IsRequired();
    entity.Property(e => e.NormalizedPath).HasColumnName("normalized_path").HasMaxLength(1000).IsRequired();
    entity.HasIndex(e => e.NormalizedPath).IsUnique();
    entity.Property(e => e.RemoteUrl).HasColumnName("remote_url").HasMaxLength(1000);
    entity.Property(e => e.Branch).HasColumnName("branch").HasMaxLength(255);
    entity.Property(e => e.Status).HasColumnName("status");
    entity.Property(e => e.AddedAt).HasColumnName("added_at");
    entity.Property(e => e.IndexingStartedAt).HasColumnName("indexing_started_at");
    entity.Property(e => e.IndexingCompletedAt).HasColumnName("indexing_completed_at");
    entity.Property(e => e.LastError).HasColumnName("last_error");
    entity.Property(e => e.StatsJson).HasColumnName("stats_json").HasColumnType("jsonb");
});
```

## Service Interface

**File:** `src/Aura.Foundation/Rag/IWorkspaceRegistryService.cs`

```csharp
public interface IWorkspaceRegistryService
{
    /// <summary>
    /// Adds a workspace to the registry.
    /// </summary>
    Task<WorkspaceRegistry> AddWorkspaceAsync(
        string path,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workspace by path.
    /// </summary>
    Task<WorkspaceRegistry?> GetWorkspaceAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered workspaces.
    /// </summary>
    Task<IReadOnlyList<WorkspaceRegistry>> ListWorkspacesAsync(
        WorkspaceStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates workspace status.
    /// </summary>
    Task UpdateStatusAsync(
        Guid workspaceId,
        WorkspaceStatus status,
        string? error = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates workspace statistics after indexing.
    /// </summary>
    Task UpdateStatsAsync(
        Guid workspaceId,
        WorkspaceStats stats,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives (soft deletes) a workspace.
    /// </summary>
    Task ArchiveWorkspaceAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a workspace and all its indexed data.
    /// </summary>
    Task RemoveWorkspaceAsync(
        string path,
        CancellationToken cancellationToken = default);
}
```

## Service Implementation

**File:** `src/Aura.Foundation/Rag/WorkspaceRegistryService.cs`

```csharp
public sealed class WorkspaceRegistryService : IWorkspaceRegistryService
{
    private readonly AuraDbContext _dbContext;
    private readonly ICodeGraphService _graphService;
    private readonly IRagService _ragService;
    private readonly ILogger<WorkspaceRegistryService> _logger;

    public async Task<WorkspaceRegistry> AddWorkspaceAsync(
        string path,
        string? name,
        CancellationToken ct)
    {
        var normalizedPath = NormalizePath(path);
        
        // Check if already exists
        var existing = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.NormalizedPath == normalizedPath, ct);
        
        if (existing != null)
        {
            if (existing.Status == WorkspaceStatus.Archived)
            {
                // Reactivate archived workspace
                existing = existing with { Status = WorkspaceStatus.Pending };
                _dbContext.Workspaces.Update(existing);
                await _dbContext.SaveChangesAsync(ct);
                return existing;
            }
            return existing;
        }

        // Infer name from path if not provided
        name ??= Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));

        // Try to get Git info
        var (remoteUrl, branch) = await GetGitInfoAsync(path);

        var workspace = new WorkspaceRegistry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = path,
            NormalizedPath = normalizedPath,
            RemoteUrl = remoteUrl,
            Branch = branch,
            Status = WorkspaceStatus.Pending,
            AddedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Added workspace: {Name} at {Path}", name, path);
        return workspace;
    }

    public async Task<IReadOnlyList<WorkspaceRegistry>> ListWorkspacesAsync(
        WorkspaceStatus? status,
        CancellationToken ct)
    {
        var query = _dbContext.Workspaces.AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }
        else
        {
            query = query.Where(w => w.Status != WorkspaceStatus.Archived);
        }

        return await query.OrderBy(w => w.Name).ToListAsync(ct);
    }

    public async Task RemoveWorkspaceAsync(string path, CancellationToken ct)
    {
        var normalizedPath = NormalizePath(path);
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.NormalizedPath == normalizedPath, ct);

        if (workspace == null) return;

        // Remove all indexed data for this workspace
        await _graphService.ClearWorkspaceGraphAsync(workspace.Path, ct);
        await _ragService.RemoveAsync($"workspace:{workspace.Id}", ct);

        _dbContext.Workspaces.Remove(workspace);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Removed workspace: {Name}", workspace.Name);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();
    }

    private async Task<(string? RemoteUrl, string? Branch)> GetGitInfoAsync(string path)
    {
        try
        {
            var gitDir = Path.Combine(path, ".git");
            if (!Directory.Exists(gitDir)) return (null, null);

            // Read current branch
            var headPath = Path.Combine(gitDir, "HEAD");
            if (File.Exists(headPath))
            {
                var head = await File.ReadAllTextAsync(headPath);
                if (head.StartsWith("ref: refs/heads/"))
                {
                    var branch = head["ref: refs/heads/".Length..].Trim();
                    
                    // Try to read remote URL
                    var configPath = Path.Combine(gitDir, "config");
                    if (File.Exists(configPath))
                    {
                        var config = await File.ReadAllTextAsync(configPath);
                        var urlMatch = Regex.Match(config, @"url\s*=\s*(.+)$", RegexOptions.Multiline);
                        var remoteUrl = urlMatch.Success ? urlMatch.Groups[1].Value.Trim() : null;
                        return (remoteUrl, branch);
                    }
                    return (null, branch);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read Git info for {Path}", path);
        }
        return (null, null);
    }
}
```

## Cross-Workspace Queries

### Update ICodeGraphService

Add overloads that work across workspaces:

```csharp
/// <summary>
/// Finds nodes across all workspaces.
/// </summary>
Task<IReadOnlyList<CodeNode>> FindNodesAcrossWorkspacesAsync(
    string name,
    CodeNodeType? nodeType = null,
    CancellationToken cancellationToken = default);
```

Implementation:
```csharp
public async Task<IReadOnlyList<CodeNode>> FindNodesAcrossWorkspacesAsync(
    string name,
    CodeNodeType? nodeType,
    CancellationToken ct)
{
    // Get active workspaces
    var activeWorkspaces = await _dbContext.Workspaces
        .Where(w => w.Status == WorkspaceStatus.Ready)
        .Select(w => w.Path)
        .ToListAsync(ct);

    var query = _dbContext.CodeNodes
        .Where(n => activeWorkspaces.Contains(n.WorkspacePath!));

    if (!string.IsNullOrEmpty(name))
    {
        query = query.Where(n => 
            n.Name!.Contains(name) || 
            n.FullName!.Contains(name));
    }

    if (nodeType.HasValue)
    {
        query = query.Where(n => n.NodeType == nodeType.Value);
    }

    return await query.Take(100).ToListAsync(ct);
}
```

## API Endpoints

**File:** `src/Aura.Api/Controllers/WorkspaceController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceRegistryService _registryService;
    private readonly ISemanticIndexer _indexer;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceRegistry>>> List(
        [FromQuery] WorkspaceStatus? status,
        CancellationToken ct)
    {
        var workspaces = await _registryService.ListWorkspacesAsync(status, ct);
        return Ok(workspaces);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceRegistry>> Add(
        [FromBody] AddWorkspaceRequest request,
        CancellationToken ct)
    {
        if (!Directory.Exists(request.Path))
        {
            return BadRequest($"Directory not found: {request.Path}");
        }

        var workspace = await _registryService.AddWorkspaceAsync(
            request.Path, request.Name, ct);

        // Optionally start indexing immediately
        if (request.IndexImmediately)
        {
            _ = Task.Run(() => IndexWorkspaceAsync(workspace.Id, ct), ct);
        }

        return CreatedAtAction(nameof(Get), new { id = workspace.Id }, workspace);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkspaceRegistry>> Get(Guid id, CancellationToken ct)
    {
        var workspace = await _registryService.GetWorkspaceAsync(id, ct);
        return workspace != null ? Ok(workspace) : NotFound();
    }

    [HttpPost("{id}/index")]
    public async Task<ActionResult> StartIndexing(Guid id, CancellationToken ct)
    {
        var workspace = await _registryService.GetWorkspaceAsync(id, ct);
        if (workspace == null) return NotFound();

        _ = Task.Run(() => IndexWorkspaceAsync(id, ct), ct);
        return Accepted();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Remove(Guid id, CancellationToken ct)
    {
        await _registryService.RemoveWorkspaceAsync(id, ct);
        return NoContent();
    }

    private async Task IndexWorkspaceAsync(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await _registryService.GetWorkspaceAsync(workspaceId, ct);
        if (workspace == null) return;

        await _registryService.UpdateStatusAsync(workspaceId, WorkspaceStatus.Indexing, ct: ct);

        try
        {
            var result = await _indexer.IndexDirectoryAsync(workspace.Path, ct: ct);
            
            var stats = new WorkspaceStats
            {
                TotalFiles = result.FilesIndexed,
                ChunksIndexed = result.ChunksCreated,
                FilesByLanguage = result.FilesByLanguage,
                LastIndexDuration = result.Duration,
            };

            await _registryService.UpdateStatsAsync(workspaceId, stats, ct);
            await _registryService.UpdateStatusAsync(workspaceId, WorkspaceStatus.Ready, ct: ct);
        }
        catch (Exception ex)
        {
            await _registryService.UpdateStatusAsync(
                workspaceId, WorkspaceStatus.Error, ex.Message, ct);
        }
    }
}

public record AddWorkspaceRequest(
    string Path,
    string? Name = null,
    bool IndexImmediately = false);
```

## Testing

### Unit Tests

- `WorkspaceRegistryServiceTests.cs` - Add, list, remove workspaces
- Verify path normalization
- Test Git info extraction

### Integration Tests

- Add multiple workspaces
- Query across workspaces
- Verify workspace isolation

## Rollout Plan

1. **Phase 1**: Add entity and migration
2. **Phase 2**: Implement `WorkspaceRegistryService`
3. **Phase 3**: Add API endpoints
4. **Phase 4**: Update indexing to use registry
5. **Phase 5**: Add cross-workspace query support

## Dependencies

- Existing `ICodeGraphService` with workspace filtering
- Existing `ISemanticIndexer`

## Estimated Effort

- **Low complexity**, **Low effort**
- Mostly CRUD operations

## Success Criteria

- [ ] Can add workspace via `POST /api/workspace`
- [ ] Workspaces appear in `GET /api/workspace`
- [ ] Indexing updates workspace status
- [ ] Cross-workspace query returns results from multiple repos
- [ ] Remove workspace clears all its indexed data
