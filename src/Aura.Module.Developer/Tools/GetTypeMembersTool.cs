// <copyright file="GetTypeMembersTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool that gets all members of a type using the code graph.
/// </summary>
public class GetTypeMembersTool : TypedToolBase<GetTypeMembersInput, GetTypeMembersOutput>
{
    private readonly ICodeGraphService _graphService;
    private readonly ILogger<GetTypeMembersTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTypeMembersTool"/> class.
    /// </summary>
    public GetTypeMembersTool(ICodeGraphService graphService, ILogger<GetTypeMembersTool> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "graph.get_type_members";

    /// <inheritdoc/>
    public override string Name => "get_type_members";

    /// <inheritdoc/>
    public override string Description => "Gets all members (methods, properties, fields) of a type using the code graph";

    /// <inheritdoc/>
    public override async Task<ToolResult<GetTypeMembersOutput>> ExecuteAsync(
        GetTypeMembersInput input,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Getting members of {TypeName}", input.TypeName);

            var members = await _graphService.GetTypeMembersAsync(
                input.TypeName,
                input.WorkspacePath,
                ct);

            return ToolResult<GetTypeMembersOutput>.Ok(new GetTypeMembersOutput
            {
                TypeName = input.TypeName,
                Members = members.Select(n => new MemberInfo
                {
                    Name = n.Name,
                    FullName = n.FullName ?? n.Name,
                    MemberType = n.NodeType.ToString(),
                    Signature = n.Signature,
                    Modifiers = n.Modifiers,
                    FilePath = n.FilePath,
                    LineNumber = n.LineNumber,
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get type members");
            return ToolResult<GetTypeMembersOutput>.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Input for the get_type_members tool.
/// </summary>
public record GetTypeMembersInput
{
    /// <summary>Gets the type name (simple or fully qualified).</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the optional workspace path for isolation.</summary>
    public string? WorkspacePath { get; init; }
}

/// <summary>
/// Output from the get_type_members tool.
/// </summary>
public record GetTypeMembersOutput
{
    /// <summary>Gets the type that was queried.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the list of members.</summary>
    public List<MemberInfo> Members { get; init; } = [];
}

/// <summary>
/// Information about a member in the code graph.
/// </summary>
public record MemberInfo
{
    /// <summary>Gets the member name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fully qualified member name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the member type (Method, Property, Field, etc.).</summary>
    public required string MemberType { get; init; }

    /// <summary>Gets the member signature.</summary>
    public string? Signature { get; init; }

    /// <summary>Gets the modifiers (public, static, etc.).</summary>
    public string? Modifiers { get; init; }

    /// <summary>Gets the file path where the member is defined.</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets the line number where the member starts.</summary>
    public int? LineNumber { get; init; }
}
