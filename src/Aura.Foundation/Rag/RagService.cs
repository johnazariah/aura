// <copyright file="RagService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.IO.Abstractions;
using System.Text.Json;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag.Ingestors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

/// <summary>
/// RAG service implementation using Ollama embeddings and pgvector storage.
/// All data stays local - no external API calls.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly AuraDbContext _dbContext;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IFileSystem _fileSystem;
    private readonly IIngestorRegistry _ingestorRegistry;
    private readonly ILogger<RagService> _logger;
    private readonly RagOptions _options;
    private readonly TextChunker _chunker;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagService"/> class.
    /// </summary>
    public RagService(
        AuraDbContext dbContext,
        IEmbeddingProvider embeddingProvider,
        IFileSystem fileSystem,
        IIngestorRegistry ingestorRegistry,
        IOptions<RagOptions> options,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _embeddingProvider = embeddingProvider;
        _fileSystem = fileSystem;
        _ingestorRegistry = ingestorRegistry;
        _logger = logger;
        _options = options.Value;
        _chunker = new TextChunker(_options.ChunkSize, _options.ChunkOverlap);
    }

    /// <inheritdoc/>
    public async Task IndexAsync(RagContent content, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Indexing content: {ContentId}", content.ContentId);

        // Remove existing chunks for this content
        await RemoveAsync(content.ContentId, cancellationToken).ConfigureAwait(false);

        // Split into chunks
        var chunks = _chunker.Split(content.Text, content.ContentType);

        if (chunks.Count == 0)
        {
            _logger.LogWarning("No chunks generated for content: {ContentId}", content.ContentId);
            return;
        }

        _logger.LogDebug("Generated {ChunkCount} chunks for {ContentId}", chunks.Count, content.ContentId);

        // Generate embeddings for all chunks
        var embeddings = await _embeddingProvider.GenerateEmbeddingsAsync(
            _options.EmbeddingModel,
            chunks,
            cancellationToken).ConfigureAwait(false);

        // Store chunks with embeddings
        var metadataJson = content.Metadata != null
            ? JsonSerializer.Serialize(content.Metadata)
            : null;

        for (var i = 0; i < chunks.Count; i++)
        {
            var ragChunk = new RagChunk
            {
                Id = Guid.NewGuid(),
                ContentId = content.ContentId,
                ChunkIndex = i,
                Content = chunks[i],
                ContentType = content.ContentType,
                SourcePath = content.SourcePath is not null ? NormalizePath(content.SourcePath) : null,
                Embedding = new Vector(embeddings[i]),
                MetadataJson = metadataJson,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.RagChunks.Add(ragChunk);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Indexed {ChunkCount} chunks for content: {ContentId}",
            chunks.Count, content.ContentId);
    }

    /// <inheritdoc/>
    public async Task IndexManyAsync(IEnumerable<RagContent> contents, CancellationToken cancellationToken = default)
    {
        foreach (var content in contents)
        {
            await IndexAsync(content, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<int> IndexDirectoryAsync(
        string directoryPath,
        RagIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RagIndexOptions();

        if (!_fileSystem.Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory not found: {Path}", directoryPath);
            return 0;
        }

        var searchOption = options.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var patterns = options.EffectiveIncludePatterns;
        var excludePatterns = options.EffectiveExcludePatterns;

        var indexedCount = 0;

        foreach (var pattern in patterns)
        {
            var files = _fileSystem.Directory.GetFiles(directoryPath, pattern, searchOption);

            foreach (var filePath in files)
            {
                // Check exclusions using shared GlobMatcher
                if (GlobMatcher.MatchesAny(filePath, excludePatterns))
                {
                    continue;
                }

                try
                {
                    var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

                    // Use smart ingestor if available
                    var ingestor = _ingestorRegistry.GetIngestor(filePath);
                    if (ingestor is not null)
                    {
                        await IndexWithIngestorAsync(filePath, content, ingestor, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Fallback to simple indexing
                        var ragContent = RagContent.FromFile(filePath, content, options.ContentType);
                        await IndexAsync(ragContent, cancellationToken).ConfigureAwait(false);
                    }

                    indexedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index file: {Path}", filePath);
                }
            }
        }

        _logger.LogInformation("Indexed {Count} files from {Path}", indexedCount, directoryPath);
        return indexedCount;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string contentId, CancellationToken cancellationToken = default)
    {
        var deleted = await _dbContext.RagChunks
            .Where(c => c.ContentId == contentId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogDebug("Removed {Count} chunks for content: {ContentId}", deleted, contentId);
        }

        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RagResult>> QueryAsync(
        string query,
        RagQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RagQueryOptions();
        var topK = options.TopK > 0 ? options.TopK : _options.DefaultTopK;
        var minScore = options.MinScore ?? _options.MinRelevanceScore;

        _logger.LogDebug("RAG query: {Query}, topK={TopK}", query, topK);

        // Generate embedding for query
        var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(
            _options.EmbeddingModel,
            query,
            cancellationToken).ConfigureAwait(false);

        var queryVector = new Vector(queryEmbedding);

        // Build query
        var dbQuery = _dbContext.RagChunks.AsQueryable();

        // Apply filters
        if (options.ContentTypes?.Count > 0)
        {
            dbQuery = dbQuery.Where(c => options.ContentTypes.Contains(c.ContentType));
        }

        if (!string.IsNullOrEmpty(options.SourcePathPrefix))
        {
            // Normalize path for case-insensitive comparison on Windows
            var normalizedPrefix = NormalizePath(options.SourcePathPrefix);
            dbQuery = dbQuery.Where(c => c.SourcePath != null && 
                EF.Functions.ILike(c.SourcePath, normalizedPrefix + "%"));
        }

        // Order by vector similarity (cosine distance)
        var results = await dbQuery
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(topK * 2) // Get extra to filter by score
            .Select(c => new
            {
                c.ContentId,
                c.ChunkIndex,
                c.Content,
                c.ContentType,
                c.SourcePath,
                c.MetadataJson,
                Distance = c.Embedding!.CosineDistance(queryVector),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Convert distance to similarity score and filter
        var ragResults = results
            .Select(r => new RagResult(
                r.ContentId,
                r.ChunkIndex,
                r.Content,
                1.0 - r.Distance) // Convert cosine distance to similarity
            {
                ContentType = r.ContentType,
                SourcePath = r.SourcePath,
                Metadata = ParseMetadata(r.MetadataJson),
            })
            .Where(r => r.Score >= minScore)
            .ToList();

        // Prioritize results from specific files if requested
        if (options.PrioritizeFiles?.Count > 0)
        {
            // Separate results into prioritized and non-prioritized
            var prioritized = new List<RagResult>();
            var nonPrioritized = new List<RagResult>();

            foreach (var r in ragResults)
            {
                var fileName = Path.GetFileName(r.SourcePath ?? string.Empty);
                var isPrioritized = options.PrioritizeFiles.Any(p =>
                    fileName.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    (r.SourcePath?.EndsWith(p, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.SourcePath?.Contains($"/{p}", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.SourcePath?.Contains($"\\{p}", StringComparison.OrdinalIgnoreCase) ?? false));

                if (isPrioritized)
                {
                    prioritized.Add(r);
                }
                else
                {
                    nonPrioritized.Add(r);
                }
            }

            // Ensure prioritized files get prominent placement:
            // Take up to half of topK from prioritized files, then fill with others
            var prioritizedCount = Math.Min(prioritized.Count, Math.Max(topK / 2, 3));
            var remainingSlots = topK - prioritizedCount;

            ragResults = prioritized
                .OrderByDescending(r => r.Score)
                .Take(prioritizedCount)
                .Concat(nonPrioritized.OrderByDescending(r => r.Score).Take(remainingSlots))
                .ToList();

            _logger.LogDebug(
                "Applied file prioritization: {PrioritizedCount} from prioritized files, {OtherCount} from others",
                Math.Min(prioritizedCount, prioritized.Count),
                Math.Min(remainingSlots, nonPrioritized.Count));
        }

        var finalResults = ragResults.Take(topK).ToList();

        _logger.LogDebug("RAG query returned {Count} results", finalResults.Count);
        return finalResults;
    }

    /// <inheritdoc/>
    public async Task<RagStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalChunks = await _dbContext.RagChunks.CountAsync(cancellationToken).ConfigureAwait(false);

        var totalDocuments = await _dbContext.RagChunks
            .Select(c => c.ContentId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var byContentType = await _dbContext.RagChunks
            .GroupBy(c => c.ContentType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Type, g => g.Count, cancellationToken)
            .ConfigureAwait(false);

        // Estimate size (rough approximation)
        var estimatedSize = totalChunks * (2000 + 768 * 4); // avg chunk + embedding

        return new RagStats(totalChunks, totalDocuments, estimatedSize)
        {
            ByContentType = byContentType,
        };
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.RagChunks.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Cleared all RAG chunks");
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check database connection
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
            {
                return false;
            }

            // Check embedding model availability
            if (_embeddingProvider is OllamaProvider ollama)
            {
                var isHealthy = await ollama.IsHealthyAsync(cancellationToken).ConfigureAwait(false);
                if (!isHealthy)
                {
                    return false;
                }

                var modelAvailable = await ollama.IsModelAvailableAsync(_options.EmbeddingModel, cancellationToken).ConfigureAwait(false);
                if (!modelAvailable)
                {
                    _logger.LogWarning("Embedding model not available: {Model}", _options.EmbeddingModel);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG health check failed");
            return false;
        }
    }

    /// <summary>
    /// Indexes a file using a smart ingestor that understands the file structure.
    /// </summary>
    private async Task IndexWithIngestorAsync(
        string filePath,
        string content,
        IContentIngestor ingestor,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Indexing {FilePath} with {Ingestor}", filePath, ingestor.IngestorId);

        // Remove existing chunks for this file
        await RemoveAsync(filePath, cancellationToken).ConfigureAwait(false);

        // Get structured chunks from ingestor
        var chunks = await ingestor.IngestAsync(filePath, content, cancellationToken).ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            _logger.LogWarning("No chunks generated for: {FilePath}", filePath);
            return;
        }

        _logger.LogDebug("Ingestor produced {ChunkCount} chunks for {FilePath}", chunks.Count, filePath);

        // Generate embeddings for all chunk texts
        var chunkTexts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embeddingProvider.GenerateEmbeddingsAsync(
            _options.EmbeddingModel,
            chunkTexts,
            cancellationToken).ConfigureAwait(false);

        // Store chunks with embeddings
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var metadata = new Dictionary<string, string>
            {
                ["chunkType"] = chunk.ChunkType,
            };

            if (chunk.Title is not null)
            {
                metadata["title"] = chunk.Title;
            }

            if (chunk.Language is not null)
            {
                metadata["language"] = chunk.Language;
            }

            if (chunk.StartLine.HasValue)
            {
                metadata["startLine"] = chunk.StartLine.Value.ToString();
            }

            if (chunk.EndLine.HasValue)
            {
                metadata["endLine"] = chunk.EndLine.Value.ToString();
            }

            // Merge any additional metadata from the chunk
            if (chunk.Metadata is not null)
            {
                foreach (var kv in chunk.Metadata)
                {
                    metadata[kv.Key] = kv.Value;
                }
            }

            var ragChunk = new RagChunk
            {
                Id = Guid.NewGuid(),
                ContentId = NormalizePath(filePath),
                ChunkIndex = i,
                Content = chunk.Text,
                ContentType = ingestor.ContentType,
                SourcePath = NormalizePath(filePath),
                Embedding = new Vector(embeddings[i]),
                MetadataJson = JsonSerializer.Serialize(metadata),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.RagChunks.Add(ragChunk);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Indexed {ChunkCount} chunks for {FilePath} using {Ingestor}",
            chunks.Count, filePath, ingestor.IngestorId);
    }

    private static IReadOnlyDictionary<string, string>? ParseMetadata(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes a file path for consistent storage and comparison.
    /// Uses forward slashes and lowercase for case-insensitive matching.
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Use lowercase and forward slashes for consistent comparison
        return path.Replace('\\', '/').ToLowerInvariant();
    }

    /// <inheritdoc/>
    public async Task<RagDirectoryStats?> GetDirectoryStatsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        // Normalize path for case-insensitive comparison
        var normalizedPath = NormalizePath(directoryPath.TrimEnd('\\', '/'));

        // Find chunks where SourcePath starts with the directory path (using ILike for case-insensitive)
        var chunks = await _dbContext.RagChunks
            .Where(c => c.SourcePath != null &&
                EF.Functions.ILike(c.SourcePath, normalizedPath + "/%"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            return null;
        }

        var fileCount = chunks
            .Select(c => c.SourcePath)
            .Distinct()
            .Count();

        var lastIndexed = chunks
            .Max(c => c.CreatedAt);

        return new RagDirectoryStats(
            directoryPath,
            chunks.Count,
            fileCount,
            lastIndexed);
    }
}
