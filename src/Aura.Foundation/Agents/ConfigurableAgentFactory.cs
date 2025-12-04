// <copyright file="ConfigurableAgentFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Llm;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Factory for creating ConfigurableAgent instances.
/// </summary>
public sealed class ConfigurableAgentFactory : IAgentFactory
{
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly IHandlebars _handlebars;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AgentOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgentFactory"/> class.
    /// </summary>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="handlebars">Handlebars template engine.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="options">Agent options with configuration overrides.</param>
    public ConfigurableAgentFactory(
        ILlmProviderRegistry providerRegistry,
        IHandlebars handlebars,
        ILoggerFactory loggerFactory,
        IOptions<AgentOptions> options)
    {
        _providerRegistry = providerRegistry;
        _handlebars = handlebars;
        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public IAgent CreateAgent(AgentDefinition definition)
    {
        var originalProvider = definition.Provider;
        var originalModel = definition.Model;
        var configSource = "default";

        // Always start with Ollama as the base default (local-first principle)
        definition = definition with
        {
            Provider = "ollama",
        };

        // First: Apply default LLM provider if configured (overrides Ollama default)
        if (_options.LlmProviders.TryGetValue("default", out var defaultConfig))
        {
            definition = definition with
            {
                Provider = defaultConfig.Provider ?? definition.Provider,
                Model = defaultConfig.Model ?? definition.Model,
                Temperature = defaultConfig.Temperature ?? definition.Temperature,
            };
            configSource = "config:default";
        }

        // Second: Apply per-agent LLM provider if configured (overrides default)
        if (_options.LlmProviders.TryGetValue(definition.AgentId, out var agentConfig))
        {
            definition = definition with
            {
                Provider = agentConfig.Provider ?? definition.Provider,
                Model = agentConfig.Model ?? definition.Model,
                Temperature = agentConfig.Temperature ?? definition.Temperature,
            };
            configSource = "config:agent";
        }

        // Log provider info
        var factoryLogger = _loggerFactory.CreateLogger<ConfigurableAgentFactory>();
        if (definition.Provider != originalProvider || definition.Model != originalModel)
        {
            factoryLogger.LogInformation(
                "Agent {AgentId}: Using {Provider}/{Model} ({ConfigSource}, markdown had {OriginalProvider}/{OriginalModel})",
                definition.AgentId,
                definition.Provider,
                definition.Model,
                configSource,
                originalProvider,
                originalModel);
        }
        else
        {
            factoryLogger.LogDebug(
                "Agent {AgentId}: Using {Provider}/{Model}",
                definition.AgentId,
                definition.Provider,
                definition.Model);
        }

        var logger = _loggerFactory.CreateLogger<ConfigurableAgent>();
        return new ConfigurableAgent(definition, _providerRegistry, _handlebars, logger);
    }
}
