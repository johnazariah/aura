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
    private readonly LlmOptions _llmOptions;
    private readonly IHandlebars _handlebars;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgentFactory"/> class.
    /// </summary>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="llmOptions">LLM configuration options.</param>
    /// <param name="handlebars">Handlebars template engine.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public ConfigurableAgentFactory(
        ILlmProviderRegistry providerRegistry,
        IOptions<LlmOptions> llmOptions,
        IHandlebars handlebars,
        ILoggerFactory loggerFactory)
    {
        _providerRegistry = providerRegistry;
        _llmOptions = llmOptions.Value;
        _handlebars = handlebars;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IAgent CreateAgent(AgentDefinition definition)
    {
        var factoryLogger = _loggerFactory.CreateLogger<ConfigurableAgentFactory>();

        // Use the global default provider from Aura:Llm configuration
        var effectiveProvider = _llmOptions.DefaultProvider;

        // Log if overriding the agent's markdown-defined provider
        if (!string.IsNullOrEmpty(definition.Provider) && definition.Provider != effectiveProvider)
        {
            factoryLogger.LogDebug(
                "Agent {AgentId}: Using configured default provider '{EffectiveProvider}' (markdown specified '{MarkdownProvider}')",
                definition.AgentId,
                effectiveProvider,
                definition.Provider);
        }

        // Update definition with effective provider
        definition = definition with
        {
            Provider = effectiveProvider,
        };

        factoryLogger.LogDebug(
            "Agent {AgentId}: Using {Provider}/{Model}",
            definition.AgentId,
            definition.Provider,
            definition.Model ?? _llmOptions.DefaultModel);

        var logger = _loggerFactory.CreateLogger<ConfigurableAgent>();
        return new ConfigurableAgent(definition, _providerRegistry, _handlebars, logger);
    }
}
