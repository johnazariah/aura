// <copyright file="RagService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

using System.IO.Abstractions;
using System.Text.Json;
using Aura.Foundation.Data;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Llm;
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
        IOptions<RagOptions> options,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _embeddingProvider = embeddingProvider;
        _fileSystem = fileSystem;
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
                SourcePath = content.SourcePath,
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

        var patterns = options.IncludePatterns ?? new[] { "*.*" };
        var excludePatterns = options.ExcludePatterns ?? Array.Empty<string>();

        var indexedCount = 0;

        foreach (var pattern in patterns)
        {
            var files = _fileSystem.Directory.GetFiles(directoryPath, pattern, searchOption);

            foreach (var filePath in files)
            {
                // Check exclusions
                if (excludePatterns.Any(ep => MatchesPattern(filePath, ep)))
                {
                    continue;
                }

                try
                {
                    var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var ragContent = RagContent.FromFile(filePath, content, options.ContentType);

                    await IndexAsync(ragContent, cancellationToken).ConfigureAwait(false);
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
            dbQuery = dbQuery.Where(c => c.SourcePath != null && c.SourcePath.StartsWith(options.SourcePathPrefix));
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
            .Take(topK)
            .ToList();

        _logger.LogDebug("RAG query returned {Count} results", ragResults.Count);
        return ragResults;
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

    private static bool MatchesPattern(string filePath, string pattern)
    {
        // Simple pattern matching (could be enhanced with glob support)
        if (pattern.StartsWith("*"))
        {
            return filePath.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        }

        return filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase);
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
}
