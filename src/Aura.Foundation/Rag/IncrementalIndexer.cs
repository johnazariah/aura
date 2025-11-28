// <copyright file="IncrementalIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.IO.Abstractions;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Background service that watches for file changes and updates the RAG index incrementally.
/// Enables real-time knowledge base updates without full reindexing.
/// </summary>
public sealed class IncrementalIndexer : BackgroundService, IDisposable
{
    private readonly IRagService _ragService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<IncrementalIndexer> _logger;
    private readonly RagWatcherOptions _options;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Channel<FileChangeEvent> _changeQueue;
    private readonly HashSet<string> _pendingChanges = [];
    private readonly object _pendingLock = new();
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalIndexer"/> class.
    /// </summary>
    public IncrementalIndexer(
        IRagService ragService,
        IFileSystem fileSystem,
        IOptions<RagWatcherOptions> options,
        ILogger<IncrementalIndexer> logger)
    {
        _ragService = ragService;
        _fileSystem = fileSystem;
        _logger = logger;
        _options = options.Value;
        _changeQueue = Channel.CreateUnbounded<FileChangeEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Adds a directory to watch for changes.
    /// </summary>
    public void WatchDirectory(string path, params string[] patterns)
    {
        if (!_fileSystem.Directory.Exists(path))
        {
            _logger.LogWarning("Cannot watch non-existent directory: {Path}", path);
            return;
        }

        patterns = patterns.Length > 0 ? patterns : ["*.*"];

        foreach (var pattern in patterns)
        {
            var watcher = new FileSystemWatcher(path, pattern)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnWatcherError;

            _watchers.Add(watcher);
            _logger.LogInformation("Watching {Path} for {Pattern}", path, pattern);
        }
    }

    /// <summary>
    /// Stops watching a directory.
    /// </summary>
    public void UnwatchDirectory(string path)
    {
        var watchersToRemove = _watchers
            .Where(w => w.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var watcher in watchersToRemove)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _watchers.Remove(watcher);
        }

        _logger.LogInformation("Stopped watching {Path}", path);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Incremental indexer started");

        await foreach (var change in _changeQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessChangeAsync(change, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file change: {Path}", change.Path);
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        QueueChange(e.FullPath, FileChangeType.Modified);

    private void OnFileCreated(object sender, FileSystemEventArgs e) =>
        QueueChange(e.FullPath, FileChangeType.Created);

    private void OnFileDeleted(object sender, FileSystemEventArgs e) =>
        QueueChange(e.FullPath, FileChangeType.Deleted);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueChange(e.OldFullPath, FileChangeType.Deleted);
        QueueChange(e.FullPath, FileChangeType.Created);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        _logger.LogError(e.GetException(), "File watcher error");

    private void QueueChange(string path, FileChangeType changeType)
    {
        lock (_pendingLock)
        {
            var key = changeType + ":" + path;
            if (_pendingChanges.Contains(key))
            {
                return;
            }

            _pendingChanges.Add(key);
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => FlushPendingChanges(),
            null,
            TimeSpan.FromMilliseconds(_options.DebounceMs),
            Timeout.InfiniteTimeSpan);
    }

    private void FlushPendingChanges()
    {
        List<string> pending;
        lock (_pendingLock)
        {
            pending = [.. _pendingChanges];
            _pendingChanges.Clear();
        }

        foreach (var entry in pending)
        {
            var parts = entry.Split(':', 2);
            if (parts.Length == 2 && Enum.TryParse<FileChangeType>(parts[0], out var changeType))
            {
                _changeQueue.Writer.TryWrite(new FileChangeEvent(parts[1], changeType));
            }
        }
    }

    private async Task ProcessChangeAsync(FileChangeEvent change, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing {ChangeType}: {Path}", change.ChangeType, change.Path);

        switch (change.ChangeType)
        {
            case FileChangeType.Created:
            case FileChangeType.Modified:
                await IndexFileAsync(change.Path, cancellationToken).ConfigureAwait(false);
                break;

            case FileChangeType.Deleted:
                await _ragService.RemoveAsync(change.Path, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Removed from index: {Path}", change.Path);
                break;
        }
    }

    private async Task IndexFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!_fileSystem.File.Exists(path))
        {
            return;
        }

        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var ragContent = RagContent.FromFile(path, content);
            await _ragService.IndexAsync(ragContent, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Indexed: {Path}", path);
        }
        catch (IOException ex) when (ex.HResult == -2147024864)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            await IndexFileAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed) return;

        _debounceTimer?.Dispose();
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _changeQueue.Writer.Complete();
        _disposed = true;
        base.Dispose();
    }
}

/// <summary>
/// Configuration options for file watching.
/// </summary>
public sealed class RagWatcherOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Aura:Rag:Watcher";

    /// <summary>
    /// Gets or sets the debounce delay in milliseconds.
    /// </summary>
    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the default file patterns to watch.
    /// </summary>
    public string[] DefaultPatterns { get; set; } = ["*.cs", "*.ts", "*.md", "*.py", "*.json"];
}

/// <summary>
/// Represents a file change event.
/// </summary>
internal sealed record FileChangeEvent(string Path, FileChangeType ChangeType);

/// <summary>
/// Types of file changes.
/// </summary>
internal enum FileChangeType
{
    Created,
    Modified,
    Deleted,
}
