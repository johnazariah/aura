// <copyright file="FallbackEmbeddingProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Embedding provider that tries the primary provider first, then falls back
/// to a secondary provider if the primary is unavailable or fails.
/// Default: OpenAI (fast, hosted) → Ollama (local fallback).
/// </summary>
public sealed class FallbackEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAiEmbeddingProvider _openAi;
    private readonly OllamaProvider _ollama;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<FallbackEmbeddingProvider> _logger;

    private volatile bool _primaryHealthy = true;
    private DateTime _lastPrimaryCheck = DateTime.MinValue;
    private readonly TimeSpan _recheckInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="FallbackEmbeddingProvider"/> class.
    /// </summary>
    public FallbackEmbeddingProvider(
        OpenAiEmbeddingProvider openAi,
        OllamaProvider ollama,
        IOptions<EmbeddingOptions> options,
        ILogger<FallbackEmbeddingProvider> logger)
    {
        _openAi = openAi;
        _ollama = ollama;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<float[]> GenerateEmbeddingAsync(
        string model,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (ShouldTryPrimary())
        {
            try
            {
                var result = await _openAi.GenerateEmbeddingAsync(model, text, cancellationToken)
                    .ConfigureAwait(false);
                MarkPrimaryHealthy();
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MarkPrimaryUnhealthy(ex);
            }
        }

        _logger.LogDebug("Using Ollama fallback for embedding");
        return await _ollama.GenerateEmbeddingAsync(
            _options.FallbackModel ?? model,
            text,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        string model,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        if (ShouldTryPrimary())
        {
            try
            {
                var result = await _openAi.GenerateEmbeddingsAsync(model, texts, cancellationToken)
                    .ConfigureAwait(false);
                MarkPrimaryHealthy();
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MarkPrimaryUnhealthy(ex);
            }
        }

        _logger.LogDebug("Using Ollama fallback for {Count} embeddings", texts.Count);
        return await _ollama.GenerateEmbeddingsAsync(
            _options.FallbackModel ?? model,
            texts,
            cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldTryPrimary()
    {
        if (_primaryHealthy)
        {
            return true;
        }

        // Periodically recheck if primary has recovered
        if (DateTime.UtcNow - _lastPrimaryCheck > _recheckInterval)
        {
            _logger.LogInformation("Rechecking primary embedding provider availability");
            return true;
        }

        return false;
    }

    private void MarkPrimaryHealthy()
    {
        if (!_primaryHealthy)
        {
            _logger.LogInformation("Primary embedding provider recovered");
        }

        _primaryHealthy = true;
        _lastPrimaryCheck = DateTime.UtcNow;
    }

    private void MarkPrimaryUnhealthy(Exception ex)
    {
        _primaryHealthy = false;
        _lastPrimaryCheck = DateTime.UtcNow;
        _logger.LogWarning(ex, "Primary embedding provider failed, falling back to Ollama");
    }
}
