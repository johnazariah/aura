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
    private readonly ILogger<LlmProviderInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderInitializer"/> class.
    /// </summary>
    /// <param name="registry">Provider registry.</param>
    /// <param name="ollamaProvider">Ollama provider instance.</param>
    /// <param name="stubProvider">Stub provider instance.</param>
    /// <param name="logger">Logger instance.</param>
    public LlmProviderInitializer(
        ILlmProviderRegistry registry,
        OllamaProvider ollamaProvider,
        StubLlmProvider stubProvider,
        ILogger<LlmProviderInitializer> logger)
    {
        _registry = registry;
        _ollamaProvider = ollamaProvider;
        _stubProvider = stubProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing LLM providers");

        // Register Ollama provider (primary)
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
            _logger.LogWarning("Ollama is not available - agents will use stub provider as fallback");
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
