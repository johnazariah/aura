// <copyright file="LlmProviderInitializer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that initializes LLM providers on startup.
/// </summary>
public sealed class LlmProviderInitializer : IHostedService
{
    private readonly ILlmProviderRegistry _registry;
    private readonly OllamaProvider _ollamaProvider;
    private readonly StubLlmProvider _stubProvider;
    private readonly OpenAiProvider? _openAiProvider;
    private readonly AzureOpenAiProvider? _azureOpenAiProvider;
    private readonly ILogger<LlmProviderInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderInitializer"/> class.
    /// </summary>
    /// <param name="registry">Provider registry.</param>
    /// <param name="ollamaProvider">Ollama provider instance.</param>
    /// <param name="stubProvider">Stub provider instance.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="openAiProvider">Optional OpenAI provider instance.</param>
    /// <param name="azureOpenAiProvider">Optional Azure OpenAI provider instance.</param>
    public LlmProviderInitializer(
        ILlmProviderRegistry registry,
        OllamaProvider ollamaProvider,
        StubLlmProvider stubProvider,
        ILogger<LlmProviderInitializer> logger,
        OpenAiProvider? openAiProvider = null,
        AzureOpenAiProvider? azureOpenAiProvider = null)
    {
        _registry = registry;
        _ollamaProvider = ollamaProvider;
        _stubProvider = stubProvider;
        _openAiProvider = openAiProvider;
        _azureOpenAiProvider = azureOpenAiProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing LLM providers");

        // Register Ollama provider (primary for local models)
        _registry.Register(_ollamaProvider);

        // Check Ollama health
        var isOllamaHealthy = await _ollamaProvider.IsHealthyAsync(cancellationToken);
        if (isOllamaHealthy)
        {
            var models = await _ollamaProvider.ListModelsAsync(cancellationToken);
            _logger.LogInformation(
                "Ollama is healthy with {ModelCount} models available",
                models.Count);
        }
        else
        {
            _logger.LogWarning("Ollama is not available - local models will not work");
        }

        // Register OpenAI provider if configured
        if (_openAiProvider is not null)
        {
            _registry.Register(_openAiProvider);

            var isOpenAiHealthy = await _openAiProvider.IsHealthyAsync(cancellationToken);
            if (isOpenAiHealthy)
            {
                _logger.LogInformation("OpenAI is configured and healthy");
            }
            else
            {
                _logger.LogWarning("OpenAI is configured but not responding");
            }
        }

        // Register Azure OpenAI provider if configured
        if (_azureOpenAiProvider is not null)
        {
            _registry.Register(_azureOpenAiProvider);

            var isAzureHealthy = await _azureOpenAiProvider.IsHealthyAsync(cancellationToken);
            if (isAzureHealthy)
            {
                _logger.LogInformation("Azure OpenAI is configured and healthy");
            }
            else
            {
                _logger.LogWarning("Azure OpenAI is configured but not responding");
            }
        }

        // Register stub provider (fallback)
        _registry.Register(_stubProvider);

        _logger.LogInformation(
            "LLM providers initialized. {Count} providers registered",
            _registry.Providers.Count);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
