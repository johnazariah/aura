// <copyright file="WorkspaceRegistryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.IO.Abstractions;
using System.Text.Json;
using Aura.Foundation.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of workspace registry using a JSON file for persistence.
/// </summary>
public sealed class WorkspaceRegistryService : IWorkspaceRegistryService
{
    private readonly IFileSystem _fileSystem;
    private readonly IDbContextFactory<AuraDbContext> _dbContextFactory;
    private readonly ILogger<WorkspaceRegistryService> _logger;
    private readonly string _registryPath;
    private readonly object _lock = new();

    private WorkspaceRegistryData? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceRegistryService"/> class.
    /// </summary>
    public WorkspaceRegistryService(
        IFileSystem fileSystem,
        IDbContextFactory<AuraDbContext> dbContextFactory,
        ILogger<WorkspaceRegistryService> logger)
    {
        _fileSystem = fileSystem;
        _dbContextFactory = dbContextFactory;
        _logger = logger;

        // Use platform-appropriate config directory
        var configDir = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config";

        _registryPath = _fileSystem.Path.Combine(configDir, "aura", "workspaces.json");
        _logger.LogDebug("Workspace registry path: {Path}", _registryPath);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RegisteredWorkspace> ListWorkspaces()
    {
        var data = LoadRegistry();
        return EnrichWithIndexStatus(data.Workspaces);
    }

    /// <inheritdoc/>
    public RegisteredWorkspace? GetWorkspace(string idOrAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrAlias);

        var data = LoadRegistry();
        var entry = data.Workspaces.FirstOrDefault(w =>
            w.Id.Equals(idOrAlias, StringComparison.OrdinalIgnoreCase) ||
            (w.Alias?.Equals(idOrAlias, StringComparison.OrdinalIgnoreCase) ?? false));

        if (entry is null)
        {
            return null;
        }

        return EnrichWithIndexStatus([entry]).FirstOrDefault();
    }

    /// <inheritdoc/>
    public RegisteredWorkspace? GetDefaultWorkspace()
    {
        var data = LoadRegistry();
        if (string.IsNullOrEmpty(data.DefaultId))
        {
            return null;
        }

        return GetWorkspace(data.DefaultId);
    }

    /// <inheritdoc/>
    public RegisteredWorkspace AddWorkspace(string path, string? alias = null, IReadOnlyList<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = PathNormalizer.Normalize(path);
        var id = WorkspaceIdGenerator.GenerateId(path);

        lock (_lock)
        {
            var data = LoadRegistry();

            // Check for duplicate ID
            if (data.Workspaces.Any(w => w.Id == id))
            {
                throw new InvalidOperationException($"Workspace already registered: {path}");
            }

            // Check for duplicate alias
            if (!string.IsNullOrEmpty(alias) &&
                data.Workspaces.Any(w => w.Alias?.Equals(alias, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                throw new InvalidOperationException($"Alias already in use: {alias}");
            }

            var entry = new WorkspaceRegistryEntry
            {
                Id = id,
                Path = normalizedPath,
                Alias = alias,
                Tags = tags?.ToList() ?? []
            };

            data.Workspaces.Add(entry);

            // Set as default if it's the first workspace
            if (data.Workspaces.Count == 1)
            {
                data.DefaultId = id;
            }

            SaveRegistry(data);
            _logger.LogInformation("Added workspace to registry: {Path} (ID: {Id}, Alias: {Alias})", path, id, alias);
        }

        return GetWorkspace(id)!;
    }

    /// <inheritdoc/>
    public bool RemoveWorkspace(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_lock)
        {
            var data = LoadRegistry();
            var entry = data.Workspaces.FirstOrDefault(w => w.Id == id);

            if (entry is null)
            {
                return false;
            }

            data.Workspaces.Remove(entry);

            // Clear default if it was the removed workspace
            if (data.DefaultId == id)
            {
                data.DefaultId = data.Workspaces.FirstOrDefault()?.Id;
            }

            SaveRegistry(data);
            _logger.LogInformation("Removed workspace from registry: {Id}", id);
        }

        return true;
    }

    /// <inheritdoc/>
    public bool SetDefault(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_lock)
        {
            var data = LoadRegistry();
            var entry = data.Workspaces.FirstOrDefault(w => w.Id == id);

            if (entry is null)
            {
                return false;
            }

            data.DefaultId = id;
            SaveRegistry(data);
            _logger.LogInformation("Set default workspace: {Id}", id);
        }

        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ResolveWorkspaceIds(IReadOnlyList<string> workspaceRefs)
    {
        if (workspaceRefs.Count == 0)
        {
            return [];
        }

        // Handle wildcard
        if (workspaceRefs.Contains("*"))
        {
            return ListWorkspaces().Select(w => w.Id).ToList();
        }

        var data = LoadRegistry();
        var resolved = new List<string>();

        foreach (var refStr in workspaceRefs)
        {
            var entry = data.Workspaces.FirstOrDefault(w =>
                w.Id.Equals(refStr, StringComparison.OrdinalIgnoreCase) ||
                (w.Alias?.Equals(refStr, StringComparison.OrdinalIgnoreCase) ?? false));

            if (entry is not null)
            {
                resolved.Add(entry.Id);
            }
            else
            {
                _logger.LogWarning("Workspace reference not found: {Ref}", refStr);
            }
        }

        return resolved.Distinct().ToList();
    }

    private WorkspaceRegistryData LoadRegistry()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        lock (_lock)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            if (!_fileSystem.File.Exists(_registryPath))
            {
                _cache = new WorkspaceRegistryData();
                return _cache;
            }

            try
            {
                var json = _fileSystem.File.ReadAllText(_registryPath);
                _cache = JsonSerializer.Deserialize<WorkspaceRegistryData>(json, JsonOptions) ?? new WorkspaceRegistryData();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load workspace registry from {Path}, starting fresh", _registryPath);
                _cache = new WorkspaceRegistryData();
            }

            return _cache;
        }
    }

    private void SaveRegistry(WorkspaceRegistryData data)
    {
        var directory = _fileSystem.Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        _fileSystem.File.WriteAllText(_registryPath, json);
        _cache = data;
    }

    private IReadOnlyList<RegisteredWorkspace> EnrichWithIndexStatus(IReadOnlyList<WorkspaceRegistryEntry> entries)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        using var db = _dbContextFactory.CreateDbContext();

        var result = new List<RegisteredWorkspace>();

        foreach (var entry in entries)
        {
            // Look up workspace in database to get index status
            var workspace = db.Workspaces.FirstOrDefault(w => w.Id == entry.Id);

            // Count chunks for this workspace path
            var chunkCount = db.RagChunks
                .Where(c => c.SourcePath != null && c.SourcePath.StartsWith(entry.Path))
                .Count();

            // Get last indexed time from IndexMetadata
            var indexMeta = db.IndexMetadata
                .Where(m => m.WorkspacePath == entry.Path)
                .OrderByDescending(m => m.IndexedAt)
                .FirstOrDefault();

            result.Add(new RegisteredWorkspace(
                entry.Id,
                entry.Path,
                entry.Alias,
                entry.Tags)
            {
                Indexed = chunkCount > 0,
                ChunkCount = chunkCount,
                LastIndexed = indexMeta?.IndexedAt
            });
        }

        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Internal data structure for registry persistence.
    /// </summary>
    private sealed class WorkspaceRegistryData
    {
        public List<WorkspaceRegistryEntry> Workspaces { get; set; } = [];
        public string? DefaultId { get; set; }
    }

    /// <summary>
    /// Internal entry for a registered workspace.
    /// </summary>
    private sealed class WorkspaceRegistryEntry
    {
        public required string Id { get; set; }
        public required string Path { get; set; }
        public string? Alias { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
