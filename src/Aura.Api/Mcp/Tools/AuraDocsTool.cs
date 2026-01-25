// <copyright file="AuraDocsTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp.Tools;

using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of the Aura documentation search tool.
/// Provides semantic search capabilities over Aura documentation using RAG.
/// </summary>
public sealed class AuraDocsTool : IAuraDocsTool
{
    private readonly IRagService _ragService;
    private readonly ILogger<AuraDocsTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuraDocsTool"/> class.
    /// </summary>
    /// <param name="ragService">The RAG service for performing semantic searches.</param>
    /// <param name="logger">The logger instance.</param>
    public AuraDocsTool(IRagService ragService, ILogger<AuraDocsTool> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<object> SearchDocumentationAsync(string query, CancellationToken ct)
    {
        _logger.LogDebug("Searching Aura documentation for query: {Query}", query);

        var options = new RagQueryOptions
        {
            TopK = 10,
            ContentTypes = new[] { RagContentType.Documentation, RagContentType.Markdown },
            MinScore = 0.5
        };

        var results = await _ragService.QueryAsync(query, options, ct);

        _logger.LogInformation(
            "Found {ResultCount} documentation results for query: {Query}",
            results.Count,
            query);

        return new
        {
            query,
            resultCount = results.Count,
            results = results.Select(r => new
            {
                content = r.Text,
                sourcePath = r.SourcePath,
                score = r.Score,
                contentType = r.ContentType.ToString(),
                metadata = r.Metadata
            })
        };
    }
}
