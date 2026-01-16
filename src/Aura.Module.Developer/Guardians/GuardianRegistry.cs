// <copyright file="GuardianRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Guardians;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using Aura.Foundation.Guardians;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Default implementation of guardian registry with hot-reload support.
/// </summary>
public sealed class GuardianRegistry : IGuardianRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, GuardianDefinition> _guardians = new(StringComparer.OrdinalIgnoreCase);
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<GuardianRegistry> _logger;
    private readonly List<string> _watchDirectories = [];
    private readonly List<IFileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IDeserializer _yamlDeserializer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardianRegistry"/> class.
    /// </summary>
    /// <param name="fileSystem">File system abstraction.</param>
    /// <param name="logger">Logger instance.</param>
    public GuardianRegistry(
        IFileSystem fileSystem,
        ILogger<GuardianRegistry> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc/>
    public IReadOnlyList<GuardianDefinition> Guardians => _guardians.Values.ToList();

    /// <inheritdoc/>
    public event EventHandler<GuardianRegistryChangedEventArgs>? GuardiansChanged;

    /// <inheritdoc/>
    public GuardianDefinition? GetGuardian(string guardianId)
    {
        _guardians.TryGetValue(guardianId, out var guardian);
        return guardian;
    }

    /// <inheritdoc/>
    public IReadOnlyList<GuardianDefinition> GetByTriggerType(GuardianTriggerType triggerType)
    {
        return _guardians.Values
            .Where(g => g.Triggers.Any(t => t.Type == triggerType))
            .ToList();
    }

    /// <inheritdoc/>
    public void AddWatchDirectory(string directory)
    {
        if (_watchDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _watchDirectories.Add(directory);

        if (!_fileSystem.Directory.Exists(directory))
        {
            _logger.LogDebug("Guardian directory does not exist yet: {Directory}", directory);
            return;
        }

        var watcher = _fileSystem.FileSystemWatcher.New(directory, "*.yaml");
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        watcher.EnableRaisingEvents = true;

        _watchers.Add(watcher);
        _logger.LogInformation("Watching for guardian changes in: {Directory}", directory);
    }

    /// <inheritdoc/>
    public async Task ReloadAsync()
    {
        await _reloadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var previousIds = _guardians.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var directory in _watchDirectories)
            {
                if (!_fileSystem.Directory.Exists(directory))
                {
                    continue;
                }

                var files = _fileSystem.Directory.GetFiles(directory, "*.yaml");
                foreach (var file in files)
                {
                    try
                    {
                        var guardian = await LoadGuardianAsync(file).ConfigureAwait(false);
                        if (guardian is not null)
                        {
                            var isUpdate = _guardians.ContainsKey(guardian.Id);
                            _guardians[guardian.Id] = guardian;
                            loadedIds.Add(guardian.Id);

                            _logger.LogInformation(
                                "{Action} guardian: {GuardianId} ({Name})",
                                isUpdate ? "Updated" : "Loaded",
                                guardian.Id,
                                guardian.Name);

                            OnGuardiansChanged(new GuardianRegistryChangedEventArgs
                            {
                                ChangeType = isUpdate ? GuardianChangeType.Updated : GuardianChangeType.Added,
                                GuardianId = guardian.Id,
                                Guardian = guardian,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load guardian from {File}", file);
                    }
                }
            }

            // Remove guardians that no longer exist
            foreach (var id in previousIds.Except(loadedIds))
            {
                if (_guardians.TryRemove(id, out _))
                {
                    _logger.LogInformation("Removed guardian: {GuardianId}", id);
                    OnGuardiansChanged(new GuardianRegistryChangedEventArgs
                    {
                        ChangeType = GuardianChangeType.Removed,
                        GuardianId = id,
                    });
                }
            }

            _logger.LogInformation("Guardian registry reloaded: {Count} guardians", _guardians.Count);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task<GuardianDefinition?> LoadGuardianAsync(string filePath)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var definition = _yamlDeserializer.Deserialize<GuardianDefinition>(content);

        if (string.IsNullOrEmpty(definition?.Id))
        {
            _logger.LogWarning("Guardian file {File} has no ID, skipping", filePath);
            return null;
        }

        return definition;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Guardian file changed: {File} ({ChangeType})", e.Name, e.ChangeType);

        // Debounce by using a timer or just reload async
        _ = Task.Run(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false); // Small debounce
            await ReloadAsync().ConfigureAwait(false);
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("Guardian file renamed: {OldName} -> {NewName}", e.OldName, e.Name);
        _ = Task.Run(ReloadAsync);
    }

    private void OnGuardiansChanged(GuardianRegistryChangedEventArgs args)
    {
        GuardiansChanged?.Invoke(this, args);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _reloadLock.Dispose();
        _disposed = true;
    }
}
