// <copyright file="BackgroundIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Channels;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Microsoft.EntityFrameworkCore;
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
    public (Guid JobId, bool IsNew) QueueDirectory(string directoryPath, RagIndexOptions? options = null)
    {
        // Use PathNormalizer for consistent cross-platform path handling
        var normalizedPath = PathNormalizer.Normalize(directoryPath);

        // Check if there's already an active job for this path
        var existingJob = _jobs.Values.FirstOrDefault(j =>
            (j.State == IndexJobState.Queued || j.State == IndexJobState.Processing) &&
            string.Equals(PathNormalizer.Normalize(j.Source), normalizedPath, StringComparison.Ordinal));

        if (existingJob is not null)
        {
            _logger.LogInformation("Reusing existing job {JobId} for {Path} (state: {State})",
                existingJob.JobId, normalizedPath, existingJob.State);
            return (existingJob.JobId, false);
        }

        var jobId = Guid.NewGuid();
        var jobStatus = new IndexJobStatus
        {
            JobId = jobId,
            Source = normalizedPath,
            State = IndexJobState.Queued,
            TotalItems = 0, // Will be updated when processing starts
        };

        _jobs[jobId] = jobStatus;
        _logger.LogInformation("Created job {JobId} with Source={Source}", jobId, jobStatus.Source);

        var workItem = new IndexWorkItem
        {
            Type = WorkItemType.Directory,
            DirectoryPath = normalizedPath,
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
                _logger.LogInformation("Queued directory for indexing: {Path} (Job: {JobId})", normalizedPath, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue directory: {Path}", normalizedPath);
                UpdateJobStatus(jobId, s => s with { State = IndexJobState.Failed, Error = ex.Message });
            }
        });

        return (jobId, true);
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
    public IReadOnlyCollection<IndexJobStatus> GetActiveJobs()
    {
        return _jobs.Values
            .Where(j => j.State is IndexJobState.Queued or IndexJobState.Processing)
            .ToList();
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
        var ingestorRegistry = scope.ServiceProvider.GetRequiredService<Ingestors.IIngestorRegistry>();
        var codeGraphService = scope.ServiceProvider.GetRequiredService<ICodeGraphService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<IGitService>();

        switch (workItem.Type)
        {
            case WorkItemType.Content when workItem.Content is not null:
                await ragService.IndexAsync(workItem.Content, cancellationToken);
                break;

            case WorkItemType.Directory when workItem.DirectoryPath is not null:
                await ProcessDirectoryAsync(workItem, ragService, agentRegistry, ingestorRegistry, codeGraphService, dbContext, gitService, cancellationToken);
                break;
        }
    }

    private async Task ProcessDirectoryAsync(
        IndexWorkItem workItem,
        IRagService ragService,
        IAgentRegistry agentRegistry,
        Ingestors.IIngestorRegistry ingestorRegistry,
        ICodeGraphService codeGraphService,
        AuraDbContext dbContext,
        IGitService gitService,
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
            // Discover files first (prefer git-tracked files for performance)
            var files = await DiscoverFilesAsync(directoryPath, options, gitService, cancellationToken);
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
                    var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);

                    // Priority chain: ICodeIngestor > IContentIngestor > Agent > PlainText
                    var ingestor = ingestorRegistry.GetIngestor(filePath);

                    if (ingestor is Ingestors.ICodeIngestor codeIngestor)
                    {
                        // Native code ingestor - produces RAG chunks AND code graph nodes
                        _logger.LogDebug("Using code ingestor {IngestorId} for {FilePath}",
                            codeIngestor.IngestorId, filePath);

                        var result = await codeIngestor.IngestCodeAsync(
                            filePath, content, directoryPath, cancellationToken);

                        // Collect all RAG chunks for batch embedding
                        var ragContents = new List<RagContent>(result.Chunks.Count);
                        foreach (var chunk in result.Chunks)
                        {
                            var contentId = $"{filePath}:{chunk.ChunkType}:{chunk.SymbolName ?? chunk.StartLine.ToString()}";
                            ragContents.Add(new RagContent(contentId, chunk.Text, RagContentType.Code)
                            {
                                SourcePath = filePath,
                                Language = chunk.Language,
                                Metadata = chunk.Metadata,
                            });
                        }

                        // Index all chunks in one batch (single embedding call for the file)
                        if (ragContents.Count > 0)
                        {
                            await ragService.IndexBatchAsync(ragContents, cancellationToken);
                        }

                        // Save code graph nodes and edges
                        foreach (var node in result.Nodes)
                        {
                            await codeGraphService.AddNodeAsync(node, cancellationToken);
                        }

                        foreach (var edge in result.Edges)
                        {
                            await codeGraphService.AddEdgeAsync(edge, cancellationToken);
                        }

                        if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                        {
                            await codeGraphService.SaveChangesAsync(cancellationToken);
                        }
                    }
                    else if (ingestor is not null && ingestor is not Ingestors.PlainTextIngestor)
                    {
                        // Native content ingestor (non-code) - produces RAG chunks only
                        _logger.LogDebug("Using ingestor {IngestorId} for {FilePath}",
                            ingestor.IngestorId, filePath);

                        var chunks = await ingestor.IngestAsync(filePath, content, cancellationToken);

                        // Collect all chunks for batch embedding
                        var ragContents = new List<RagContent>(chunks.Count);
                        foreach (var chunk in chunks)
                        {
                            var contentId = $"{filePath}:{chunk.ChunkType}:{chunk.SymbolName ?? chunk.StartLine.ToString()}";
                            ragContents.Add(new RagContent(contentId, chunk.Text, ingestor.ContentType)
                            {
                                SourcePath = filePath,
                                Language = chunk.Language,
                                Metadata = chunk.Metadata,
                            });
                        }

                        // Index all chunks in one batch
                        if (ragContents.Count > 0)
                        {
                            await ragService.IndexBatchAsync(ragContents, cancellationToken);
                        }
                    }
                    else
                    {
                        // Try agent-based ingestion (LLM-powered)
                        var capability = $"ingest:{extension}";
                        var ingesterAgent = agentRegistry.GetBestForCapability(capability);

                        if (ingesterAgent is not null)
                        {
                            _logger.LogDebug("Using agent {AgentId} for {FilePath}",
                                ingesterAgent.AgentId, filePath);

                            var chunks = await IngestWithAgentAsync(
                                ingesterAgent, filePath, content, extension, cancellationToken);

                            // Collect all chunks for batch embedding
                            var ragContents = new List<RagContent>(chunks.Count);
                            foreach (var chunk in chunks)
                            {
                                var contentId = $"{filePath}:{chunk.ChunkType}:{chunk.SymbolName ?? chunk.StartLine.ToString()}";
                                ragContents.Add(new RagContent(contentId, chunk.Text, RagContentType.Code)
                                {
                                    SourcePath = filePath,
                                    Language = chunk.Language,
                                    Metadata = chunk.Metadata.AsReadOnly(),
                                });
                            }

                            if (ragContents.Count > 0)
                            {
                                await ragService.IndexBatchAsync(ragContents, cancellationToken);
                            }
                        }
                        else
                        {
                            // Final fallback: plain text indexing
                            _logger.LogDebug("No ingestor/agent for .{Extension}, using text indexing: {Path}",
                                extension, filePath);

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                var ragContent = RagContent.FromFile(filePath, content);
                                await ragService.IndexAsync(ragContent, cancellationToken);
                            }
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

            // Save index metadata for freshness tracking
            await SaveIndexMetadataAsync(directoryPath, processedCount, totalFiles, dbContext, gitService, cancellationToken);

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

    private async Task SaveIndexMetadataAsync(
        string directoryPath,
        int processedCount,
        int totalFiles,
        AuraDbContext dbContext,
        IGitService gitService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use PathNormalizer for consistent cross-platform path handling
            var normalizedPath = PathNormalizer.Normalize(directoryPath);

            // Get git commit info if available
            string? commitSha = null;
            DateTimeOffset? commitAt = null;

            var isGitRepo = await gitService.IsRepositoryAsync(normalizedPath, cancellationToken);
            if (isGitRepo)
            {
                var headResult = await gitService.GetHeadCommitAsync(normalizedPath, cancellationToken);
                if (headResult.Success)
                {
                    commitSha = headResult.Value;
                    var timestampResult = await gitService.GetCommitTimestampAsync(normalizedPath, commitSha!, cancellationToken);
                    if (timestampResult.Success)
                    {
                        commitAt = timestampResult.Value;
                    }
                }
            }

            // Save metadata for RAG index (the BackgroundIndexer does unified indexing for both RAG and graph)
            // We'll create/update entries for both index types since ProcessDirectoryAsync handles both
            var indexTypes = new[] { IndexTypes.Rag, IndexTypes.Graph };

            foreach (var indexType in indexTypes)
            {
                var existingMetadata = await dbContext.IndexMetadata
                    .FirstOrDefaultAsync(m => m.WorkspacePath == normalizedPath && m.IndexType == indexType, cancellationToken);

                if (existingMetadata is not null)
                {
                    existingMetadata.IndexedAt = DateTimeOffset.UtcNow;
                    existingMetadata.CommitSha = commitSha;
                    existingMetadata.CommitAt = commitAt;
                    existingMetadata.FilesIndexed = totalFiles;
                    existingMetadata.ItemsCreated = processedCount;
                }
                else
                {
                    var metadata = new IndexMetadata
                    {
                        WorkspacePath = normalizedPath,
                        IndexType = indexType,
                        IndexedAt = DateTimeOffset.UtcNow,
                        CommitSha = commitSha,
                        CommitAt = commitAt,
                        FilesIndexed = totalFiles,
                        ItemsCreated = processedCount,
                    };
                    dbContext.IndexMetadata.Add(metadata);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Saved index metadata for {Path} (commit: {CommitSha})", normalizedPath, commitSha?[..7] ?? "non-git");
        }
        catch (Exception ex)
        {
            // Don't fail the indexing job if metadata save fails
            _logger.LogWarning(ex, "Failed to save index metadata for {Path}", directoryPath);
        }
    }

    private async Task<List<string>> DiscoverFilesAsync(
        string directoryPath,
        RagIndexOptions options,
        IGitService gitService,
        CancellationToken ct)
    {
        var patterns = options.EffectiveIncludePatterns;
        var excludePatterns = options.EffectiveExcludePatterns;

        _logger.LogInformation("DiscoverFilesAsync: path={Path}, preferGit={PreferGit}, patterns={Patterns}",
            directoryPath, options.PreferGitTrackedFiles, string.Join(",", patterns));

        // Try git-based discovery first (much faster, respects .gitignore)
        if (options.PreferGitTrackedFiles)
        {
            var isRepo = await gitService.IsRepositoryAsync(directoryPath, ct);
            _logger.LogInformation("IsRepositoryAsync({Path}) = {IsRepo}", directoryPath, isRepo);

            if (isRepo)
            {
                var gitResult = await gitService.GetTrackedFilesAsync(directoryPath, ct);
                _logger.LogInformation("GetTrackedFilesAsync: Success={Success}, Count={Count}, Error={Error}",
                    gitResult.Success, gitResult.Value?.Count ?? 0, gitResult.Error ?? "none");

                if (gitResult.Success && gitResult.Value is not null)
                {
                    var gitFiles = gitResult.Value
                        .Select(relativePath => Path.Combine(directoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar)))
                        .Where(absolutePath => MatchesIncludePatterns(absolutePath, patterns))
                        .Where(absolutePath => !GlobMatcher.MatchesAny(absolutePath, excludePatterns))
                        .ToList();

                    _logger.LogInformation("Git-based discovery found {FileCount} matching files (from {TotalTracked} tracked)",
                        gitFiles.Count, gitResult.Value.Count);

                    return gitFiles;
                }

                _logger.LogWarning("Git-based discovery failed: {Error}. Falling back to directory scan.",
                    gitResult.Error);
            }
        }

        _logger.LogWarning("Using fallback directory scan for {Path}", directoryPath);

        // Fallback: traditional directory scan
        var searchOption = options.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

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

    private static bool MatchesIncludePatterns(string filePath, IReadOnlyList<string> patterns)
    {
        var fileName = Path.GetFileName(filePath);
        foreach (var pattern in patterns)
        {
            // Simple extension matching for patterns like "*.cs"
            if (pattern.StartsWith("*.") && fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Exact name match
            if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<SemanticChunk>> IngestWithAgentAsync(
        IAgent ingester,
        string filePath,
        string content,
        string extension,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Using ingester {AgentId} for {FilePath}", ingester.AgentId, filePath);

        var ingesterContext = new IngesterContext(filePath, content, extension);
        var context = ingesterContext.ToAgentContext();

        try
        {
            var output = await ingester.ExecuteAsync(context, cancellationToken);

            if (output.Artifacts.TryGetValue(Agents.ArtifactKeys.Chunks, out var chunksJson))
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
