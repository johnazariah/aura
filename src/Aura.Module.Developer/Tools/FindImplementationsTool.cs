// <copyright file="FindImplementationsTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool that finds all types implementing a given interface using the code graph.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FindImplementationsTool"/> class.
/// </remarks>
public class FindImplementationsTool(ICodeGraphService graphService, ILogger<FindImplementationsTool> logger) : TypedToolBase<FindImplementationsInput, FindImplementationsOutput>
{
    private readonly ICodeGraphService _graphService = graphService;
    private readonly ILogger<FindImplementationsTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "graph.find_implementations";

    /// <inheritdoc/>
    public override string Name => "find_implementations";

    /// <inheritdoc/>
    public override string Description => "Finds all classes that implement a given interface using the code graph";

    /// <inheritdoc/>
    public override async Task<ToolResult<FindImplementationsOutput>> ExecuteAsync(
        FindImplementationsInput input,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Finding implementations of {InterfaceName}", input.InterfaceName);

            var implementations = await _graphService.FindImplementationsAsync(
                input.InterfaceName,
                input.WorkspacePath,
                ct);

            return ToolResult<FindImplementationsOutput>.Ok(new FindImplementationsOutput
            {
                InterfaceName = input.InterfaceName,
                Implementations = implementations.Select(n => new GraphTypeInfo
                {
                    Name = n.Name,
                    FullName = n.FullName ?? n.Name,
                    FilePath = n.FilePath,
                    LineNumber = n.LineNumber,
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find implementations");
            return ToolResult<FindImplementationsOutput>.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Input for the find_implementations tool.
/// </summary>
public record FindImplementationsInput
{
    /// <summary>Gets the interface name to search for (simple or fully qualified).</summary>
    public required string InterfaceName { get; init; }

    /// <summary>Gets the optional workspace path for isolation.</summary>
    public string? WorkspacePath { get; init; }
}

/// <summary>
/// Output from the find_implementations tool.
/// </summary>
public record FindImplementationsOutput
{
    /// <summary>Gets the interface that was searched.</summary>
    public required string InterfaceName { get; init; }

    /// <summary>Gets the list of implementing types.</summary>
    public List<GraphTypeInfo> Implementations { get; init; } = [];
}

/// <summary>
/// Information about a type in the code graph.
/// </summary>
public record GraphTypeInfo
{
    /// <summary>Gets the simple type name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fully qualified type name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the file path where the type is defined.</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets the line number where the type starts.</summary>
    public int? LineNumber { get; init; }
}
