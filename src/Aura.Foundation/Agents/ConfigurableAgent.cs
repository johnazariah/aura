// <copyright file="ConfigurableAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text;
using Aura.Foundation.Llm;
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
    public async Task<AgentOutput> ExecuteAsync(
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
                throw AgentException.ProviderUnavailable(_definition.Provider);
            }
        }

        try
        {
            // Build conversation messages
            var messages = BuildMessages(context);

            // Execute via LLM
            var response = await provider.ChatAsync(
                _definition.Model,
                messages,
                _definition.Temperature,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Agent {AgentId} completed, tokens={Tokens}",
                AgentId, response.TokensUsed);

            return new AgentOutput(response.Content, response.TokensUsed);
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (LlmException ex)
        {
            _logger.LogError(ex, "Agent {AgentId} LLM execution failed", AgentId);
            throw AgentException.ExecutionFailed(ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentId} execution failed", AgentId);
            throw AgentException.ExecutionFailed(ex.Message, ex);
        }
    }

    private List<ChatMessage> BuildMessages(AgentContext context)
    {
        var messages = new List<ChatMessage>();

        // System prompt with template substitution
        var systemPrompt = SubstituteTemplateVariables(_definition.SystemPrompt, context);
        
        // Inject RAG context if available
        if (!string.IsNullOrEmpty(context.RagContext))
        {
            systemPrompt = InjectRagContext(systemPrompt, context.RagContext);
        }
        
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
        result.Replace("{{ragContext}}", context.RagContext ?? string.Empty);

        // Substitute properties dictionary
        foreach (var (key, value) in context.Properties)
        {
            result.Replace("{{context." + key + "}}", value?.ToString() ?? string.Empty);
            result.Replace("{{context.Data." + key + "}}", value?.ToString() ?? string.Empty);
        }

        // Clean up any remaining template variables with empty strings
        // This handles optional variables that were not provided
        var remaining = System.Text.RegularExpressions.Regex.Replace(
            result.ToString(),
            @"\{\{[^}]+\}\}",
            string.Empty);

        return remaining;
    }

    private static string InjectRagContext(string systemPrompt, string ragContext)
    {
        // If the system prompt already contains a RAG context placeholder, it was substituted
        // If not, append the RAG context as additional context
        if (systemPrompt.Contains("{{ragContext}}"))
        {
            return systemPrompt; // Already handled by template substitution
        }

        // Append RAG context to the system prompt
        var sb = new StringBuilder(systemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("## Relevant Context from Knowledge Base");
        sb.AppendLine();
        sb.AppendLine("The following information may be relevant to the user's question:");
        sb.AppendLine();
        sb.Append(ragContext);

        return sb.ToString();
    }
}
