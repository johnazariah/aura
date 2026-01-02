// <copyright file="IndexCodeGraphTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool that indexes a codebase into the code graph for structural queries.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IndexCodeGraphTool"/> class.
/// </remarks>
public class IndexCodeGraphTool(ICodeGraphIndexer indexer, ILogger<IndexCodeGraphTool> logger) : TypedToolBase<IndexCodeGraphInput, IndexCodeGraphOutput>
{
    private readonly ICodeGraphIndexer _indexer = indexer;
    private readonly ILogger<IndexCodeGraphTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "graph.index_code";

    /// <inheritdoc/>
    public override string Name => "index_code_graph";

    /// <inheritdoc/>
    public override string Description => "Indexes a solution or project into the code graph for structural queries (find implementations, callers, etc.)";

    /// <inheritdoc/>
    public override async Task<ToolResult<IndexCodeGraphOutput>> ExecuteAsync(
        IndexCodeGraphInput input,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Indexing code graph for {Path}", input.SolutionPath);

            CodeGraphIndexResult result;
            if (input.Reindex)
            {
                result = await _indexer.ReindexAsync(input.SolutionPath, input.WorkspacePath, ct);
            }
            else
            {
                result = await _indexer.IndexAsync(input.SolutionPath, input.WorkspacePath, ct);
            }

            if (!result.Success)
            {
                return ToolResult<IndexCodeGraphOutput>.Fail($"Indexing failed: {result.ErrorMessage}");
            }

            return ToolResult<IndexCodeGraphOutput>.Ok(new IndexCodeGraphOutput
            {
                NodesCreated = result.NodesCreated,
                EdgesCreated = result.EdgesCreated,
                ProjectsIndexed = result.ProjectsIndexed,
                FilesIndexed = result.FilesIndexed,
                TypesIndexed = result.TypesIndexed,
                DurationMs = (int)result.Duration.TotalMilliseconds,
                Warnings = result.Warnings,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index code graph");
            return ToolResult<IndexCodeGraphOutput>.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Input for the index_code_graph tool.
/// </summary>
public record IndexCodeGraphInput
{
    /// <summary>Gets the path to the solution or project to index.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Gets the workspace path for node isolation.</summary>
    public required string WorkspacePath { get; init; }

    /// <summary>Gets whether to clear existing graph and re-index.</summary>
    public bool Reindex { get; init; } = false;
}

/// <summary>
/// Output from the index_code_graph tool.
/// </summary>
public record IndexCodeGraphOutput
{
    /// <summary>Gets the number of nodes created.</summary>
    public int NodesCreated { get; init; }

    /// <summary>Gets the number of edges created.</summary>
    public int EdgesCreated { get; init; }

    /// <summary>Gets the number of projects indexed.</summary>
    public int ProjectsIndexed { get; init; }

    /// <summary>Gets the number of files indexed.</summary>
    public int FilesIndexed { get; init; }

    /// <summary>Gets the number of types indexed.</summary>
    public int TypesIndexed { get; init; }

    /// <summary>Gets the indexing duration in milliseconds.</summary>
    public int DurationMs { get; init; }

    /// <summary>Gets any warnings from indexing.</summary>
    public List<string> Warnings { get; init; } = [];
}
