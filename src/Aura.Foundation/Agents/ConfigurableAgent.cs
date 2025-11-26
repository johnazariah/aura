// <copyright file="ConfigurableAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text;
using Aura.Foundation.Llm;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent implementation that uses an LLM provider for execution.
/// Created from parsed markdown definitions.
/// </summary>
public sealed class ConfigurableAgent : IAgent
{
    private readonly AgentDefinition _definition;
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgent"/> class.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="logger">Logger instance.</param>
    public ConfigurableAgent(
        AgentDefinition definition,
        ILlmProviderRegistry providerRegistry,
        ILogger<ConfigurableAgent> logger)
    {
        _definition = definition;
        _providerRegistry = providerRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => _definition.AgentId;

    /// <inheritdoc/>
    public AgentMetadata Metadata => _definition.ToMetadata();

    /// <inheritdoc/>
    public async Task<Result<AgentOutput, AgentError>> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing agent {AgentId} with provider {Provider}, model {Model}",
            AgentId, _definition.Provider, _definition.Model);

        // Get the LLM provider
        if (!_providerRegistry.TryGetProvider(_definition.Provider, out var provider) || provider is null)
        {
            _logger.LogWarning(
                "Provider {Provider} not found, trying default",
                _definition.Provider);

            provider = _providerRegistry.GetDefaultProvider();
            if (provider is null)
            {
                return Result.Failure<AgentOutput, AgentError>(
                    AgentError.ProviderUnavailable(_definition.Provider));
            }
        }

        try
        {
            // Build conversation messages
            var messages = BuildMessages(context);

            // Execute via LLM
            var result = await provider.ChatAsync(
                _definition.Model,
                messages,
                _definition.Temperature,
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return Result.Failure<AgentOutput, AgentError>(
                    MapLlmError(result.Error));
            }

            var response = result.Value;

            _logger.LogInformation(
                "Agent {AgentId} completed, tokens={Tokens}",
                AgentId, response.TokensUsed);

            return Result.Success<AgentOutput, AgentError>(
                new AgentOutput(response.Content, response.TokensUsed));
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<AgentOutput, AgentError>(AgentError.Cancelled());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentId} execution failed", AgentId);
            return Result.Failure<AgentOutput, AgentError>(
                AgentError.ExecutionFailed(ex.Message, ex.ToString()));
        }
    }

    private List<ChatMessage> BuildMessages(AgentContext context)
    {
        var messages = new List<ChatMessage>();

        // System prompt with template substitution
        var systemPrompt = SubstituteTemplateVariables(_definition.SystemPrompt, context);
        messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        // Add conversation history
        messages.AddRange(context.ConversationHistory);

        // Add user prompt
        messages.Add(new ChatMessage(ChatRole.User, context.Prompt));

        return messages;
    }

    private static string SubstituteTemplateVariables(string template, AgentContext context)
    {
        var result = new StringBuilder(template);

        // Substitute context properties
        result.Replace("{{context.Prompt}}", context.Prompt);
        result.Replace("{{context.WorkspacePath}}", context.WorkspacePath ?? string.Empty);

        // Substitute properties dictionary
        foreach (var (key, value) in context.Properties)
        {
            result.Replace($"{{{{context.{key}}}}}", value?.ToString() ?? string.Empty);
            result.Replace($"{{{{context.Data.{key}}}}}", value?.ToString() ?? string.Empty);
        }

        // Clean up any remaining template variables with empty strings
        // This handles optional variables that weren't provided
        var remaining = System.Text.RegularExpressions.Regex.Replace(
            result.ToString(),
            @"\{\{[^}]+\}\}",
            string.Empty);

        return remaining;
    }

    private static AgentError MapLlmError(LlmError error)
    {
        return error.Code switch
        {
            LlmErrorCode.Unavailable => AgentError.ProviderUnavailable(error.Message),
            LlmErrorCode.ModelNotFound => AgentError.ModelNotFound(error.Message, "unknown"),
            LlmErrorCode.Timeout => AgentError.Timeout(30),
            LlmErrorCode.Cancelled => AgentError.Cancelled(),
            _ => AgentError.ExecutionFailed(error.Message, error.Details),
        };
    }
}
