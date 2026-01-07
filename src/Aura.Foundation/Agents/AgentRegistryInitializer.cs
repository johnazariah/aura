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
/// <remarks>
/// Initializes a new instance of the <see cref="AgentRegistryInitializer"/> class.
/// </remarks>
/// <param name="registry">Agent registry.</param>
/// <param name="hardcodedProviders">Hardcoded agent providers.</param>
/// <param name="options">Agent options.</param>
/// <param name="environment">Host environment for resolving paths.</param>
/// <param name="logger">Logger instance.</param>
public sealed class AgentRegistryInitializer(
    IAgentRegistry registry,
    IEnumerable<IHardcodedAgentProvider> hardcodedProviders,
    IOptions<AgentOptions> options,
    IHostEnvironment environment,
    ILogger<AgentRegistryInitializer> logger) : IHostedService
{
    private readonly IAgentRegistry _registry = registry;
    private readonly IEnumerable<IHardcodedAgentProvider> _hardcodedProviders = hardcodedProviders;
    private readonly AgentOptions _options = options.Value;
    private readonly IHostEnvironment _environment = environment;
    private readonly ILogger<AgentRegistryInitializer> _logger = logger;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing agent registry");

        // Load markdown agents from directories FIRST
        if (_registry is AgentRegistry agentRegistry)
        {
            foreach (var directory in _options.Directories)
            {
                // Resolve relative paths against content root (important for Windows Services)
                var resolvedPath = Path.IsPathRooted(directory)
                    ? directory
                    : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, directory));

                _logger.LogDebug("Resolved agent directory: {Original} -> {Resolved}", directory, resolvedPath);
                agentRegistry.AddWatchDirectory(resolvedPath, _options.EnableHotReload);
            }

            await agentRegistry.ReloadAsync().ConfigureAwait(false);
        }

        // Load hardcoded agents AFTER markdown agents
        // Mark them as hardcoded so they survive hot-reload
        var hardcodedCount = 0;
        foreach (var provider in _hardcodedProviders)
        {
            foreach (var agent in provider.GetAgents())
            {
                if (_registry is AgentRegistry registry)
                {
                    registry.Register(agent, isHardcoded: true);
                }
                else
                {
                    _registry.Register(agent);
                }

                hardcodedCount++;
                _logger.LogDebug("Registered hardcoded agent: {AgentId} from {Provider}",
                    agent.AgentId, provider.GetType().Name);
            }
        }

        if (hardcodedCount > 0)
        {
            _logger.LogInformation("Registered {Count} hardcoded agents", hardcodedCount);
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
