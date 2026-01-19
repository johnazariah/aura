// <copyright file="AgentReflectionService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of the agent reflection service.
/// </summary>
public sealed class AgentReflectionService : IAgentReflectionService
{
    /// <summary>
    /// Default prompt template name for reflection.
    /// </summary>
    public const string DefaultReflectionPrompt = "agent-reflection";

    private readonly IPromptRegistry _promptRegistry;
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly ILogger<AgentReflectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentReflectionService"/> class.
    /// </summary>
    public AgentReflectionService(
        IPromptRegistry promptRegistry,
        ILlmProviderRegistry providerRegistry,
        ILogger<AgentReflectionService> logger)
    {
        _promptRegistry = promptRegistry;
        _providerRegistry = providerRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ReflectionResult> ReflectAsync(
        string task,
        string response,
        AgentMetadata agentMetadata,
        CancellationToken cancellationToken = default)
    {
        // Only reflect if enabled for this agent
        if (!agentMetadata.Reflection)
        {
            return new ReflectionResult(response, WasModified: false, TokensUsed: 0);
        }

        _logger.LogDebug(
            "Applying reflection for agent with prompt={Prompt}, model={Model}",
            agentMetadata.ReflectionPrompt ?? DefaultReflectionPrompt,
            agentMetadata.ReflectionModel ?? agentMetadata.Model ?? "(default)");

        try
        {
            // Render the reflection prompt
            var promptName = agentMetadata.ReflectionPrompt ?? DefaultReflectionPrompt;
            var renderedPrompt = _promptRegistry.Render(promptName, new { task, response });

            // Get the provider
            var providerName = agentMetadata.Provider ?? "ollama";
            if (!_providerRegistry.TryGetProvider(providerName, out var provider) || provider is null)
            {
                provider = _providerRegistry.GetDefaultProvider();
                if (provider is null)
                {
                    _logger.LogWarning("No LLM provider available for reflection, skipping");
                    return new ReflectionResult(response, WasModified: false, TokensUsed: 0);
                }
            }

            // Use the reflection model if specified, otherwise use the agent's model
            var model = agentMetadata.ReflectionModel ?? agentMetadata.Model;

            // Call the LLM for reflection
            var reflectionResponse = await provider.GenerateAsync(
                model,
                renderedPrompt,
                temperature: 0.3, // Lower temperature for more consistent review
                cancellationToken);

            var result = reflectionResponse.Content.Trim();

            // Check if approved
            if (result.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Reflection approved original response");
                return new ReflectionResult(response, WasModified: false, TokensUsed: reflectionResponse.TokensUsed);
            }

            // Response was modified
            _logger.LogInformation(
                "Reflection modified response (tokens used: {Tokens})",
                reflectionResponse.TokensUsed);

            return new ReflectionResult(result, WasModified: true, TokensUsed: reflectionResponse.TokensUsed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reflection failed, returning original response");
            return new ReflectionResult(response, WasModified: false, TokensUsed: 0);
        }
    }
}
