// <copyright file="LanguageSpecialistAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Language specialist agent that loads behavior from YAML configuration.
/// Provides coding capabilities for any language with CLI tools.
/// </summary>
/// <remarks>
/// This agent is created dynamically from YAML files in agents/languages/*.yaml.
/// Each language gets its own agent instance with language-specific tools and prompts.
/// </remarks>
public sealed class LanguageSpecialistAgent : IAgent
{
    private readonly LanguageConfig _config;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageSpecialistAgent"/> class.
    /// </summary>
    /// <param name="config">The language configuration.</param>
    /// <param name="reactExecutor">ReAct executor for agentic workflows.</param>
    /// <param name="toolRegistry">Tool registry for accessing tools.</param>
    /// <param name="llmRegistry">LLM provider registry.</param>
    /// <param name="logger">Logger instance.</param>
    public LanguageSpecialistAgent(
        LanguageConfig config,
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmRegistry,
        ILogger logger)
    {
        _config = config;
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _llmRegistry = llmRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => $"{_config.Language.Id}-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata => new(
        Name: $"{_config.Language.Name} Coding Agent",
        Description: $"Configurable coding agent for {_config.Language.Name}. " +
                     "Uses CLI tools and ReAct reasoning to understand, plan, and implement code changes.",
        Capabilities: BuildCapabilities(),
        Priority: _config.Priority,
        Languages: [_config.Language.Id],
        Provider: _config.Agent.Provider,
        Model: _config.Agent.Model,
        Temperature: _config.Agent.Temperature,
        Tools: _config.Tools.Values.Select(t => t.Id).ToList(),
        Tags: ["coding", _config.Language.Id, "agentic", "configurable"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting {Language} coding agent for task: {Task}",
            _config.Language.Name,
            context.Prompt.Length > 100 ? context.Prompt[..100] + "..." : context.Prompt);

        var llmProvider = _llmRegistry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No LLM provider available");

        // Get available tools (file tools + language tools)
        var availableTools = GetAvailableTools();

        _logger.LogDebug("Agent has access to {ToolCount} tools", availableTools.Count);

        // Build the enhanced task with context
        var enhancedTask = BuildEnhancedTask(context);

        // Build ReAct options
        var options = new ReActOptions
        {
            MaxSteps = _config.Agent.MaxSteps,
            Temperature = _config.Agent.Temperature,
            Model = _config.Agent.Model,
            WorkingDirectory = context.WorkspacePath,
            AdditionalContext = BuildAdditionalContext(context),
        };

        try
        {
            var result = await _reactExecutor.ExecuteAsync(
                enhancedTask,
                availableTools,
                llmProvider,
                options,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "{Language} agent completed in {Steps} steps, success={Success}, tokens={Tokens}",
                _config.Language.Name,
                result.Steps.Count,
                result.Success,
                result.TotalTokensUsed);

            return new AgentOutput(
                result.FinalAnswer,
                result.TotalTokensUsed,
                ToolCalls: result.Steps
                    .Where(s => s.Action != "finish")
                    .Select(s => new ToolCall(s.Action, s.ActionInput, s.Observation))
                    .ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "{Language} agent execution failed", _config.Language.Name);
            throw AgentException.ExecutionFailed(
                $"{_config.Language.Name} coding agent failed: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Builds the unified capabilities list.
    /// </summary>
    private IReadOnlyList<string> BuildCapabilities()
    {
        var capabilities = new List<string>
        {
            // Primary unified capability
            $"software-development-{_config.Language.Id}",
        };

        // Add explicit capabilities from config
        capabilities.AddRange(_config.Capabilities);

        // Ensure "coding" is always present as fallback
        if (!capabilities.Contains("coding", StringComparer.OrdinalIgnoreCase))
        {
            capabilities.Add("coding");
        }

        return capabilities.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Gets all tools available to this agent.
    /// </summary>
    private IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        var tools = new List<ToolDefinition>();

        // Always include core file tools
        var fileTools = new[] { "file.read", "file.write", "file.modify", "file.list", "file.exists" };
        foreach (var toolId in fileTools)
        {
            var tool = _toolRegistry.GetTool(toolId);
            if (tool is not null)
            {
                tools.Add(tool);
            }
        }

        // Add language-specific tools from config
        foreach (var toolDef in _config.Tools.Values)
        {
            var tool = _toolRegistry.GetTool(toolDef.Id);
            if (tool is not null)
            {
                tools.Add(tool);
            }
            else
            {
                _logger.LogWarning(
                    "Tool {ToolId} from {Language} config not found in registry",
                    toolDef.Id,
                    _config.Language.Name);
            }
        }

        return tools;
    }

    /// <summary>
    /// Builds an enhanced task description with context.
    /// </summary>
    private string BuildEnhancedTask(AgentContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {_config.Language.Name} Development Task");
        sb.AppendLine();
        sb.AppendLine("## Objective");
        sb.AppendLine(context.Prompt);
        sb.AppendLine();

        // Add workflow instructions
        if (!string.IsNullOrEmpty(_config.Prompt.Workflow))
        {
            sb.AppendLine("## Workflow");
            sb.AppendLine(_config.Prompt.Workflow);
            sb.AppendLine();
        }

        // Add available tools
        sb.AppendLine("## Available Tools");
        foreach (var toolDef in _config.Tools.Values)
        {
            sb.AppendLine($"- `{toolDef.Id}`: {toolDef.Description}");
        }

        sb.AppendLine("- `file.read`: Read file contents");
        sb.AppendLine("- `file.write`: Write/create a file");
        sb.AppendLine("- `file.modify`: Make targeted edits to a file");
        sb.AppendLine("- `file.list`: List directory contents");
        sb.AppendLine("- `file.exists`: Check if file exists");
        sb.AppendLine();

        // Add workspace info
        if (!string.IsNullOrEmpty(context.WorkspacePath))
        {
            sb.AppendLine("## Workspace");
            sb.AppendLine($"Working directory: {context.WorkspacePath}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds additional context for the ReAct system prompt.
    /// </summary>
    private string BuildAdditionalContext(AgentContext context)
    {
        var sb = new StringBuilder();

        // Add best practices
        if (!string.IsNullOrEmpty(_config.Prompt.BestPractices))
        {
            sb.AppendLine($"## {_config.Language.Name} Best Practices");
            sb.AppendLine(_config.Prompt.BestPractices);
            sb.AppendLine();
        }

        // Add syntax reminders
        if (!string.IsNullOrEmpty(_config.Prompt.SyntaxReminders))
        {
            sb.AppendLine($"## {_config.Language.Name} Syntax Reminders");
            sb.AppendLine(_config.Prompt.SyntaxReminders);
            sb.AppendLine();
        }

        // Add project structure guidance
        if (!string.IsNullOrEmpty(_config.Prompt.ProjectStructure))
        {
            sb.AppendLine("## Project Structure");
            sb.AppendLine(_config.Prompt.ProjectStructure);
            sb.AppendLine();
        }

        // Add RAG context if available
        if (!string.IsNullOrEmpty(context.RagContext))
        {
            sb.AppendLine("## Relevant Code Context (from RAG)");
            sb.AppendLine(context.RagContext);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
