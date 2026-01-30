// <copyright file="RagEnrichedExecutor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

/// <summary>
/// Options for RAG-enriched agent execution.
/// </summary>
public sealed class RagExecutionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:RagExecution";

    /// <summary>
    /// Gets or sets whether RAG enrichment is enabled by default.
    /// </summary>
    public bool EnabledByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Code Graph enrichment is enabled by default.
    /// </summary>
    public bool CodeGraphEnabledByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets the default number of RAG results to include.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum relevance score for RAG results.
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.3;
}

/// <summary>
/// Executes agents with automatic RAG context enrichment.
/// </summary>
public interface IRagEnrichedExecutor
{
    /// <summary>
    /// Executes an agent with RAG-enriched context.
    /// </summary>
    /// <param name="agentId">The agent ID to execute.</param>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="workspacePath">Optional workspace path.</param>
    /// <param name="useRag">Whether to use RAG enrichment (null = use default).</param>
    /// <param name="useCodeGraph">Whether to use Code Graph enrichment (null = use default).</param>
    /// <param name="ragOptions">Custom RAG query options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent output.</returns>
    Task<AgentOutput> ExecuteAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        bool? useCodeGraph = null,
        RagQueryOptions? ragOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an agent with tools (agentic mode using ReAct).
    /// The agent can call read-only tools to explore the codebase.
    /// </summary>
    /// <param name="agentId">The agent ID to execute.</param>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="workspacePath">Optional workspace path.</param>
    /// <param name="useRag">Whether to use RAG enrichment for initial context.</param>
    /// <param name="useCodeGraph">Whether to use Code Graph enrichment for initial context.</param>
    /// <param name="maxSteps">Maximum number of tool-use iterations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent output including tool use history.</returns>
    Task<AgenticOutput> ExecuteAgenticAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        bool? useCodeGraph = null,
        int maxSteps = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Output from agentic (tool-enabled) execution.
/// </summary>
public sealed record AgenticOutput(
    string Content,
    int TokensUsed,
    IReadOnlyList<ToolUseStep> ToolSteps);

/// <summary>
/// A single tool use step in agentic execution.
/// </summary>
public sealed record ToolUseStep(
    string ToolId,
    string Input,
    string Output,
    bool Success);

/// <summary>
/// Executes agents with automatic RAG context enrichment.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RagEnrichedExecutor"/> class.
/// </remarks>
public sealed class RagEnrichedExecutor(
    IAgentRegistry agentRegistry,
    IRagService ragService,
    ICodeGraphEnricher codeGraphEnricher,
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry,
    ILlmProviderRegistry llmProviderRegistry,
    IOptions<RagExecutionOptions> options,
    ILogger<RagEnrichedExecutor> logger) : IRagEnrichedExecutor
{
    private readonly IAgentRegistry _agentRegistry = agentRegistry;
    private readonly IRagService _ragService = ragService;
    private readonly ICodeGraphEnricher _codeGraphEnricher = codeGraphEnricher;
    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry = llmProviderRegistry;
    private readonly RagExecutionOptions _options = options.Value;
    private readonly ILogger<RagEnrichedExecutor> _logger = logger;

    /// <summary>
    /// Safe read-only tools allowed for chat agent exploration.
    /// </summary>
    private static readonly HashSet<string> SafeToolIds =
    [
        // File exploration (read-only)
        Tools.BuiltInToolIds.FileRead,
        Tools.BuiltInToolIds.FileList,
        Tools.BuiltInToolIds.FileExists,

        // Search
        Tools.BuiltInToolIds.SearchGrep,

        // Code Graph exploration
        "graph.find_implementations",
        "graph.find_callers",
        "graph.get_type_members",
        "graph.index_code",

        // Roslyn analysis (read-only)
        "roslyn.list_projects",
        "roslyn.list_classes",
        "roslyn.get_class_info",
        "roslyn.find_usages",
        "roslyn.get_project_references",

        // Git status (read-only)
        Tools.BuiltInToolIds.GitStatus,
    ];

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        bool? useCodeGraph = null,
        RagQueryOptions? ragOptions = null,
        CancellationToken cancellationToken = default)
    {
        var agent = _agentRegistry.GetAgent(agentId)
            ?? throw new AgentException(AgentErrorCode.NotFound, "Agent '" + agentId + "' not found");

        var shouldUseRag = useRag ?? _options.EnabledByDefault;
        var shouldUseCodeGraph = useCodeGraph ?? _options.CodeGraphEnabledByDefault;

        var context = new AgentContext(prompt, WorkspacePath: workspacePath);

        // Apply RAG enrichment
        if (shouldUseRag)
        {
            context = await ApplyRagEnrichmentAsync(context, ragOptions, cancellationToken);
        }

        // Apply Code Graph enrichment
        if (shouldUseCodeGraph)
        {
            context = await ApplyCodeGraphEnrichmentAsync(context, cancellationToken);
        }

        _logger.LogInformation(
            "Executing agent {AgentId} with RAG={UseRag} ({RagCount} results), CodeGraph={UseCodeGraph} ({NodeCount} nodes)",
            agentId,
            shouldUseRag,
            context.RagResults?.Count ?? 0,
            shouldUseCodeGraph,
            context.RelevantNodes?.Count ?? 0);

        return await agent.ExecuteAsync(context, cancellationToken);
    }

    private async Task<AgentContext> ApplyRagEnrichmentAsync(
        AgentContext context,
        RagQueryOptions? customOptions,
        CancellationToken cancellationToken)
    {
        // Build options, merging custom options with defaults
        var options = new RagQueryOptions
        {
            TopK = customOptions?.TopK ?? _options.DefaultTopK,
            MinScore = customOptions?.MinScore ?? _options.MinRelevanceScore,
            SourcePathPrefix = customOptions?.SourcePathPrefix ?? context.WorkspacePath,
            ContentTypes = customOptions?.ContentTypes,
        };

        try
        {
            var results = await _ragService.QueryAsync(context.Prompt, options, cancellationToken);

            if (results.Count == 0)
            {
                _logger.LogDebug("No RAG results found for prompt");
                return context;
            }

            var ragContext = FormatRagContext(results);

            _logger.LogDebug(
                "Found {Count} RAG results with scores {Scores}",
                results.Count,
                string.Join(", ", results.Select(r => r.Score.ToString("F2"))));

            return context with
            {
                RagContext = ragContext,
                RagResults = results,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG query failed, proceeding without RAG context");
            return context;
        }
    }

    private async Task<AgentContext> ApplyCodeGraphEnrichmentAsync(
        AgentContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var enrichment = await _codeGraphEnricher.EnrichAsync(
                context.Prompt,
                context.WorkspacePath,
                options: null,
                cancellationToken);

            if (enrichment.Nodes.Count == 0)
            {
                _logger.LogDebug("No Code Graph results found for prompt");
                return context;
            }

            return context with
            {
                CodeGraphContext = enrichment.FormattedContext,
                RelevantNodes = enrichment.Nodes,
                RelevantEdges = enrichment.Edges,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Code Graph enrichment failed, proceeding without Code Graph context");
            return context;
        }
    }

    private static string FormatRagContext(IReadOnlyList<RagResult> results)
    {
        var sb = new StringBuilder();

        foreach (var result in results)
        {
            sb.AppendLine("---");

            if (!string.IsNullOrEmpty(result.SourcePath))
            {
                sb.AppendLine("Source: " + result.SourcePath);
            }

            sb.AppendLine("Relevance: " + result.Score.ToString("P0"));
            sb.AppendLine();
            sb.AppendLine(result.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public async Task<AgenticOutput> ExecuteAgenticAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        bool? useCodeGraph = null,
        int maxSteps = 10,
        CancellationToken cancellationToken = default)
    {
        var agent = _agentRegistry.GetAgent(agentId)
            ?? throw new AgentException(AgentErrorCode.NotFound, "Agent '" + agentId + "' not found");

        var shouldUseRag = useRag ?? _options.EnabledByDefault;
        var shouldUseCodeGraph = useCodeGraph ?? _options.CodeGraphEnabledByDefault;

        // Build initial context with RAG and Code Graph
        var context = new AgentContext(prompt, WorkspacePath: workspacePath);

        if (shouldUseRag)
        {
            context = await ApplyRagEnrichmentAsync(context, null, cancellationToken);
        }

        if (shouldUseCodeGraph)
        {
            context = await ApplyCodeGraphEnrichmentAsync(context, cancellationToken);
        }

        // Get safe read-only tools for exploration
        var safeTools = _toolRegistry.GetAllTools()
            .Where(t => SafeToolIds.Contains(t.ToolId))
            .ToList();

        if (safeTools.Count == 0)
        {
            _logger.LogWarning("No safe tools available for agentic chat, falling back to single-shot execution");
            var fallback = await agent.ExecuteAsync(context, cancellationToken);
            return new AgenticOutput(fallback.Content, fallback.TokensUsed, []);
        }

        // Get LLM provider from agent config or default
        var llmProvider = _llmProviderRegistry.GetDefaultProvider()
            ?? throw new AgentException(AgentErrorCode.ProviderUnavailable, "No LLM provider configured");

        // Build the task with initial context
        var taskBuilder = new StringBuilder();
        taskBuilder.AppendLine("You are an intelligent assistant helping the user explore and understand a codebase.");
        taskBuilder.AppendLine();
        taskBuilder.AppendLine("## User Question");
        taskBuilder.AppendLine(prompt);
        taskBuilder.AppendLine();

        if (!string.IsNullOrEmpty(context.RagContext))
        {
            taskBuilder.AppendLine("## Initial Context (from semantic search)");
            taskBuilder.AppendLine(context.RagContext);
            taskBuilder.AppendLine();
        }

        if (!string.IsNullOrEmpty(context.CodeGraphContext))
        {
            taskBuilder.AppendLine("## Code Graph Context (symbols and relationships)");
            taskBuilder.AppendLine(context.CodeGraphContext);
            taskBuilder.AppendLine();
        }

        taskBuilder.AppendLine("## Instructions");
        taskBuilder.AppendLine("Use the available tools to explore the codebase and find the answer.");
        taskBuilder.AppendLine("Read relevant files, search for patterns, and trace code relationships.");
        taskBuilder.AppendLine("When you have enough information, provide a clear and helpful answer.");

        _logger.LogInformation(
            "Starting agentic chat for agent {AgentId} with {ToolCount} tools, max {MaxSteps} steps",
            agentId,
            safeTools.Count,
            maxSteps);

        // Execute with ReAct loop
        var options = new ReActOptions
        {
            MaxSteps = maxSteps,
            WorkingDirectory = workspacePath,
            RequireConfirmation = false, // All tools are safe/read-only
        };

        var result = await _reactExecutor.ExecuteAsync(
            taskBuilder.ToString(),
            safeTools,
            llmProvider,
            options,
            cancellationToken);

        // Convert ReAct steps to our output format
        var toolSteps = result.Steps
            .Where(s => s.Action != "finish")
            .Select(s => new ToolUseStep(s.Action, s.ActionInput, s.Observation, !s.Observation.StartsWith("Error:")))
            .ToList();

        _logger.LogInformation(
            "Agentic chat completed with {Success} after {StepCount} steps, {TokensUsed} tokens",
            result.Success ? "success" : "failure",
            result.Steps.Count,
            result.TotalTokensUsed);

        return new AgenticOutput(result.FinalAnswer, result.TotalTokensUsed, toolSteps);
    }
}
