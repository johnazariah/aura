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
    private readonly StubLlmProvider _stubProvider;
    private readonly ILogger<LlmProviderInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderInitializer"/> class.
    /// </summary>
    /// <param name="registry">Provider registry.</param>
    /// <param name="stubProvider">Stub provider instance.</param>
    /// <param name="logger">Logger instance.</param>
    public LlmProviderInitializer(
        ILlmProviderRegistry registry,
        StubLlmProvider stubProvider,
        ILogger<LlmProviderInitializer> logger)
    {
        _registry = registry;
        _stubProvider = stubProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing LLM providers");

        // Register stub provider (Phase 2 will add OllamaProvider)
        _registry.Register(_stubProvider);

        _logger.LogInformation(
            "LLM providers initialized. {Count} providers registered",
            _registry.Providers.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
