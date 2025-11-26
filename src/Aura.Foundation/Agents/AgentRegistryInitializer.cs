// <copyright file="AgentRegistryInitializer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that initializes the agent registry on startup.
/// </summary>
public sealed class AgentRegistryInitializer : IHostedService
{
    private readonly IAgentRegistry _registry;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentRegistryInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRegistryInitializer"/> class.
    /// </summary>
    /// <param name="registry">Agent registry.</param>
    /// <param name="options">Agent options.</param>
    /// <param name="logger">Logger instance.</param>
    public AgentRegistryInitializer(
        IAgentRegistry registry,
        IOptions<AgentOptions> options,
        ILogger<AgentRegistryInitializer> logger)
    {
        _registry = registry;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing agent registry");

        if (_registry is AgentRegistry agentRegistry)
        {
            foreach (var directory in _options.Directories)
            {
                agentRegistry.AddWatchDirectory(directory, _options.EnableHotReload);
            }

            await agentRegistry.ReloadAsync().ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Agent registry initialized. {Count} agents loaded",
            _registry.Agents.Count);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registry is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }
}
