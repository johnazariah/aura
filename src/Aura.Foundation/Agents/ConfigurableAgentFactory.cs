// <copyright file="ConfigurableAgentFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Llm;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating ConfigurableAgent instances.
/// </summary>
public sealed class ConfigurableAgentFactory : IAgentFactory
{
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgentFactory"/> class.
    /// </summary>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public ConfigurableAgentFactory(
        ILlmProviderRegistry providerRegistry,
        ILoggerFactory loggerFactory)
    {
        _providerRegistry = providerRegistry;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IAgent CreateAgent(AgentDefinition definition)
    {
        var logger = _loggerFactory.CreateLogger<ConfigurableAgent>();
        return new ConfigurableAgent(definition, _providerRegistry, logger);
    }
}
