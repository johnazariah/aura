// <copyright file="AgentRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of agent registry with hot-reload support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AgentRegistry"/> class.
/// </remarks>
/// <param name="agentLoader">Agent loader for parsing agent files.</param>
/// <param name="fileSystem">File system abstraction.</param>
/// <param name="logger">Logger instance.</param>
public sealed class AgentRegistry(
    IAgentLoader agentLoader,
    IFileSystem fileSystem,
    ILogger<AgentRegistry> logger) : IAgentRegistry, IDisposable
{
    /// <summary>
    /// Maps old capability names to their unified equivalents for backward compatibility.
    /// </summary>
    private static readonly Dictionary<string, string> CapabilityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // C#
        ["csharp-coding"] = "software-development-csharp",
        ["testing-csharp"] = "software-development-csharp",
        ["csharp-documentation"] = "software-development-csharp",

        // TypeScript
        ["typescript-coding"] = "software-development-typescript",

        // JavaScript
        ["javascript-coding"] = "software-development-javascript",

        // Python
        ["python-coding"] = "software-development-python",

        // Go
        ["go-coding"] = "software-development-go",

        // F#
        ["fsharp-coding"] = "software-development-fsharp",

        // Rust
        ["rust-coding"] = "software-development-rust",
    };

    private readonly ConcurrentDictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hardcodedAgentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAgentLoader _agentLoader = agentLoader;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<AgentRegistry> _logger = logger;
    private readonly List<string> _watchDirectories = [];
    private readonly List<IFileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private bool _disposed;

    /// <inheritdoc/>
    public IReadOnlyList<IAgent> Agents => _agents.Values.ToList();

    /// <inheritdoc/>
    public event EventHandler<AgentRegistryChangedEventArgs>? AgentsChanged;

    /// <inheritdoc/>
    public IAgent? GetAgent(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    /// <inheritdoc/>
    public bool TryGetAgent(string agentId, out IAgent? agent)
    {
        return _agents.TryGetValue(agentId, out agent);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgent> GetAgentsByTags(params string[] tags)
    {
        if (tags.Length == 0)
        {
            return [];
        }

        var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        return _agents.Values
            .Where(a => a.Metadata.Tags.Any(t => tagSet.Contains(t)))
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgent> GetByCapability(string capability, string? language = null)
    {
        return _agents.Values
            .Where(a => a.Metadata.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .Where(a => language is null
                || a.Metadata.Languages.Count == 0  // Polyglot agents match any language
                || a.Metadata.Languages.Contains(language, StringComparer.OrdinalIgnoreCase))
            .OrderBy(a => a.Metadata.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public IAgent? GetBestForCapability(string capability, string? language = null)
    {
        // Resolve capability alias if one exists (backward compatibility)
        if (CapabilityAliases.TryGetValue(capability, out var aliasedCapability))
        {
            _logger.LogDebug("Resolved capability alias '{OldCapability}' -> '{NewCapability}'",
                capability, aliasedCapability);
            capability = aliasedCapability;
        }

        // First: exact match by priority
        var agent = GetByCapability(capability, language).FirstOrDefault();
        if (agent is not null)
        {
            return agent;
        }

        // Second: try wildcard match (e.g., ingest:cs -> ingest:*)
        var wildcardCapability = GetWildcardCapability(capability);
        if (wildcardCapability is not null)
        {
            _logger.LogDebug("No agent for '{Capability}', trying wildcard '{WildcardCapability}'",
                capability, wildcardCapability);
            agent = GetByCapability(wildcardCapability, language).FirstOrDefault();
            if (agent is not null)
            {
                return agent;
            }
        }

        // Third: try fallback to base capability (e.g., "csharp-coding" -> "coding")
        var baseCapability = GetBaseCapability(capability);
        if (baseCapability is not null && baseCapability != capability)
        {
            _logger.LogDebug("No agent for '{Capability}', falling back to '{BaseCapability}'",
                capability, baseCapability);
            return GetByCapability(baseCapability, language).FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Gets the wildcard version of a capability (e.g., "ingest:cs" -> "ingest:*").
    /// </summary>
    private static string? GetWildcardCapability(string capability)
    {
        var colonIndex = capability.IndexOf(':');
        if (colonIndex > 0 && colonIndex < capability.Length - 1)
        {
            return capability[..(colonIndex + 1)] + "*";
        }

        return null;
    }

    /// <summary>
    /// Extracts the base capability from a specialized capability name.
    /// E.g., "csharp-coding" -> "coding", "python-testing" -> "testing"
    /// </summary>
    private static string? GetBaseCapability(string capability)
    {
        // Known base capabilities
        var baseCapabilities = new[] { "coding", "testing", "review", "documentation", "analysis", "fixing", "chat" };

        foreach (var baseCapability in baseCapabilities)
        {
            if (capability.EndsWith($"-{baseCapability}", StringComparison.OrdinalIgnoreCase))
            {
                return baseCapability;
            }

            if (capability.StartsWith($"{baseCapability}-", StringComparison.OrdinalIgnoreCase))
            {
                return baseCapability;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public void Register(IAgent agent)
    {
        Register(agent, isHardcoded: false);
    }

    /// <summary>
    /// Registers an agent with the registry.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    /// <param name="isHardcoded">Whether this is a hardcoded (non-markdown) agent.</param>
    public void Register(IAgent agent, bool isHardcoded)
    {
        var isUpdate = _agents.ContainsKey(agent.AgentId);
        _agents[agent.AgentId] = agent;

        if (isHardcoded)
        {
            _hardcodedAgentIds.Add(agent.AgentId);
        }

        _logger.LogInformation(
            "{Action} agent: {AgentId} ({Name}) -> {Provider}/{Model}{Hardcoded}",
            isUpdate ? "Updated" : "Registered",
            agent.AgentId,
            agent.Metadata.Name,
            agent.Metadata.Provider,
            agent.Metadata.Model,
            isHardcoded ? " [hardcoded]" : string.Empty);

        OnAgentsChanged(new AgentRegistryChangedEventArgs
        {
            ChangeType = isUpdate ? AgentChangeType.Updated : AgentChangeType.Added,
            AgentId = agent.AgentId,
            Agent = agent,
        });
    }

    /// <inheritdoc/>
    public bool Unregister(string agentId)
    {
        if (_agents.TryRemove(agentId, out _))
        {
            _logger.LogInformation("Unregistered agent: {AgentId}", agentId);

            OnAgentsChanged(new AgentRegistryChangedEventArgs
            {
                ChangeType = AgentChangeType.Removed,
                AgentId = agentId,
            });

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task ReloadAsync()
    {
        await _reloadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Reloading agents from {Count} directories", _watchDirectories.Count);

            // Track which agents still exist
            var foundAgentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var directory in _watchDirectories)
            {
                if (!_fileSystem.Directory.Exists(directory))
                {
                    _logger.LogWarning("Agent directory does not exist: {Directory}", directory);
                    continue;
                }

                var files = _fileSystem.Directory.GetFiles(directory, "*.md");
                foreach (var file in files)
                {
                    try
                    {
                        var agent = await _agentLoader.LoadAsync(file).ConfigureAwait(false);
                        if (agent is not null)
                        {
                            Register(agent);
                            foundAgentIds.Add(agent.AgentId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load agent from {File}", file);
                    }
                }
            }

            // Remove markdown agents that no longer have files (but keep hardcoded agents!)
            var toRemove = _agents.Keys
                .Except(foundAgentIds)
                .Except(_hardcodedAgentIds)
                .ToList();
            foreach (var agentId in toRemove)
            {
                Unregister(agentId);
            }

            _logger.LogInformation("Reload complete. {Count} agents registered", _agents.Count);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    /// Adds a directory to watch for agent files.
    /// </summary>
    /// <param name="directory">The directory path.</param>
    /// <param name="enableHotReload">Whether to watch for file changes.</param>
    public void AddWatchDirectory(string directory, bool enableHotReload = true)
    {
        if (_watchDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _watchDirectories.Add(directory);
        _logger.LogInformation("Added agent watch directory: {Directory}", directory);

        if (enableHotReload && _fileSystem.Directory.Exists(directory))
        {
            var watcher = _fileSystem.FileSystemWatcher.New(directory, "*.md");
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Created += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
            _logger.LogInformation("Hot-reload enabled for: {Directory}", directory);
        }
    }

    /// <summary>
    /// Removes a watch directory.
    /// </summary>
    /// <param name="directory">The directory path.</param>
    public void RemoveWatchDirectory(string directory)
    {
        _watchDirectories.Remove(directory);

        var watcher = _watchers.FirstOrDefault(w => w.Path.Equals(directory, StringComparison.OrdinalIgnoreCase));
        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _watchers.Remove(watcher);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Take a snapshot to avoid concurrent modification
        var watchersSnapshot = _watchers.ToList();
        foreach (var watcher in watchersSnapshot)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _reloadLock.Dispose();
        _disposed = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Agent file changed: {Path}", e.FullPath);
        _ = ReloadFileAsync(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Agent file deleted: {Path}", e.FullPath);
        var agentId = GetAgentIdFromPath(e.FullPath);
        if (agentId is not null)
        {
            Unregister(agentId);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("Agent file renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);

        // Remove old agent
        var oldAgentId = GetAgentIdFromPath(e.OldFullPath);
        if (oldAgentId is not null)
        {
            Unregister(oldAgentId);
        }

        // Load new agent
        _ = ReloadFileAsync(e.FullPath);
    }

    private async Task ReloadFileAsync(string filePath)
    {
        try
        {
            // Small delay to handle file system events
            await Task.Delay(100).ConfigureAwait(false);

            if (!_fileSystem.File.Exists(filePath))
            {
                return;
            }

            var agent = await _agentLoader.LoadAsync(filePath).ConfigureAwait(false);
            if (agent is not null)
            {
                Register(agent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload agent from {Path}", filePath);
        }
    }

    private string? GetAgentIdFromPath(string filePath)
    {
        // Agent ID is typically the filename without extension
        var fileName = _fileSystem.Path.GetFileNameWithoutExtension(filePath);
        return fileName;
    }

    private void OnAgentsChanged(AgentRegistryChangedEventArgs e)
    {
        AgentsChanged?.Invoke(this, e);
    }
}
