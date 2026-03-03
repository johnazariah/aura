// <copyright file="OpenAiEmbeddingProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// OpenAI-compatible embedding provider. Works with OpenAI, Azure OpenAI,
/// and any API that implements the /v1/embeddings endpoint.
/// Supports configurable batch sizes for high-throughput indexing.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiEmbeddingOptions _options;
    private readonly ILogger<OpenAiEmbeddingProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiEmbeddingProvider"/> class.
    /// </summary>
    public OpenAiEmbeddingProvider(
        HttpClient httpClient,
        IOptions<OpenAiEmbeddingOptions> options,
        ILogger<OpenAiEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<float[]> GenerateEmbeddingAsync(
        string model,
        string text,
        CancellationToken cancellationToken = default)
    {
        var truncated = Truncate(text);
        _logger.LogDebug("OpenAI embed: model={Model}, text_length={Length}", model, truncated.Length);

        var request = new OpenAiEmbeddingRequest
        {
            Model = model,
            Input = [truncated],
            Dimensions = _options.Dimensions,
        };

        var response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Data is null || response.Data.Count == 0)
        {
            throw LlmException.GenerationFailed("Empty embedding response from OpenAI");
        }

        return response.Data[0].Embedding;
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

        var truncated = texts.Select(Truncate).ToList();
        var batchSize = _options.BatchSize;
        var allEmbeddings = new List<float[]>(texts.Count);

        // Process in batches
        for (var i = 0; i < truncated.Count; i += batchSize)
        {
            var batch = truncated.Skip(i).Take(batchSize).ToList();
            var batchNum = (i / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)truncated.Count / batchSize);

            _logger.LogDebug(
                "OpenAI embed batch {Batch}/{Total}: model={Model}, count={Count}",
                batchNum, totalBatches, model, batch.Count);

            var request = new OpenAiEmbeddingRequest
            {
                Model = model,
                Input = batch,
                Dimensions = _options.Dimensions,
            };

            var response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.Data is null || response.Data.Count != batch.Count)
            {
                throw LlmException.GenerationFailed(
                    $"Expected {batch.Count} embeddings, got {response.Data?.Count ?? 0}");
            }

            // OpenAI returns embeddings in order but with index field — sort by index
            allEmbeddings.AddRange(
                response.Data.OrderBy(d => d.Index).Select(d => d.Embedding));

            // Rate limiting: small delay between batches if configured
            if (i + batchSize < truncated.Count && _options.BatchDelayMs > 0)
            {
                await Task.Delay(_options.BatchDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "OpenAI embed complete: {Count} embeddings in {Batches} batch(es)",
            allEmbeddings.Count, (int)Math.Ceiling((double)truncated.Count / batchSize));

        return allEmbeddings;
    }

    private async Task<OpenAiEmbeddingResponse> SendRequestAsync(
        OpenAiEmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "v1/embeddings",
                request,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("OpenAI embed failed: {Status} - {Error}", response.StatusCode, error);

                // Handle rate limiting
                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalMilliseconds ?? 1000;
                    _logger.LogWarning("Rate limited, waiting {Ms}ms", retryAfter);
                    await Task.Delay((int)retryAfter, cancellationToken).ConfigureAwait(false);
                    return await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
                }

                throw LlmException.GenerationFailed($"HTTP {(int)response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            return result ?? throw LlmException.GenerationFailed("Null response from OpenAI");
        }
        catch (OperationCanceledException) { throw; }
        catch (LlmException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenAI connection failed");
            throw LlmException.Unavailable("OpenAI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI embedding error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    private string Truncate(string text)
    {
        if (text.Length <= _options.MaxTextLength)
        {
            return text;
        }

        return text[.._options.MaxTextLength];
    }

    // Request/response DTOs

    private sealed class OpenAiEmbeddingRequest
    {
        public required string Model { get; init; }
        public required IReadOnlyList<string> Input { get; init; }
        public int? Dimensions { get; init; }
    }

    private sealed class OpenAiEmbeddingResponse
    {
        public List<EmbeddingData>? Data { get; init; }
        public UsageInfo? Usage { get; init; }
    }

    private sealed class EmbeddingData
    {
        public int Index { get; init; }
        public float[] Embedding { get; init; } = [];
    }

    private sealed class UsageInfo
    {
        public int PromptTokens { get; init; }
        public int TotalTokens { get; init; }
    }
}

/// <summary>
/// Configuration for OpenAI-compatible embedding providers.
/// </summary>
public sealed class OpenAiEmbeddingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Llm:Providers:OpenAi";

    /// <summary>
    /// Gets or sets the API base URL.
    /// Use "https://api.openai.com/" for OpenAI,
    /// or "https://{resource}.openai.azure.com/openai/deployments/{deployment}/" for Azure.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/";

    /// <summary>Gets or sets the API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the embedding model.
    /// OpenAI: "text-embedding-3-small" (1536d) or "text-embedding-3-large" (3072d).
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Gets or sets the output dimensions for the embedding vector.
    /// OpenAI's text-embedding-3 models support Matryoshka dimension reduction.
    /// Set to 768 to match Ollama's nomic-embed-text for seamless failover.
    /// If null, uses the model's native dimensions (1536 for small, 3072 for large).
    /// </summary>
    public int? Dimensions { get; set; } = 768;

    /// <summary>
    /// Gets or sets the number of texts to send per API request.
    /// OpenAI supports up to 2048 inputs per request.
    /// Higher = faster throughput, but larger request payloads.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the delay in milliseconds between batches.
    /// Useful for staying within rate limits. 0 = no delay.
    /// </summary>
    public int BatchDelayMs { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum text length in characters before truncation.
    /// text-embedding-3-small supports ~8191 tokens ≈ 32000 chars.
    /// </summary>
    public int MaxTextLength { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
