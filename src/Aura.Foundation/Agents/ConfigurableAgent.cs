// <copyright file="ConfigurableAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text;
using Aura.Foundation.Llm;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent implementation that uses an LLM provider for execution.
/// Created from parsed markdown definitions.
/// </summary>
public sealed class ConfigurableAgent : IAgent
{
    private readonly AgentDefinition _definition;
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly IHandlebars _handlebars;
    private readonly HandlebarsTemplate<object, object> _compiledTemplate;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgent"/> class.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="handlebars">Handlebars template engine.</param>
    /// <param name="logger">Logger instance.</param>
    public ConfigurableAgent(
        AgentDefinition definition,
        ILlmProviderRegistry providerRegistry,
        IHandlebars handlebars,
        ILogger<ConfigurableAgent> logger)
    {
        _definition = definition;
        _providerRegistry = providerRegistry;
        _handlebars = handlebars;
        _logger = logger;

        // Pre-compile the system prompt template
        _compiledTemplate = _handlebars.Compile(_definition.SystemPrompt);
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
            AgentId, _definition.Provider, _definition.Model ?? "(provider default)");

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

        // Build template context object for Handlebars
        var templateContext = new
        {
            context = new
            {
                Prompt = context.Prompt,
                WorkspacePath = context.WorkspacePath ?? string.Empty,
                RagContext = context.RagContext ?? string.Empty,
                Data = context.Properties,
            },
            // Also expose at top level for simpler templates
            ragContext = context.RagContext ?? string.Empty,
        };

        // Render system prompt using Handlebars
        var systemPrompt = _compiledTemplate(templateContext);

        // If RAG context wasn't included in template but is available, append it
        if (!string.IsNullOrEmpty(context.RagContext) && !_definition.SystemPrompt.Contains("RagContext"))
        {
            systemPrompt = AppendRagContext(systemPrompt, context.RagContext);
        }

        messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        // Add conversation history
        messages.AddRange(context.ConversationHistory);

        // Add user prompt
        messages.Add(new ChatMessage(ChatRole.User, context.Prompt));

        return messages;
    }

    private static string AppendRagContext(string systemPrompt, string ragContext)
    {
        var sb = new StringBuilder(systemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("## Relevant Context from Knowledge Base");
        sb.AppendLine();
        sb.AppendLine("The following information may be relevant to the user's request:");
        sb.AppendLine();
        sb.Append(ragContext);

        return sb.ToString();
    }
}