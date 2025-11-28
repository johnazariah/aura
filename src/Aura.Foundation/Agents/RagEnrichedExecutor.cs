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
    /// <param name="ragOptions">Custom RAG query options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent output.</returns>
    Task<AgentOutput> ExecuteAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
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
    private readonly RagExecutionOptions _options;
    private readonly ILogger<RagEnrichedExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagEnrichedExecutor"/> class.
    /// </summary>
    public RagEnrichedExecutor(
        IAgentRegistry agentRegistry,
        IRagService ragService,
        IOptions<RagExecutionOptions> options,
        ILogger<RagEnrichedExecutor> logger)
    {
        _agentRegistry = agentRegistry;
        _ragService = ragService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        RagQueryOptions? ragOptions = null,
        CancellationToken cancellationToken = default)
    {
        var agent = _agentRegistry.GetAgent(agentId)
            ?? throw new AgentException(AgentErrorCode.NotFound, "Agent '" + agentId + "' not found");

        var shouldUseRag = useRag ?? _options.EnabledByDefault;
        
        AgentContext context;
        
        if (shouldUseRag)
        {
            context = await BuildRagEnrichedContextAsync(
                prompt, 
                workspacePath, 
                ragOptions, 
                cancellationToken);
        }
        else
        {
            context = new AgentContext(prompt, WorkspacePath: workspacePath);
        }

        _logger.LogInformation(
            "Executing agent {AgentId} with RAG={UseRag}, RagResults={RagCount}",
            agentId,
            shouldUseRag,
            context.RagResults?.Count ?? 0);

        return await agent.ExecuteAsync(context, cancellationToken);
    }

    private async Task<AgentContext> BuildRagEnrichedContextAsync(
        string prompt,
        string? workspacePath,
        RagQueryOptions? customOptions,
        CancellationToken cancellationToken)
    {
        var options = customOptions ?? new RagQueryOptions
        {
            TopK = _options.DefaultTopK,
            MinScore = _options.MinRelevanceScore,
            SourcePathPrefix = workspacePath,
        };

        try
        {
            var results = await _ragService.QueryAsync(prompt, options, cancellationToken);

            if (results.Count == 0)
            {
                _logger.LogDebug("No RAG results found for prompt");
                return new AgentContext(prompt, WorkspacePath: workspacePath);
            }

            var ragContext = FormatRagContext(results);

            _logger.LogDebug(
                "Found {Count} RAG results with scores {Scores}",
                results.Count,
                string.Join(", ", results.Select(r => r.Score.ToString("F2"))));

            return new AgentContext(prompt, WorkspacePath: workspacePath)
            {
                RagContext = ragContext,
                RagResults = results,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG query failed, proceeding without context");
            return new AgentContext(prompt, WorkspacePath: workspacePath);
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
