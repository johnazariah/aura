// <copyright file="AzureOpenAiProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.ClientModel;
using Aura.Foundation.Agents;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

/// <summary>
/// Azure OpenAI LLM provider implementation.
/// Connects to Azure OpenAI Service for high-quality model inference.
/// </summary>
public sealed class AzureOpenAiProvider : ILlmProvider
{
    private readonly AzureOpenAIClient _client;
    private readonly ILogger<AzureOpenAiProvider> _logger;
    private readonly AzureOpenAiOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAiProvider"/> class.
    /// </summary>
    public AzureOpenAiProvider(
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.Endpoint))
        {
            throw new ArgumentException("Azure OpenAI endpoint is required", nameof(options));
        }

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new ArgumentException("Azure OpenAI API key is required", nameof(options));
        }

        _client = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));
    }

    /// <inheritdoc/>
    public string ProviderId => "azureopenai";

    /// <inheritdoc/>
    public async Task<LlmResponse> GenerateAsync(
        string model,
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
        string model,
        IReadOnlyList<Agents.ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var deploymentName = ResolveDeploymentName(model);

        _logger.LogDebug(
            "Azure OpenAI chat: deployment={Deployment}, messages={MessageCount}, temp={Temperature}",
            deploymentName, messages.Count, temperature);

        try
        {
            var chatClient = _client.GetChatClient(deploymentName);

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
                "Azure OpenAI response: tokens={Tokens}, finish_reason={FinishReason}",
                tokensUsed, completion.FinishReason);

            return new LlmResponse(
                Content: content,
                TokensUsed: tokensUsed,
                Model: deploymentName,
                FinishReason: completion.FinishReason.ToString());
        }
        catch (OperationCanceledException) { throw; }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            _logger.LogError(ex, "Azure OpenAI authentication failed");
            throw LlmException.Unavailable("azureopenai");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError(ex, "Azure OpenAI deployment not found: {Deployment}", deploymentName);
            throw LlmException.ModelNotFound(deploymentName);
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            _logger.LogWarning("Azure OpenAI rate limited");
            throw LlmException.GenerationFailed("Rate limited - too many requests");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure OpenAI request failed: {Status}", ex.Status);
            throw LlmException.GenerationFailed($"Azure OpenAI error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI error");
            throw LlmException.GenerationFailed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        // Azure OpenAI doesn't have a simple "list deployments" API in the SDK
        // We'll assume the model is available if it's configured
        var deploymentName = ResolveDeploymentName(model);
        var isAvailable = !string.IsNullOrEmpty(deploymentName);
        return Task.FromResult(isAvailable);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Return configured deployments as available models
        var models = new List<ModelInfo>();

        if (!string.IsNullOrEmpty(_options.DefaultDeployment))
        {
            models.Add(new ModelInfo(_options.DefaultDeployment));
        }

        foreach (var mapping in _options.ModelDeployments)
        {
            if (!models.Any(m => m.Name == mapping.Value))
            {
                models.Add(new ModelInfo(mapping.Value));
            }
        }

        return Task.FromResult<IReadOnlyList<ModelInfo>>(models);
    }

    /// <summary>
    /// Checks if Azure OpenAI is available and responding.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple health check - try to get a deployment
            var deploymentName = _options.DefaultDeployment ?? _options.ModelDeployments.Values.FirstOrDefault();
            if (string.IsNullOrEmpty(deploymentName))
            {
                return false;
            }

            // Try a minimal completion to verify connectivity
            var chatClient = _client.GetChatClient(deploymentName);
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

    /// <summary>
    /// Resolves a model name to an Azure deployment name.
    /// </summary>
    private string ResolveDeploymentName(string model)
    {
        // Check if there's a direct mapping
        if (_options.ModelDeployments.TryGetValue(model, out var deployment))
        {
            return deployment;
        }

        // Check if the model itself is a deployment name
        if (_options.ModelDeployments.ContainsValue(model))
        {
            return model;
        }

        // Fall back to default deployment or the model name itself
        return _options.DefaultDeployment ?? model;
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
/// Configuration options for Azure OpenAI provider.
/// </summary>
public sealed class AzureOpenAiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "LlmProviders:AzureOpenAI";

    /// <summary>Gets or sets the Azure OpenAI endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the Azure OpenAI API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the default deployment name.</summary>
    public string DefaultDeployment { get; set; } = "gpt-4o";

    /// <summary>Gets or sets model-to-deployment mappings.</summary>
    /// <remarks>
    /// Maps model names (like "gpt-4o") to deployment names.
    /// Example: { "gpt-4o": "my-gpt4o-deployment" }
    /// </remarks>
    public Dictionary<string, string> ModelDeployments { get; set; } = new();

    /// <summary>Gets or sets the maximum tokens for responses.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Gets or sets the timeout in seconds for requests.</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
