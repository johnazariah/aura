// <copyright file="FindUsagesTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the find_usages tool.
/// </summary>
public record FindUsagesInput
{
    /// <summary>Symbol name to find (class, method, or property name)</summary>
    public required string SymbolName { get; init; }

    /// <summary>Optional: containing type for method/property search</summary>
    public string? ContainingType { get; init; }

    /// <summary>Optional: project to search in (searches all if not specified)</summary>
    public string? ProjectName { get; init; }

    /// <summary>Maximum results to return</summary>
    public int MaxResults { get; init; } = 50;
}

/// <summary>
/// Information about a symbol usage/reference.
/// </summary>
public record UsageInfo
{
    /// <summary>File path containing the usage</summary>
    public required string FilePath { get; init; }

    /// <summary>Line number (1-indexed)</summary>
    public int Line { get; init; }

    /// <summary>Column number</summary>
    public int Column { get; init; }

    /// <summary>The line of code containing the usage</summary>
    public string? CodeSnippet { get; init; }

    /// <summary>Containing method or property name</summary>
    public string? ContainingMember { get; init; }

    /// <summary>Containing type name</summary>
    public string? ContainingType { get; init; }

    /// <summary>Whether this is a definition vs reference</summary>
    public bool IsDefinition { get; init; }
}

/// <summary>
/// Output from the find_usages tool.
/// </summary>
public record FindUsagesOutput
{
    /// <summary>Symbol that was searched for</summary>
    public required string SymbolName { get; init; }

    /// <summary>Full symbol name if found</summary>
    public string? FullSymbolName { get; init; }

    /// <summary>List of usages found</summary>
    public required IReadOnlyList<UsageInfo> Usages { get; init; }

    /// <summary>Total number of usages</summary>
    public int TotalUsages => Usages.Count;

    /// <summary>Whether results were truncated</summary>
    public bool WasTruncated { get; init; }
}

/// <summary>
/// Finds all usages/references of a symbol across the codebase.
/// Use to understand how a class, method, or property is used.
/// </summary>
public class FindUsagesTool : TypedToolBase<FindUsagesInput, FindUsagesOutput>
{
    private readonly IRoslynWorkspaceService _workspace;
    private readonly ILogger<FindUsagesTool> _logger;

    public FindUsagesTool(
        IRoslynWorkspaceService workspace,
        ILogger<FindUsagesTool> logger)
    {
        _workspace = workspace;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "roslyn.find_usages";

    /// <inheritdoc/>
    public override string Name => "Find Usages";

    /// <inheritdoc/>
    public override string Description =>
        "Finds all usages/references of a symbol (class, method, property) across the codebase. " +
        "Returns file locations and code snippets. Use to understand how code is used before " +
        "making changes, or to find all places that need updating.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "analysis"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<FindUsagesOutput>> ExecuteAsync(
        FindUsagesInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Finding usages of: {SymbolName}", input.SymbolName);

        try
        {
            var solutionPath = _workspace.FindSolutionFile(Environment.CurrentDirectory);
            if (solutionPath is null)
            {
                return ToolResult<FindUsagesOutput>.Fail("No solution file found in current directory");
            }

            var solution = await _workspace.GetSolutionAsync(solutionPath, ct);

            // Find the symbol
            var symbol = await FindSymbolAsync(solution, input, ct);
            if (symbol is null)
            {
                return ToolResult<FindUsagesOutput>.Fail(
                    $"Symbol '{input.SymbolName}' not found. Use list_classes or get_class_info to verify the name.");
            }

            // Find all references
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
            var usages = new List<UsageInfo>();

            foreach (var reference in references)
            {
                // Add the definition
                foreach (var location in reference.Definition.Locations)
                {
                    if (location.IsInSource)
                    {
                        var usage = await BuildUsageInfoAsync(location, solution, isDefinition: true, ct);
                        if (usage is not null)
                        {
                            usages.Add(usage);
                        }
                    }
                }

                // Add all references
                foreach (var refLocation in reference.Locations)
                {
                    var usage = await BuildUsageInfoAsync(refLocation.Location, solution, isDefinition: false, ct);
                    if (usage is not null)
                    {
                        usages.Add(usage);
                    }

                    if (usages.Count >= input.MaxResults)
                        break;
                }

                if (usages.Count >= input.MaxResults)
                    break;
            }

            var output = new FindUsagesOutput
            {
                SymbolName = input.SymbolName,
                FullSymbolName = symbol.ToDisplayString(),
                Usages = usages.OrderBy(u => u.FilePath).ThenBy(u => u.Line).ToList(),
                WasTruncated = usages.Count >= input.MaxResults,
            };

            _logger.LogInformation("Found {Count} usages of {Symbol}", usages.Count, input.SymbolName);
            return ToolResult<FindUsagesOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find usages of {SymbolName}", input.SymbolName);
            return ToolResult<FindUsagesOutput>.Fail($"Failed to find usages: {ex.Message}");
        }
    }

    private async Task<ISymbol?> FindSymbolAsync(Solution solution, FindUsagesInput input, CancellationToken ct)
    {
        var projects = input.ProjectName is not null
            ? solution.Projects.Where(p => p.Name.Equals(input.ProjectName, StringComparison.OrdinalIgnoreCase))
            : solution.Projects;

        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            // Search for types first
            var allTypes = compilation.GetSymbolsWithName(
                name => name.Equals(input.SymbolName, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.Type);

            var typeSymbol = allTypes.FirstOrDefault();
            if (typeSymbol is not null)
            {
                // If looking for a member within this type
                if (input.ContainingType is null)
                {
                    return typeSymbol;
                }
            }

            // Search for members
            if (input.ContainingType is not null)
            {
                var containingTypes = compilation.GetSymbolsWithName(
                    name => name.Equals(input.ContainingType, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.Type);

                foreach (var containingType in containingTypes.OfType<INamedTypeSymbol>())
                {
                    var member = containingType.GetMembers(input.SymbolName).FirstOrDefault();
                    if (member is not null)
                    {
                        return member;
                    }
                }
            }
            else
            {
                // Search all members with matching name
                var members = compilation.GetSymbolsWithName(
                    name => name.Equals(input.SymbolName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.Member);

                var member = members.FirstOrDefault();
                if (member is not null)
                {
                    return member;
                }
            }
        }

        return null;
    }

    private static async Task<UsageInfo?> BuildUsageInfoAsync(
        Location location,
        Solution solution,
        bool isDefinition,
        CancellationToken ct)
    {
        if (!location.IsInSource) return null;

        var lineSpan = location.GetLineSpan();
        var document = solution.GetDocument(location.SourceTree);

        string? codeSnippet = null;
        string? containingMember = null;
        string? containingType = null;

        if (document is not null)
        {
            var root = await document.GetSyntaxRootAsync(ct);
            var semanticModel = await document.GetSemanticModelAsync(ct);

            if (root is not null)
            {
                var node = root.FindNode(location.SourceSpan);
                var line = location.SourceTree?.GetText(ct).Lines[lineSpan.StartLinePosition.Line];
                codeSnippet = line?.ToString().Trim();

                // Find containing member
                if (semanticModel is not null)
                {
                    var containingSymbol = semanticModel.GetEnclosingSymbol(location.SourceSpan.Start);
                    if (containingSymbol is IMethodSymbol method)
                    {
                        containingMember = method.Name;
                        containingType = method.ContainingType?.Name;
                    }
                    else if (containingSymbol is IPropertySymbol property)
                    {
                        containingMember = property.Name;
                        containingType = property.ContainingType?.Name;
                    }
                    else if (containingSymbol is INamedTypeSymbol type)
                    {
                        containingType = type.Name;
                    }
                }
            }
        }

        return new UsageInfo
        {
            FilePath = lineSpan.Path ?? "",
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            CodeSnippet = codeSnippet,
            ContainingMember = containingMember,
            ContainingType = containingType,
            IsDefinition = isDefinition,
        };
    }
}
