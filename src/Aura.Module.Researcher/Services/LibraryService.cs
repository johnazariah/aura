// <copyright file="LibraryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

using Aura.Foundation.Llm;
using Aura.Module.Researcher.Data;
using Aura.Module.Researcher.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

/// <summary>
/// Implementation of the library service.
/// </summary>
public class LibraryService : ILibraryService
{
    private const string EmbeddingModel = "nomic-embed-text";

    private readonly ResearcherDbContext db;
    private readonly IEmbeddingProvider embeddingProvider;
    private readonly ILogger<LibraryService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryService"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="logger">The logger.</param>
    public LibraryService(
        ResearcherDbContext db,
        IEmbeddingProvider embeddingProvider,
        ILogger<LibraryService> logger)
    {
        this.db = db;
        this.embeddingProvider = embeddingProvider;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Source>> GetSourcesAsync(
        SourceType? sourceType = null,
        ReadingStatus? status = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        var query = this.db.Sources.AsQueryable();

        if (sourceType.HasValue)
        {
            query = query.Where(s => s.SourceType == sourceType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.ReadingStatus == status.Value);
        }

        if (tags is { Length: > 0 })
        {
            query = query.Where(s => s.Tags.Any(t => tags.Contains(t)));
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Source?> GetSourceAsync(
        Guid id,
        bool includeExcerpts = false,
        CancellationToken cancellationToken = default)
    {
        var query = this.db.Sources.AsQueryable();

        if (includeExcerpts)
        {
            query = query.Include(s => s.Excerpts);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Source> CreateSourceAsync(Source source, CancellationToken cancellationToken = default)
    {
        if (source.Id == Guid.Empty)
        {
            source.Id = Guid.NewGuid();
        }

        source.CreatedAt = DateTime.UtcNow;
        source.UpdatedAt = DateTime.UtcNow;

        // Generate embedding from abstract or title
        var textToEmbed = source.Abstract ?? source.Title;
        try
        {
            var embedding = await this.embeddingProvider.GenerateEmbeddingAsync(EmbeddingModel, textToEmbed, cancellationToken);
            source.Embedding = new Vector(embedding);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to generate embedding for source {Title}", source.Title);
        }

        this.db.Sources.Add(source);
        await this.db.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Created source {Id}: {Title}", source.Id, source.Title);
        return source;
    }

    /// <inheritdoc/>
    public async Task<Source> UpdateSourceAsync(Source source, CancellationToken cancellationToken = default)
    {
        source.UpdatedAt = DateTime.UtcNow;
        this.db.Sources.Update(source);
        await this.db.SaveChangesAsync(cancellationToken);
        return source;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await this.db.Sources.FindAsync([id], cancellationToken);
        if (source == null)
        {
            return false;
        }

        this.db.Sources.Remove(source);
        await this.db.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Deleted source {Id}: {Title}", id, source.Title);
        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(Source Source, float Score)>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await this.embeddingProvider.GenerateEmbeddingAsync(EmbeddingModel, query, cancellationToken);
        var vector = new Vector(queryEmbedding);

        var results = await this.db.Sources
            .Where(s => s.Embedding != null)
            .OrderBy(s => s.Embedding!.CosineDistance(vector))
            .Take(limit)
            .Select(s => new
            {
                Source = s,
                Distance = s.Embedding!.CosineDistance(vector),
            })
            .ToListAsync(cancellationToken);

        return results
            .Select(r => (r.Source, Score: (float)(1 - r.Distance)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<Excerpt> AddExcerptAsync(Excerpt excerpt, CancellationToken cancellationToken = default)
    {
        if (excerpt.Id == Guid.Empty)
        {
            excerpt.Id = Guid.NewGuid();
        }

        excerpt.CreatedAt = DateTime.UtcNow;

        // Generate embedding
        try
        {
            var embedding = await this.embeddingProvider.GenerateEmbeddingAsync(EmbeddingModel, excerpt.Content, cancellationToken);
            excerpt.Embedding = new Vector(embedding);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to generate embedding for excerpt");
        }

        this.db.Excerpts.Add(excerpt);
        await this.db.SaveChangesAsync(cancellationToken);

        return excerpt;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Excerpt>> GetExcerptsAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        return await this.db.Excerpts
            .Where(e => e.SourceId == sourceId)
            .OrderBy(e => e.PageNumber)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
