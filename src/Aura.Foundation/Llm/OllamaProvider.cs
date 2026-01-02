// <copyright file="OllamaProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Foundation.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Ollama LLM provider implementation.
/// Communicates with local Ollama instance via HTTP API.
/// Also provides embedding generation via IEmbeddingProvider.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OllamaProvider"/> class.
/// </remarks>
public sealed class OllamaProvider(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaProvider> logger) : ILlmProvider, IEmbeddingProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<OllamaProvider> _logger = logger;
    private readonly OllamaOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public string ProviderId => "ollama";

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(
        string? model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? _options.DefaultModel;
        _logger.LogDebug(
            "Ollama generate: model={Model}, prompt_length={PromptLength}, temp={Temperature}",
            effectiveModel, prompt.Length, temperature);

        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = effectiveModel,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaModelOptions { Temperature = temperature },
            };

            using var cts = CreateTimeoutCts(cancellationToken);

            var response = await _httpClient.PostAsJsonAsync(
                _options.BaseUrl + "/api/generate",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError("Ollama generate failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result is null)
            {
                throw LlmException.GenerationFailed("Empty response from Ollama");
            }

            return new LlmResponse(
                Content: result.Response ?? string.Empty,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? model,
                FinishReason: result.Done ? "stop" : null);
        }
        catch (OperationCanceledException) { throw; }
        catch (LlmException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed");
            throw LlmException.Unavailable("ollama");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama generate error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? _options.DefaultModel;
        _logger.LogDebug("Ollama chat: model={Model}, messages={MessageCount}, temp={Temperature}", effectiveModel, messages.Count, temperature);

        try
        {
            var request = new OllamaChatRequest
            {
                Model = effectiveModel,
                Messages = messages.Select(m => new OllamaChatMessage
                {
                    Role = m.Role.ToString().ToLowerInvariant(),
                    Content = m.Content,
                }).ToList(),
                Stream = false,
                Options = new OllamaModelOptions { Temperature = temperature },
            };

            using var cts = CreateTimeoutCts(cancellationToken);

            var response = await _httpClient.PostAsJsonAsync(
                _options.BaseUrl + "/api/chat",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError("Ollama chat failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result is null)
            {
                throw LlmException.GenerationFailed("Empty response from Ollama");
            }

            return new LlmResponse(
                Content: result.Message?.Content ?? string.Empty,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? model,
                FinishReason: result.Done ? "stop" : null);
        }
        catch (OperationCanceledException) { throw; }
        catch (LlmException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed");
            throw LlmException.Unavailable("ollama");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama chat error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<float[]> GenerateEmbeddingAsync(
        string model,
        string text,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Ollama embed: model={Model}, text_length={TextLength}", model, text.Length);

        try
        {
            var request = new OllamaEmbedRequest { Model = model, Input = text };
            using var cts = CreateTimeoutCts(cancellationToken);

            var response = await _httpClient.PostAsJsonAsync(
                _options.BaseUrl + "/api/embed",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError("Ollama embed failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result?.Embeddings is null || result.Embeddings.Count == 0)
            {
                throw LlmException.GenerationFailed("Empty embedding response from Ollama");
            }

            return result.Embeddings[0];
        }
        catch (OperationCanceledException) { throw; }
        catch (LlmException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed for embeddings");
            throw LlmException.Unavailable("ollama");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama embedding error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        string model,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        if (texts.Count == 1)
        {
            var single = await GenerateEmbeddingAsync(model, texts[0], cancellationToken).ConfigureAwait(false);
            return [single];
        }

        _logger.LogDebug("Ollama embed batch: model={Model}, count={Count}", model, texts.Count);

        try
        {
            // Use batch embedding API with array input
            var request = new OllamaEmbedBatchRequest { Model = model, Input = texts.ToList() };
            using var cts = CreateTimeoutCts(cancellationToken);

            var response = await _httpClient.PostAsJsonAsync(
                _options.BaseUrl + "/api/embed",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError("Ollama batch embed failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result?.Embeddings is null || result.Embeddings.Count != texts.Count)
            {
                throw LlmException.GenerationFailed($"Expected {texts.Count} embeddings, got {result?.Embeddings?.Count ?? 0}");
            }

            return result.Embeddings;
        }
        catch (OperationCanceledException) { throw; }
        catch (LlmException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed for batch embeddings");
            throw LlmException.Unavailable("ollama");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama batch embedding error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
            // Support flexible matching: "nomic-embed-text" matches "nomic-embed-text:latest"
            // Also handle case where user specifies full name with tag
            return models.Any(m =>
                m.Name.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                m.Name.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase) ||
                model.StartsWith(m.Name.Split(':')[0] + ":", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                _options.BaseUrl + "/api/tags",
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (response?.Models is null) return [];

            return response.Models
                .Select(m => new ModelInfo(Name: m.Name ?? "unknown", Size: m.Size, ModifiedAt: m.ModifiedAt))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list Ollama models");
            return [];
        }
    }

    /// <summary>
    /// Checks if Ollama is available and responding.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var response = await _httpClient.GetAsync(_options.BaseUrl + "/api/tags", linked.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private CancellationTokenSource CreateTimeoutCts(CancellationToken cancellationToken)
    {
        var cts = new CancellationTokenSource(_options.TimeoutSeconds * 1000);
        return CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
    }

    #region Ollama API DTOs

    private sealed record OllamaGenerateRequest
    {
        public required string Model { get; init; }
        public required string Prompt { get; init; }
        public bool Stream { get; init; }
        public OllamaModelOptions? Options { get; init; }
    }

    private sealed record OllamaGenerateResponse
    {
        public string? Model { get; init; }
        public string? Response { get; init; }
        public bool Done { get; init; }
        public int? PromptEvalCount { get; init; }
        public int? EvalCount { get; init; }
    }

    private sealed record OllamaChatRequest
    {
        public required string Model { get; init; }
        public required List<OllamaChatMessage> Messages { get; init; }
        public bool Stream { get; init; }
        public OllamaModelOptions? Options { get; init; }
    }

    private sealed record OllamaChatMessage
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
    }

    private sealed record OllamaChatResponse
    {
        public string? Model { get; init; }
        public OllamaChatMessage? Message { get; init; }
        public bool Done { get; init; }
        public int? PromptEvalCount { get; init; }
        public int? EvalCount { get; init; }
    }

    private sealed record OllamaModelOptions
    {
        public double Temperature { get; init; } = 0.7;
    }

    private sealed record OllamaTagsResponse
    {
        public List<OllamaModelEntry>? Models { get; init; }
    }

    private sealed record OllamaModelEntry
    {
        public string? Name { get; init; }
        public long? Size { get; init; }
        public DateTimeOffset? ModifiedAt { get; init; }
    }

    private sealed record OllamaEmbedRequest
    {
        public required string Model { get; init; }
        public required string Input { get; init; }
    }

    private sealed record OllamaEmbedBatchRequest
    {
        public required string Model { get; init; }
        public required List<string> Input { get; init; }
    }

    private sealed record OllamaEmbedResponse
    {
        public List<float[]>? Embeddings { get; init; }
        public string? Model { get; init; }
    }

    #endregion
}

/// <summary>
/// Configuration options for Ollama provider.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Llm:Providers:Ollama";

    /// <summary>Gets or sets the base URL for Ollama API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Gets or sets the timeout in seconds for requests.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Gets or sets the default model to use. Required - must be set in configuration.</summary>
    public required string DefaultModel { get; set; }

    /// <summary>Gets or sets the default embedding model. Required - must be set in configuration.</summary>
    public required string DefaultEmbeddingModel { get; set; }
}
