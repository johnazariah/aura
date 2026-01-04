// <copyright file="ConfigurableAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using System.Text;
using System.Text.Json;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent implementation that uses an LLM provider for execution.
/// Created from parsed markdown definitions. Supports tool execution via function calling.
/// </summary>
public sealed class ConfigurableAgent : IAgent
{
    private readonly AgentDefinition _definition;
    private readonly ILlmProviderRegistry _providerRegistry;
    private readonly IToolRegistry? _toolRegistry;
    private readonly IToolConfirmationService? _confirmationService;
    private readonly IHandlebars _handlebars;
    private readonly HandlebarsTemplate<object, object> _compiledTemplate;
    private readonly ILogger _logger;
    private readonly int _maxToolIterations;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurableAgent"/> class.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <param name="providerRegistry">LLM provider registry.</param>
    /// <param name="handlebars">Handlebars template engine.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="toolRegistry">Optional tool registry for tool execution.</param>
    /// <param name="confirmationService">Optional confirmation service for tool approval.</param>
    /// <param name="maxToolIterations">Maximum number of tool execution iterations.</param>
    public ConfigurableAgent(
        AgentDefinition definition,
        ILlmProviderRegistry providerRegistry,
        IHandlebars handlebars,
        ILogger<ConfigurableAgent> logger,
        IToolRegistry? toolRegistry = null,
        IToolConfirmationService? confirmationService = null,
        int maxToolIterations = 10)
    {
        _definition = definition;
        _providerRegistry = providerRegistry;
        _toolRegistry = toolRegistry;
        _confirmationService = confirmationService;
        _handlebars = handlebars;
        _logger = logger;
        _maxToolIterations = maxToolIterations;

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

            // Resolve available tools from agent definition
            var tools = ResolveTools();

            // If we have tools, use the function calling path
            if (tools.Count > 0)
            {
                return await ExecuteWithToolsAsync(provider, messages, tools, context, cancellationToken).ConfigureAwait(false);
            }

            // Execute via LLM without tools
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

    /// <summary>
    /// Resolves available tools from the agent definition.
    /// </summary>
    private List<ToolDefinition> ResolveTools()
    {
        if (_toolRegistry is null || _definition.Tools.Count == 0)
        {
            return [];
        }

        var tools = new List<ToolDefinition>();
        foreach (var toolName in _definition.Tools)
        {
            var tool = _toolRegistry.GetTool(toolName);
            if (tool is not null)
            {
                tools.Add(tool);
            }
            else
            {
                _logger.LogWarning("Tool '{ToolName}' declared in agent '{AgentId}' was not found in registry", toolName, AgentId);
            }
        }

        return tools;
    }

    /// <summary>
    /// Converts tool definitions to LLM function definitions.
    /// </summary>
    private static List<FunctionDefinition> ToFunctionDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new FunctionDefinition(
            t.ToolId,
            t.Description,
            t.InputSchema ?? "{\"type\": \"object\", \"properties\": {}}"))
            .ToList();
    }

    /// <summary>
    /// Executes the agent with tool support using function calling.
    /// </summary>
    private async Task<AgentOutput> ExecuteWithToolsAsync(
        ILlmProvider provider,
        List<ChatMessage> messages,
        List<ToolDefinition> tools,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Agent {AgentId} executing with {ToolCount} tools",
            AgentId, tools.Count);

        var functions = ToFunctionDefinitions(tools);
        var allToolCalls = new List<ToolCall>();
        var totalTokens = 0;
        List<FunctionResultMessage>? functionResults = null;

        for (var iteration = 0; iteration < _maxToolIterations; iteration++)
        {
            var response = await provider.ChatWithFunctionsAsync(
                _definition.Model,
                messages,
                functions,
                functionResults,
                _definition.Temperature,
                cancellationToken).ConfigureAwait(false);

            totalTokens += response.TokensUsed;

            // If no function calls, we're done - return the content
            if (!response.HasFunctionCalls)
            {
                _logger.LogInformation(
                    "Agent {AgentId} completed after {Iterations} iteration(s), tokens={Tokens}, tool_calls={ToolCalls}",
                    AgentId, iteration + 1, totalTokens, allToolCalls.Count);

                return new AgentOutput(
                    response.Content ?? string.Empty,
                    totalTokens,
                    allToolCalls.Count > 0 ? allToolCalls : null);
            }

            // Execute each function call
            functionResults = [];
            foreach (var call in response.FunctionCalls!)
            {
                var tool = tools.FirstOrDefault(t => t.ToolId == call.Name);
                if (tool is null)
                {
                    _logger.LogWarning("LLM requested unknown tool '{ToolName}'", call.Name);
                    functionResults.Add(new FunctionResultMessage(
                        call.Id,
                        call.Name,
                        JsonSerializer.Serialize(new { error = $"Unknown tool: {call.Name}" })));
                    continue;
                }

                // Handle confirmation if required
                if (tool.RequiresConfirmation && _confirmationService is not null)
                {
                    var approved = await _confirmationService.RequestApprovalAsync(
                        tool.ToolId,
                        tool.Description,
                        call.ArgumentsJson,
                        cancellationToken).ConfigureAwait(false);

                    if (!approved)
                    {
                        _logger.LogInformation("Tool '{ToolId}' execution rejected by user", tool.ToolId);
                        functionResults.Add(new FunctionResultMessage(
                            call.Id,
                            call.Name,
                            JsonSerializer.Serialize(new { error = "Tool execution was rejected by user" })));

                        allToolCalls.Add(new ToolCall(call.Name, call.ArgumentsJson, "rejected"));
                        continue;
                    }
                }

                // Execute the tool
                try
                {
                    var input = ParseToolInput(call.Name, call.ArgumentsJson, context.WorkspacePath);
                    var result = await _toolRegistry!.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);

                    var resultJson = result.Success
                        ? JsonSerializer.Serialize(result.Output)
                        : JsonSerializer.Serialize(new { error = result.Error });

                    functionResults.Add(new FunctionResultMessage(call.Id, call.Name, resultJson));
                    allToolCalls.Add(new ToolCall(call.Name, call.ArgumentsJson, resultJson));

                    _logger.LogDebug(
                        "Tool '{ToolId}' executed: success={Success}, duration={Duration}ms",
                        tool.ToolId, result.Success, result.Duration.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool '{ToolId}' execution failed", tool.ToolId);
                    var errorResult = JsonSerializer.Serialize(new { error = ex.Message });
                    functionResults.Add(new FunctionResultMessage(call.Id, call.Name, errorResult));
                    allToolCalls.Add(new ToolCall(call.Name, call.ArgumentsJson, errorResult));
                }
            }

            // Add assistant message with tool calls for context (needed for multi-turn)
            if (response.Content is not null)
            {
                messages.Add(new ChatMessage(ChatRole.Assistant, response.Content));
            }
        }

        // Exceeded max iterations
        throw AgentException.ExecutionFailed($"Agent exceeded maximum tool iterations ({_maxToolIterations})");
    }

    /// <summary>
    /// Parses tool input from function call arguments.
    /// </summary>
    private static ToolInput ParseToolInput(string toolId, string argumentsJson, string? workspacePath)
    {
        var parameters = string.IsNullOrEmpty(argumentsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson) ?? [];

        return new ToolInput
        {
            ToolId = toolId,
            WorkingDirectory = workspacePath,
            Parameters = parameters,
        };
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
