// <copyright file="PromptRegistryInitializer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Initializes the prompt registry on startup.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PromptRegistryInitializer"/> class.
/// </remarks>
public sealed class PromptRegistryInitializer(
    IPromptRegistry registry,
    IHostEnvironment environment,
    IOptions<PromptOptions> options,
    ILogger<PromptRegistryInitializer> logger) : IHostedService
{
    private readonly IPromptRegistry _registry = registry;
    private readonly PromptRegistry _promptRegistry = registry as PromptRegistry ?? throw new InvalidOperationException("Expected PromptRegistry");
    private readonly IHostEnvironment _environment = environment;
    private readonly PromptOptions _options = options.Value;
    private readonly ILogger<PromptRegistryInitializer> _logger = logger;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing prompt registry...");

        // Resolve relative paths against content root
        foreach (var directory in _options.Directories)
        {
            var resolvedPath = Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(_environment.ContentRootPath, directory);

            _logger.LogDebug("Loading prompts from: {Path}", resolvedPath);
            _promptRegistry.LoadFromDirectory(resolvedPath);
        }

        _logger.LogInformation("Prompt registry initialized with {Count} prompts: {Names}",
            _registry.GetPromptNames().Count,
            string.Join(", ", _registry.GetPromptNames()));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
