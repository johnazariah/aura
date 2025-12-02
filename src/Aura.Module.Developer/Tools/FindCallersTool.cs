// <copyright file="FindCallersTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool that finds all methods calling a given method using the code graph.
/// </summary>
public class FindCallersTool : TypedToolBase<FindCallersInput, FindCallersOutput>
{
    private readonly ICodeGraphService _graphService;
    private readonly ILogger<FindCallersTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindCallersTool"/> class.
    /// </summary>
    public FindCallersTool(ICodeGraphService graphService, ILogger<FindCallersTool> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "graph.find_callers";

    /// <inheritdoc/>
    public override string Name => "find_callers";

    /// <inheritdoc/>
    public override string Description => "Finds all methods that call a given method using the code graph";

    /// <inheritdoc/>
    public override async Task<ToolResult<FindCallersOutput>> ExecuteAsync(
        FindCallersInput input,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Finding callers of {MethodName} in {ContainingType}", input.MethodName, input.ContainingTypeName);

            var callers = await _graphService.FindCallersAsync(
                input.MethodName,
                input.ContainingTypeName,
                input.WorkspacePath,
                ct);

            return ToolResult<FindCallersOutput>.Ok(new FindCallersOutput
            {
                MethodName = input.MethodName,
                ContainingTypeName = input.ContainingTypeName,
                Callers = callers.Select(n => new MethodInfo
                {
                    Name = n.Name,
                    FullName = n.FullName ?? n.Name,
                    Signature = n.Signature,
                    FilePath = n.FilePath,
                    LineNumber = n.LineNumber,
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find callers");
            return ToolResult<FindCallersOutput>.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Input for the find_callers tool.
/// </summary>
public record FindCallersInput
{
    /// <summary>Gets the method name to search for.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the optional containing type name to narrow the search.</summary>
    public string? ContainingTypeName { get; init; }

    /// <summary>Gets the optional workspace path for isolation.</summary>
    public string? WorkspacePath { get; init; }
}

/// <summary>
/// Output from the find_callers tool.
/// </summary>
public record FindCallersOutput
{
    /// <summary>Gets the method that was searched.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the containing type if specified.</summary>
    public string? ContainingTypeName { get; init; }

    /// <summary>Gets the list of calling methods.</summary>
    public List<MethodInfo> Callers { get; init; } = [];
}

/// <summary>
/// Information about a method in the code graph.
/// </summary>
public record MethodInfo
{
    /// <summary>Gets the method name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fully qualified method name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the method signature.</summary>
    public string? Signature { get; init; }

    /// <summary>Gets the file path where the method is defined.</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets the line number where the method starts.</summary>
    public int? LineNumber { get; init; }
}
