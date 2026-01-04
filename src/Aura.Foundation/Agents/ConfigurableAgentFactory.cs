// <copyright file="ConfigurableAgentFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Factory for creating ConfigurableAgent instances.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurableAgentFactory"/> class.
/// </remarks>
/// <param name="providerRegistry">LLM provider registry.</param>
/// <param name="llmOptions">LLM configuration options.</param>
/// <param name="handlebars">Handlebars template engine.</param>
/// <param name="loggerFactory">Logger factory.</param>
/// <param name="toolRegistry">Optional tool registry for tool execution.</param>
/// <param name="confirmationService">Optional confirmation service for tool approval.</param>
/// <param name="toolOptions">Optional tool execution configuration.</param>
public sealed class ConfigurableAgentFactory(
    ILlmProviderRegistry providerRegistry,
    IOptions<LlmOptions> llmOptions,
    IHandlebars handlebars,
    ILoggerFactory loggerFactory,
    IToolRegistry? toolRegistry = null,
    IToolConfirmationService? confirmationService = null,
    IOptions<ToolConfirmationOptions>? toolOptions = null) : IAgentFactory
{
    private readonly ILlmProviderRegistry _providerRegistry = providerRegistry;
    private readonly LlmOptions _llmOptions = llmOptions.Value;
    private readonly IHandlebars _handlebars = handlebars;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IToolRegistry? _toolRegistry = toolRegistry;
    private readonly IToolConfirmationService? _confirmationService = confirmationService;
    private readonly ToolConfirmationOptions _toolOptions = toolOptions?.Value ?? new ToolConfirmationOptions();

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

        // Only pass tool support if enabled
        var effectiveToolRegistry = _toolOptions.Enabled ? _toolRegistry : null;
        var effectiveConfirmationService = _toolOptions.Enabled ? _confirmationService : null;

        return new ConfigurableAgent(
            definition,
            _providerRegistry,
            _handlebars,
            logger,
            effectiveToolRegistry,
            effectiveConfirmationService,
            _toolOptions.MaxIterations);
    }
}
