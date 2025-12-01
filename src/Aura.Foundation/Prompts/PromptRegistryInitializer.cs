// <copyright file="PromptRegistryInitializer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Initializes the prompt registry on startup.
/// </summary>
public sealed class PromptRegistryInitializer : IHostedService
{
    private readonly IPromptRegistry _registry;
    private readonly ILogger<PromptRegistryInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRegistryInitializer"/> class.
    /// </summary>
    public PromptRegistryInitializer(
        IPromptRegistry registry,
        ILogger<PromptRegistryInitializer> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing prompt registry...");
        _registry.Reload();
        _logger.LogInformation("Prompt registry initialized with {Count} prompts", _registry.GetPromptNames().Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
