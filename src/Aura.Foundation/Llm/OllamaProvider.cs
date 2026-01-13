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
    public bool SupportsStreaming => true;

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
    public async Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? _options.DefaultModel;

        _logger.LogDebug(
            "Ollama chat with options: model={Model}, messages={MessageCount}, hasSchema={HasSchema}",
            effectiveModel, messages.Count, options.ResponseSchema is not null);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            // If schema is provided, add JSON mode and inject schema into system prompt
            var effectiveMessages = messages.ToList();
            string? format = null;

            if (options.ResponseSchema is not null)
            {
                // Ollama only supports basic JSON mode, not full schema enforcement
                format = "json";

                // Inject schema requirement into system prompt
                var schemaInstruction = $"\n\nIMPORTANT: You MUST respond with valid JSON matching this schema:\n{options.ResponseSchema.Schema}";

                // Find or create system message
                var systemIdx = effectiveMessages.FindIndex(m => m.Role == ChatRole.System);
                if (systemIdx >= 0)
                {
                    effectiveMessages[systemIdx] = new ChatMessage(ChatRole.System, effectiveMessages[systemIdx].Content + schemaInstruction);
                }
                else
                {
                    effectiveMessages.Insert(0, new ChatMessage(ChatRole.System, schemaInstruction));
                }
            }

            var request = new OllamaChatRequest
            {
                Model = effectiveModel,
                Messages = effectiveMessages.Select(m => new OllamaChatMessage { Role = m.Role.ToString().ToLowerInvariant(), Content = m.Content }).ToList(),
                Stream = false,
                Options = new OllamaModelOptions { Temperature = options.Temperature },
                Format = format,
            };

            var response = await _httpClient.PostAsJsonAsync(
                _options.BaseUrl + "/api/chat",
                request,
                JsonOptions,
                linked.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                _logger.LogError("Ollama chat failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, linked.Token).ConfigureAwait(false);

            if (result is null)
            {
                throw LlmException.GenerationFailed("Empty response from Ollama");
            }

            return new LlmResponse(
                Content: result.Message?.Content ?? string.Empty,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? effectiveModel,
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
            _logger.LogError(ex, "Ollama chat with options error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LlmToken> StreamChatAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? _options.DefaultModel;
        _logger.LogDebug("Ollama streaming chat: model={Model}, messages={MessageCount}", effectiveModel, messages.Count);

        var request = new OllamaChatRequest
        {
            Model = effectiveModel,
            Messages = messages.Select(m => new OllamaChatMessage
            {
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content,
            }).ToList(),
            Stream = true,
            Options = new OllamaModelOptions { Temperature = temperature },
        };

        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;

        try
        {
            using var cts = CreateTimeoutCts(cancellationToken);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl + "/api/chat")
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };

            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogError("Ollama streaming chat failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            reader = new StreamReader(stream);

            int totalPromptTokens = 0;
            int totalEvalTokens = 0;

            string? line;
            while ((line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false)) is not null)
            {
                cts.Token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                OllamaChatResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to parse streaming chunk: {Line}", line);
                    continue;
                }

                if (chunk is null)
                {
                    continue;
                }

                // Track token counts from the final message
                if (chunk.PromptEvalCount.HasValue)
                {
                    totalPromptTokens = chunk.PromptEvalCount.Value;
                }

                if (chunk.EvalCount.HasValue)
                {
                    totalEvalTokens = chunk.EvalCount.Value;
                }

                var content = chunk.Message?.Content ?? string.Empty;

                if (chunk.Done)
                {
                    yield return new LlmToken(
                        Content: content,
                        IsComplete: true,
                        FinishReason: "stop",
                        TokensUsed: totalPromptTokens + totalEvalTokens);
                    yield break;
                }

                if (!string.IsNullOrEmpty(content))
                {
                    yield return new LlmToken(Content: content);
                }
            }
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<LlmFunctionResponse> ChatWithFunctionsAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<FunctionDefinition> functions,
        IReadOnlyList<FunctionResultMessage>? functionResults = null,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? _options.DefaultModel;
        _logger.LogDebug(
            "Ollama function chat: model={Model}, messages={MessageCount}, functions={FunctionCount}",
            effectiveModel, messages.Count, functions.Count);

        try
        {
            // Build messages including function results
            var ollamaMessages = messages.Select(m => new OllamaChatMessage
            {
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content,
            }).ToList();

            // Add function result messages if provided
            if (functionResults is { Count: > 0 })
            {
                foreach (var fnResult in functionResults)
                {
                    ollamaMessages.Add(new OllamaChatMessage
                    {
                        Role = "tool",
                        Content = fnResult.Result,
                    });
                }
            }

            // Convert functions to Ollama tool format
            var tools = functions.Select(fn => new OllamaTool
            {
                Type = "function",
                Function = new OllamaToolFunction
                {
                    Name = fn.Name,
                    Description = fn.Description,
                    Parameters = JsonSerializer.Deserialize<JsonDocument>(fn.Parameters),
                },
            }).ToList();

            var request = new OllamaChatWithToolsRequest
            {
                Model = effectiveModel,
                Messages = ollamaMessages,
                Tools = tools,
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
                _logger.LogError("Ollama function chat failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw LlmException.GenerationFailed("HTTP " + response.StatusCode + ": " + errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatWithToolsResponse>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result is null)
            {
                throw LlmException.GenerationFailed("Empty response from Ollama");
            }

            // Extract function calls from the response
            List<FunctionCall>? functionCalls = null;
            if (result.Message?.ToolCalls is { Count: > 0 })
            {
                functionCalls = [];
                foreach (var toolCall in result.Message.ToolCalls)
                {
                    var argsJson = toolCall.Function?.Arguments is not null
                        ? JsonSerializer.Serialize(toolCall.Function.Arguments)
                        : "{}";
                    functionCalls.Add(new FunctionCall(
                        null, // Ollama doesn't provide call IDs
                        toolCall.Function?.Name ?? string.Empty,
                        argsJson));
                }
            }

            _logger.LogDebug(
                "Ollama function response: tokens={Tokens}, done={Done}, function_calls={FunctionCallCount}",
                (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0), result.Done, functionCalls?.Count ?? 0);

            return new LlmFunctionResponse(
                Content: result.Message?.Content,
                TokensUsed: (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
                Model: result.Model ?? model,
                FinishReason: result.Done ? "stop" : null,
                FunctionCalls: functionCalls);
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
            _logger.LogError(ex, "Ollama function chat error");
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

        /// <summary>Format for structured output. Set to "json" for JSON mode.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; init; }
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

    // Tool calling DTOs
    private sealed record OllamaChatWithToolsRequest
    {
        public required string Model { get; init; }
        public required List<OllamaChatMessage> Messages { get; init; }
        public List<OllamaTool>? Tools { get; init; }
        public bool Stream { get; init; }
        public OllamaModelOptions? Options { get; init; }
    }

    private sealed record OllamaTool
    {
        public required string Type { get; init; }
        public OllamaToolFunction? Function { get; init; }
    }

    private sealed record OllamaToolFunction
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public JsonDocument? Parameters { get; init; }
    }

    private sealed record OllamaChatWithToolsResponse
    {
        public string? Model { get; init; }
        public OllamaToolMessage? Message { get; init; }
        public bool Done { get; init; }
        public int? PromptEvalCount { get; init; }
        public int? EvalCount { get; init; }
    }

    private sealed record OllamaToolMessage
    {
        public required string Role { get; init; }
        public string? Content { get; init; }
        public List<OllamaToolCall>? ToolCalls { get; init; }
    }

    private sealed record OllamaToolCall
    {
        public OllamaToolCallFunction? Function { get; init; }
    }

    private sealed record OllamaToolCallFunction
    {
        public string? Name { get; init; }
        public JsonElement? Arguments { get; init; }
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
