// <copyright file="OllamaProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Foundation.Agents;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Ollama LLM provider implementation.
/// Communicates with local Ollama instance via HTTP API.
/// </summary>
public sealed class OllamaProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProvider> _logger;
    private readonly OllamaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured for Ollama.</param>
    /// <param name="options">Ollama configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public OllamaProvider(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderId => "ollama";

    /// <inheritdoc/>
    public async Task<Result<LlmResponse, LlmError>> GenerateAsync(
        string model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Ollama generate: model={Model}, prompt_length={PromptLength}, temp={Temperature}",
            model, prompt.Length, temperature);

        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaModelOptions { Temperature = temperature },
            };

            using var cts = CreateTimeoutCts(cancellationToken);

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.BaseUrl}/api/generate",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError(
                    "Ollama generate failed: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);

                return Result.Failure<LlmResponse, LlmError>(
                    LlmError.GenerationFailed($"HTTP {response.StatusCode}", errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                JsonOptions, cts.Token).ConfigureAwait(false);

            if (result is null)
            {
                return Result.Failure<LlmResponse, LlmError>(
                    LlmError.GenerationFailed("Empty response from Ollama"));
            }

            var llmResponse = new LlmResponse(
                Content: result.Response ?? string.Empty,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? model,
                FinishReason: result.Done ? "stop" : null);

            return Result.Success<LlmResponse, LlmError>(llmResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<LlmResponse, LlmError>(LlmError.Cancelled());
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<LlmResponse, LlmError>(LlmError.Timeout());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed");
            return Result.Failure<LlmResponse, LlmError>(
                LlmError.Unavailable("ollama"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama generate error");
            return Result.Failure<LlmResponse, LlmError>(
                LlmError.GenerationFailed(ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<LlmResponse, LlmError>> ChatAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Ollama chat: model={Model}, messages={MessageCount}, temp={Temperature}",
            model, messages.Count, temperature);

        try
        {
            var request = new OllamaChatRequest
            {
                Model = model,
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
                $"{_options.BaseUrl}/api/chat",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError(
                    "Ollama chat failed: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);

                return Result.Failure<LlmResponse, LlmError>(
                    LlmError.GenerationFailed($"HTTP {response.StatusCode}", errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                JsonOptions, cts.Token).ConfigureAwait(false);

            if (result is null)
            {
                return Result.Failure<LlmResponse, LlmError>(
                    LlmError.GenerationFailed("Empty response from Ollama"));
            }

            var llmResponse = new LlmResponse(
                Content: result.Message?.Content ?? string.Empty,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? model,
                FinishReason: result.Done ? "stop" : null);

            return Result.Success<LlmResponse, LlmError>(llmResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<LlmResponse, LlmError>(LlmError.Cancelled());
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<LlmResponse, LlmError>(LlmError.Timeout());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama connection failed");
            return Result.Failure<LlmResponse, LlmError>(
                LlmError.Unavailable("ollama"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama chat error");
            return Result.Failure<LlmResponse, LlmError>(
                LlmError.GenerationFailed(ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsModelAvailableAsync(
        string model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ListModelsAsync(cancellationToken).ConfigureAwait(false);
            return models.Any(m => m.Name.Equals(model, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                $"{_options.BaseUrl}/api/tags",
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (response?.Models is null)
            {
                return [];
            }

            return response.Models
                .Select(m => new ModelInfo(
                    Name: m.Name ?? "unknown",
                    Size: m.Size,
                    ModifiedAt: m.ModifiedAt))
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if Ollama is healthy.</returns>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await _httpClient.GetAsync(
                $"{_options.BaseUrl}/api/tags",
                linked.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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

    #endregion
}

/// <summary>
/// Configuration options for Ollama provider.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Ollama";

    /// <summary>Gets or sets the base URL for Ollama API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Gets or sets the timeout in seconds for requests.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Gets or sets the default model to use.</summary>
    public string DefaultModel { get; set; } = "qwen2.5-coder:7b";
}
