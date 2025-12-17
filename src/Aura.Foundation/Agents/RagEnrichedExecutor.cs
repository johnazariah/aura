// <copyright file="RagEnrichedExecutor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Rag;
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
}

/// <summary>
/// Executes agents with automatic RAG context enrichment.
/// </summary>
public sealed class RagEnrichedExecutor : IRagEnrichedExecutor
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IRagService _ragService;
    private readonly ICodeGraphEnricher _codeGraphEnricher;
    private readonly RagExecutionOptions _options;
    private readonly ILogger<RagEnrichedExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagEnrichedExecutor"/> class.
    /// </summary>
    public RagEnrichedExecutor(
        IAgentRegistry agentRegistry,
        IRagService ragService,
        ICodeGraphEnricher codeGraphEnricher,
        IOptions<RagExecutionOptions> options,
        ILogger<RagEnrichedExecutor> logger)
    {
        _agentRegistry = agentRegistry;
        _ragService = ragService;
        _codeGraphEnricher = codeGraphEnricher;
        _options = options.Value;
        _logger = logger;
    }

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
        var options = customOptions ?? new RagQueryOptions
        {
            TopK = _options.DefaultTopK,
            MinScore = _options.MinRelevanceScore,
            SourcePathPrefix = context.WorkspacePath,
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
}
