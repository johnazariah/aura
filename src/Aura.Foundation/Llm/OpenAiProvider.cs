// <copyright file="OpenAiProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.ClientModel;
using Aura.Foundation.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

/// <summary>
/// OpenAI LLM provider implementation.
/// Connects directly to OpenAI API for high-quality model inference.
/// </summary>
public sealed class OpenAiProvider : ILlmProvider
{
    private readonly OpenAIClient _client;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly OpenAiOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiProvider"/> class.
    /// </summary>
    public OpenAiProvider(
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new ArgumentException("OpenAI API key is required", nameof(options));
        }

        _client = new OpenAIClient(_options.ApiKey);
    }

    /// <inheritdoc/>
    public string ProviderId => "openai";

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(
        string? model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        // For generate, we wrap in a single user message
        var messages = new List<Agents.ChatMessage> { new(Agents.ChatRole.User, prompt) };
        return await ChatAsync(model, messages, temperature, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<Agents.ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var modelName = ResolveModelName(model);

        _logger.LogDebug(
            "OpenAI chat: model={Model}, messages={MessageCount}, temp={Temperature}",
            modelName, messages.Count, temperature);

        try
        {
            var chatClient = _client.GetChatClient(modelName);

            var chatMessages = messages.Select(m => ConvertMessage(m)).ToList();

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = (float)temperature,
                MaxOutputTokenCount = _options.MaxTokens,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await chatClient.CompleteChatAsync(chatMessages, chatOptions, linked.Token).ConfigureAwait(false);
            var completion = response.Value;

            var content = string.Join(string.Empty, completion.Content.Select(c => c.Text));
            var tokensUsed = (completion.Usage?.InputTokenCount ?? 0) + (completion.Usage?.OutputTokenCount ?? 0);

            _logger.LogDebug(
                "OpenAI response: tokens={Tokens}, finish_reason={FinishReason}",
                tokensUsed, completion.FinishReason);

            return new LlmResponse(
                Content: content,
                TokensUsed: tokensUsed,
                Model: modelName,
                FinishReason: completion.FinishReason.ToString());
        }
        catch (OperationCanceledException) { throw; }
        catch (ClientResultException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            _logger.LogError(ex, "OpenAI authentication failed");
            throw LlmException.Unavailable("openai");
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            _logger.LogError(ex, "OpenAI model not found: {Model}", modelName);
            throw LlmException.ModelNotFound(modelName);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning("OpenAI rate limited");
            throw LlmException.GenerationFailed("Rate limited - too many requests");
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "OpenAI request failed: {Status}", ex.Status);
            throw LlmException.GenerationFailed($"OpenAI error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<Agents.ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken = default)
    {
        var modelName = model ?? _options.DefaultModel;
        var callStart = DateTime.UtcNow;

        _logger.LogDebug(
            "OpenAI chat with options: model={Model}, messages={MessageCount}, hasSchema={HasSchema}",
            modelName, messages.Count, options.ResponseSchema is not null);

        try
        {
            var chatClient = _client.GetChatClient(modelName);

            var chatMessages = messages.Select(m => ConvertMessage(m)).ToList();

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = (float)options.Temperature,
                MaxOutputTokenCount = _options.MaxTokens,
            };

            // Add structured output schema if specified
            if (options.ResponseSchema is not null)
            {
                var schema = options.ResponseSchema;
                chatOptions.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    schema.Name,
                    BinaryData.FromString(schema.Schema.GetRawText()),
                    jsonSchemaFormatDescription: schema.Description,
                    jsonSchemaIsStrict: schema.Strict);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await chatClient.CompleteChatAsync(chatMessages, chatOptions, linked.Token).ConfigureAwait(false);
            var completion = response.Value;

            var content = string.Join(string.Empty, completion.Content.Select(c => c.Text));
            var tokensUsed = (completion.Usage?.InputTokenCount ?? 0) + (completion.Usage?.OutputTokenCount ?? 0);
            var callDuration = DateTime.UtcNow - callStart;

            _logger.LogDebug(
                "OpenAI chat with options completed in {Duration:F1}s: tokens={Tokens}",
                callDuration.TotalSeconds, tokensUsed);

            return new LlmResponse(
                Content: content,
                TokensUsed: tokensUsed,
                Model: modelName,
                FinishReason: completion.FinishReason.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "OpenAI request failed: {Status}", ex.Status);
            throw LlmException.GenerationFailed($"OpenAI error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LlmToken> StreamChatAsync(
        string? model,
        IReadOnlyList<Agents.ChatMessage> messages,
        double temperature = 0.7,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modelName = ResolveModelName(model);

        _logger.LogDebug(
            "OpenAI streaming chat: model={Model}, messages={MessageCount}",
            modelName, messages.Count);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        AsyncCollectionResult<StreamingChatCompletionUpdate>? streamingResult = null;

        try
        {
            var chatClient = _client.GetChatClient(modelName);
            var chatMessages = messages.Select(m => ConvertMessage(m)).ToList();

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = (float)temperature,
                MaxOutputTokenCount = _options.MaxTokens,
            };

            streamingResult = chatClient.CompleteChatStreamingAsync(chatMessages, chatOptions, linked.Token);
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "OpenAI streaming request failed: {Status}", ex.Status);
            throw LlmException.GenerationFailed($"OpenAI error: {ex.Message}", ex);
        }

        int totalTokens = 0;
        string? finishReason = null;

        await foreach (var update in streamingResult.WithCancellation(linked.Token).ConfigureAwait(false))
        {
            // Track completion info
            if (update.FinishReason.HasValue)
            {
                finishReason = update.FinishReason.Value.ToString();
            }

            if (update.Usage is not null)
            {
                totalTokens = (update.Usage.InputTokenCount) + (update.Usage.OutputTokenCount);
            }

            // Yield content deltas
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new LlmToken(Content: part.Text);
                }
            }
        }

        // Yield the final completion token
        yield return new LlmToken(
            Content: string.Empty,
            IsComplete: true,
            FinishReason: finishReason ?? "stop",
            TokensUsed: totalTokens);
    }

    /// <inheritdoc/>
    public Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        // OpenAI models are generally available if API key is valid
        // We'll assume the model is available
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Return common OpenAI models
        var models = new List<ModelInfo>
        {
            new("gpt-4o"),
            new("gpt-4o-mini"),
            new("gpt-4-turbo"),
            new("gpt-4"),
            new("gpt-3.5-turbo"),
        };

        return Task.FromResult<IReadOnlyList<ModelInfo>>(models);
    }

    /// <summary>
    /// Checks if OpenAI is available and responding.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.DefaultModel);
            var messages = new List<OpenAI.Chat.ChatMessage> { new UserChatMessage("Hi") };
            var options = new ChatCompletionOptions { MaxOutputTokenCount = 1 };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            await chatClient.CompleteChatAsync(messages, options, linked.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<LlmFunctionResponse> ChatWithFunctionsAsync(
        string? model,
        IReadOnlyList<Agents.ChatMessage> messages,
        IReadOnlyList<FunctionDefinition> functions,
        IReadOnlyList<FunctionResultMessage>? functionResults = null,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var modelName = ResolveModelName(model);

        _logger.LogDebug(
            "OpenAI function call: model={Model}, messages={MessageCount}, functions={FunctionCount}",
            modelName, messages.Count, functions.Count);

        try
        {
            var chatClient = _client.GetChatClient(modelName);

            // Build the message list including function results
            var chatMessages = new List<OpenAI.Chat.ChatMessage>();
            foreach (var m in messages)
            {
                chatMessages.Add(ConvertMessage(m));
            }

            // Add function result messages if provided
            if (functionResults is { Count: > 0 })
            {
                foreach (var result in functionResults)
                {
                    chatMessages.Add(new ToolChatMessage(result.CallId ?? result.Name, result.Result));
                }
            }

            // Build chat options with tools
            var chatOptions = new ChatCompletionOptions
            {
                Temperature = (float)temperature,
                MaxOutputTokenCount = _options.MaxTokens,
            };

            // Add tools (functions)
            foreach (var fn in functions)
            {
                var tool = ChatTool.CreateFunctionTool(
                    fn.Name,
                    fn.Description,
                    BinaryData.FromString(fn.Parameters));
                chatOptions.Tools.Add(tool);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var response = await chatClient.CompleteChatAsync(chatMessages, chatOptions, linked.Token).ConfigureAwait(false);
            var completion = response.Value;

            var content = string.Join(string.Empty, completion.Content.Select(c => c.Text));
            var tokensUsed = (completion.Usage?.InputTokenCount ?? 0) + (completion.Usage?.OutputTokenCount ?? 0);

            // Extract function calls from the response
            List<FunctionCall>? functionCalls = null;
            if (completion.ToolCalls is { Count: > 0 })
            {
                functionCalls = [];
                foreach (var toolCall in completion.ToolCalls)
                {
                    functionCalls.Add(new FunctionCall(
                        toolCall.Id,
                        toolCall.FunctionName,
                        toolCall.FunctionArguments.ToString()));
                }
            }

            _logger.LogDebug(
                "OpenAI function response: tokens={Tokens}, finish_reason={FinishReason}, function_calls={FunctionCallCount}",
                tokensUsed, completion.FinishReason, functionCalls?.Count ?? 0);

            return new LlmFunctionResponse(
                Content: string.IsNullOrEmpty(content) ? null : content,
                TokensUsed: tokensUsed,
                Model: modelName,
                FinishReason: completion.FinishReason.ToString(),
                FunctionCalls: functionCalls);
        }
        catch (OperationCanceledException) { throw; }
        catch (ClientResultException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            _logger.LogError(ex, "OpenAI authentication failed");
            throw LlmException.Unavailable("openai");
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            _logger.LogError(ex, "OpenAI model not found: {Model}", modelName);
            throw LlmException.ModelNotFound(modelName);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning("OpenAI rate limited");
            throw LlmException.GenerationFailed("Rate limited - too many requests");
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "OpenAI request failed: {Status}", ex.Status);
            throw LlmException.GenerationFailed($"OpenAI error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI function call error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <summary>
    /// Resolves a model name to an OpenAI model identifier.
    /// </summary>
    private string ResolveModelName(string? model)
    {
        // Use default if null or empty
        if (string.IsNullOrEmpty(model))
        {
            return _options.DefaultModel;
        }

        // Check if there's a mapping
        if (_options.ModelMappings.TryGetValue(model, out var mappedModel))
        {
            return mappedModel;
        }

        // Use the model name as-is
        return model;
    }

    /// <summary>
    /// Converts our ChatMessage to OpenAI ChatMessage.
    /// </summary>
    private static OpenAI.Chat.ChatMessage ConvertMessage(Agents.ChatMessage message)
    {
        return message.Role switch
        {
            Agents.ChatRole.System => new SystemChatMessage(message.Content),
            Agents.ChatRole.User => new UserChatMessage(message.Content),
            Agents.ChatRole.Assistant => new AssistantChatMessage(message.Content),
            _ => new UserChatMessage(message.Content),
        };
    }
}

/// <summary>
/// Configuration options for OpenAI provider.
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Llm:Providers:OpenAI";

    /// <summary>Gets or sets the OpenAI API key. Required.</summary>
    public required string ApiKey { get; set; }

    /// <summary>Gets or sets the default model. Required - must be set in configuration.</summary>
    public required string DefaultModel { get; set; }

    /// <summary>Gets or sets model name mappings.</summary>
    /// <remarks>
    /// Maps friendly names to OpenAI model identifiers.
    /// Example: { "fast": "gpt-4o-mini", "smart": "gpt-4o" }
    /// </remarks>
    public Dictionary<string, string> ModelMappings { get; set; } = new();

    /// <summary>Gets or sets the maximum tokens for responses.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Gets or sets the timeout in seconds for requests.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
