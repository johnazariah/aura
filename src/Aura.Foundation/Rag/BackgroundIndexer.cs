// <copyright file="BackgroundIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Channels;
using Aura.Foundation.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Background indexing service using channels for async document processing.
/// Provides non-blocking indexing with progress tracking.
/// </summary>
public sealed class BackgroundIndexer : BackgroundService, IBackgroundIndexer
{
    private readonly Channel<IndexWorkItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BackgroundIndexer> _logger;
    private readonly BackgroundIndexerOptions _options;
    private readonly ConcurrentDictionary<Guid, IndexJobStatus> _jobs = new();

    private int _queuedItems;
    private int _processedItems;
    private int _failedItems;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundIndexer"/> class.
    /// </summary>
    public BackgroundIndexer(
        IServiceScopeFactory scopeFactory,
        IFileSystem fileSystem,
        IOptions<BackgroundIndexerOptions> options,
        ILogger<BackgroundIndexer> logger)
    {
        _scopeFactory = scopeFactory;
        _fileSystem = fileSystem;
        _logger = logger;
        _options = options.Value;

        _channel = Channel.CreateBounded<IndexWorkItem>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false, // Allow multiple consumers
            SingleWriter = false,
        });
    }

    /// <inheritdoc/>
    public bool QueueContent(RagContent content)
    {
        var workItem = new IndexWorkItem
        {
            Type = WorkItemType.Content,
            Content = content,
        };

        if (_channel.Writer.TryWrite(workItem))
        {
            Interlocked.Increment(ref _queuedItems);
            _logger.LogDebug("Queued content for indexing: {ContentId}", content.ContentId);
            return true;
        }

        _logger.LogWarning("Queue full, could not queue content: {ContentId}", content.ContentId);
        return false;
    }

    /// <inheritdoc/>
    public Guid QueueDirectory(string directoryPath, RagIndexOptions? options = null)
    {
        var jobId = Guid.NewGuid();
        var jobStatus = new IndexJobStatus
        {
            JobId = jobId,
            Source = directoryPath,
            State = IndexJobState.Queued,
            TotalItems = 0, // Will be updated when processing starts
        };

        _jobs[jobId] = jobStatus;
        _logger.LogInformation("Created job {JobId} with Source={Source}", jobId, jobStatus.Source);

        var workItem = new IndexWorkItem
        {
            Type = WorkItemType.Directory,
            DirectoryPath = directoryPath,
            IndexOptions = options,
            JobId = jobId,
        };

        // For directory jobs, always queue (we want to track them)
        _ = Task.Run(async () =>
        {
            try
            {
                await _channel.Writer.WriteAsync(workItem);
                Interlocked.Increment(ref _queuedItems);
                _logger.LogInformation("Queued directory for indexing: {Path} (Job: {JobId})", directoryPath, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue directory: {Path}", directoryPath);
                UpdateJobStatus(jobId, s => s with { State = IndexJobState.Failed, Error = ex.Message });
            }
        });

        return jobId;
    }

    /// <inheritdoc/>
    public BackgroundIndexerStatus GetStatus()
    {
        return new BackgroundIndexerStatus
        {
            QueuedItems = _channel.Reader.Count,
            ProcessedItems = _processedItems,
            FailedItems = _failedItems,
            IsProcessing = _activeWorkers > 0,
            ActiveJobs = _jobs.Values.Count(j => j.State is IndexJobState.Queued or IndexJobState.Processing),
        };
    }

    /// <inheritdoc/>
    public IndexJobStatus? GetJobStatus(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var status))
        {
            _logger.LogDebug("GetJobStatus {JobId}: Source={Source}, State={State}", jobId, status.Source, status.State);
            return status;
        }
        _logger.LogWarning("GetJobStatus {JobId}: NOT FOUND", jobId);
        return null;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately to allow host startup to complete
        // This is critical - BackgroundService.ExecuteAsync blocks host startup until it yields
        await Task.Yield();

        _logger.LogInformation("Background indexer started with {WorkerCount} workers", _options.WorkerCount);

        // Start multiple worker tasks for parallel processing
        var workers = Enumerable.Range(0, _options.WorkerCount)
            .Select(_ => ProcessWorkItemsAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);

        _logger.LogInformation("Background indexer stopped");
    }

    private int _activeWorkers;

    private async Task ProcessWorkItemsAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            Interlocked.Increment(ref _activeWorkers);
            try
            {
                await ProcessWorkItemAsync(workItem, stoppingToken);
                Interlocked.Increment(ref _processedItems);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedItems);
                _logger.LogError(ex, "Failed to process work item");

                if (workItem.JobId.HasValue)
                {
                    UpdateJobStatus(workItem.JobId.Value, s => s with
                    {
                        FailedItems = s.FailedItems + 1,
                    });
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkers);
            }
        }
    }

    private async Task ProcessWorkItemAsync(IndexWorkItem workItem, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ragService = scope.ServiceProvider.GetRequiredService<IRagService>();
        var agentRegistry = scope.ServiceProvider.GetRequiredService<IAgentRegistry>();

        switch (workItem.Type)
        {
            case WorkItemType.Content when workItem.Content is not null:
                await ragService.IndexAsync(workItem.Content, cancellationToken);
                break;

            case WorkItemType.Directory when workItem.DirectoryPath is not null:
                await ProcessDirectoryAsync(workItem, ragService, agentRegistry, cancellationToken);
                break;
        }
    }

    private async Task ProcessDirectoryAsync(
        IndexWorkItem workItem,
        IRagService ragService,
        IAgentRegistry agentRegistry,
        CancellationToken cancellationToken)
    {
        var jobId = workItem.JobId!.Value;
        var directoryPath = workItem.DirectoryPath!;
        var options = workItem.IndexOptions ?? new RagIndexOptions();

        UpdateJobStatus(jobId, s => s with
        {
            State = IndexJobState.Processing,
            StartedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            // Discover files first
            var files = DiscoverFiles(directoryPath, options);
            var totalFiles = files.Count;

            UpdateJobStatus(jobId, s => s with { TotalItems = totalFiles });

            _logger.LogInformation("Processing directory {Path}: {FileCount} files (Job: {JobId})",
                directoryPath, totalFiles, jobId);

            var processedCount = 0;

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    UpdateJobStatus(jobId, s => s with { State = IndexJobState.Cancelled });
                    return;
                }

                try
                {
                    var extension = Path.GetExtension(filePath).TrimStart('.');
                    var capability = $"ingest:{extension}";

                    // Find the best ingester agent for this file type
                    var ingester = agentRegistry.GetBestForCapability(capability);

                    if (ingester is not null)
                    {
                        // Use agent-based ingestion
                        var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
                        var chunks = await IngestWithAgentAsync(ingester, filePath, content, extension, cancellationToken);

                        foreach (var chunk in chunks)
                        {
                            var contentId = $"{filePath}:{chunk.ChunkType}:{chunk.SymbolName ?? chunk.StartLine.ToString()}";
                            var ragContent = new RagContent(contentId, chunk.Text, RagContentType.Code)
                            {
                                SourcePath = filePath,
                                Language = chunk.Language,
                                Metadata = chunk.Metadata.AsReadOnly(),
                            };
                            await ragService.IndexAsync(ragContent, cancellationToken);
                        }
                    }
                    else
                    {
                        // Fall back to simple text-based RAG indexing (no ingester available)
                        _logger.LogDebug("No ingester found for .{Extension}, using text indexing for: {Path}",
                            extension, filePath);

                        var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var ragContent = RagContent.FromFile(filePath, content);
                            await ragService.IndexAsync(ragContent, cancellationToken);
                        }
                    }

                    processedCount++;
                    UpdateJobStatus(jobId, s => s with { ProcessedItems = processedCount });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index file: {Path}", filePath);
                    UpdateJobStatus(jobId, s => s with { FailedItems = s.FailedItems + 1 });
                }
            }

            UpdateJobStatus(jobId, s => s with
            {
                State = IndexJobState.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
            });

            _logger.LogInformation("Directory indexing completed: {Path} ({Processed}/{Total} files, Job: {JobId})",
                directoryPath, processedCount, totalFiles, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directory indexing failed: {Path} (Job: {JobId})", directoryPath, jobId);
            UpdateJobStatus(jobId, s => s with
            {
                State = IndexJobState.Failed,
                Error = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private List<string> DiscoverFiles(string directoryPath, RagIndexOptions options)
    {
        var searchOption = options.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var patterns = options.EffectiveIncludePatterns;
        var excludePatterns = options.EffectiveExcludePatterns;

        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            var matchingFiles = _fileSystem.Directory.GetFiles(directoryPath, pattern, searchOption);
            foreach (var file in matchingFiles)
            {
                if (!GlobMatcher.MatchesAny(file, excludePatterns))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    private async Task<IReadOnlyList<SemanticChunk>> IngestWithAgentAsync(
        IAgent ingester,
        string filePath,
        string content,
        string extension,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Using ingester {AgentId} for {FilePath}", ingester.AgentId, filePath);

        var context = new AgentContext(
            Prompt: "Parse this file and extract semantic chunks",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = filePath,
                ["content"] = content,
                ["language"] = extension,
                ["extension"] = extension,
            });

        try
        {
            var output = await ingester.ExecuteAsync(context, cancellationToken);

            if (output.Artifacts.TryGetValue("chunks", out var chunksJson))
            {
                var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(chunksJson);
                if (chunks is not null)
                {
                    _logger.LogDebug("Ingester {AgentId} extracted {ChunkCount} chunks from {FilePath}",
                        ingester.AgentId, chunks.Count, filePath);
                    return chunks;
                }
            }

            // Ingester didn't return chunks in expected format, create fallback
            _logger.LogWarning("Ingester {AgentId} did not return valid chunks for {FilePath}",
                ingester.AgentId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ingester {AgentId} failed for {FilePath}, using fallback",
                ingester.AgentId, filePath);
        }

        // Fallback: return entire file as single chunk
        return [
            new SemanticChunk
            {
                Text = content,
                FilePath = filePath,
                ChunkType = ChunkTypes.File,
                SymbolName = Path.GetFileName(filePath),
                StartLine = 1,
                EndLine = content.Split('\n').Length,
                Language = extension,
            }
        ];
    }

    private void UpdateJobStatus(Guid jobId, Func<IndexJobStatus, IndexJobStatus> updater)
    {
        _jobs.AddOrUpdate(
            jobId,
            id => throw new InvalidOperationException($"Job {id} not found"),
            (id, existing) => updater(existing));
    }

    private enum WorkItemType
    {
        Content,
        Directory,
    }

    private sealed record IndexWorkItem
    {
        public WorkItemType Type { get; init; }
        public RagContent? Content { get; init; }
        public string? DirectoryPath { get; init; }
        public RagIndexOptions? IndexOptions { get; init; }
        public Guid? JobId { get; init; }
    }
}

/// <summary>
/// Options for the background indexer.
/// </summary>
public sealed class BackgroundIndexerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BackgroundIndexer";

    /// <summary>Gets or sets the maximum queue size.</summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>Gets or sets the number of worker tasks.</summary>
    public int WorkerCount { get; set; } = 2;
}
