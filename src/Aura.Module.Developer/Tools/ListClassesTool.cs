// <copyright file="ListClassesTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the list_classes tool.
/// </summary>
public record ListClassesInput
{
    /// <summary>Project name to search in</summary>
    public required string ProjectName { get; init; }

    /// <summary>Optional namespace filter (partial match)</summary>
    public string? NamespaceFilter { get; init; }

    /// <summary>Optional name filter (partial match)</summary>
    public string? NameFilter { get; init; }

    /// <summary>Include interfaces</summary>
    public bool IncludeInterfaces { get; init; } = true;

    /// <summary>Include abstract classes</summary>
    public bool IncludeAbstract { get; init; } = true;

    /// <summary>Include records</summary>
    public bool IncludeRecords { get; init; } = true;
}

/// <summary>
/// Information about a discovered type (class, interface, record).
/// </summary>
public record TypeInfo
{
    /// <summary>Simple name of the type</summary>
    public required string Name { get; init; }

    /// <summary>Fully qualified name</summary>
    public required string FullName { get; init; }

    /// <summary>Containing namespace</summary>
    public required string Namespace { get; init; }

    /// <summary>Type kind (class, interface, struct, record)</summary>
    public required string Kind { get; init; }

    /// <summary>File path where the type is defined</summary>
    public required string FilePath { get; init; }

    /// <summary>Line number of declaration</summary>
    public int LineNumber { get; init; }

    /// <summary>Base type name if any</summary>
    public string? BaseType { get; init; }

    /// <summary>Implemented interface names</summary>
    public IReadOnlyList<string> Interfaces { get; init; } = [];

    /// <summary>Whether the type is abstract</summary>
    public bool IsAbstract { get; init; }

    /// <summary>Whether the type is sealed</summary>
    public bool IsSealed { get; init; }

    /// <summary>Whether the type is public</summary>
    public bool IsPublic { get; init; }

    /// <summary>Number of members (methods, properties, fields)</summary>
    public int MemberCount { get; init; }
}

/// <summary>
/// Output from the list_classes tool.
/// </summary>
public record ListClassesOutput
{
    /// <summary>Name of the project searched</summary>
    public required string ProjectName { get; init; }

    /// <summary>List of discovered types</summary>
    public required IReadOnlyList<TypeInfo> Types { get; init; }

    /// <summary>Total number of types found</summary>
    public int TotalTypes => Types.Count;

    /// <summary>Filters that were applied</summary>
    public string? AppliedFilters { get; init; }
}

/// <summary>
/// Lists all classes, interfaces, and records in a project.
/// Use to discover types before examining specific class details.
/// </summary>
public class ListClassesTool : TypedToolBase<ListClassesInput, ListClassesOutput>
{
    private readonly IRoslynWorkspaceService _workspace;
    private readonly ILogger<ListClassesTool> _logger;

    public ListClassesTool(
        IRoslynWorkspaceService workspace,
        ILogger<ListClassesTool> logger)
    {
        _workspace = workspace;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "roslyn.list_classes";

    /// <inheritdoc/>
    public override string Name => "List Classes";

    /// <inheritdoc/>
    public override string Description =>
        "Lists all classes, interfaces, and records in a specified project. " +
        "Supports filtering by namespace or name. Returns type names, namespaces, " +
        "file locations, and basic metadata. Use this to find types before getting detailed info.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "analysis"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<ListClassesOutput>> ExecuteAsync(
        ListClassesInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Listing classes in project: {ProjectName}", input.ProjectName);

        try
        {
            // Find the project in any loaded solution
            Project? project = await FindProjectByNameAsync(input.ProjectName, ct);

            if (project is null)
            {
                return ToolResult<ListClassesOutput>.Fail(
                    $"Project '{input.ProjectName}' not found. Use list_projects first to see available projects.");
            }

            var types = new List<TypeInfo>();
            var compilation = await project.GetCompilationAsync(ct);

            if (compilation is null)
            {
                return ToolResult<ListClassesOutput>.Fail($"Failed to compile project '{input.ProjectName}'");
            }

            foreach (var document in project.Documents)
            {
                if (!document.FilePath?.EndsWith(".cs") ?? true) continue;

                var syntaxRoot = await document.GetSyntaxRootAsync(ct);
                var semanticModel = await document.GetSemanticModelAsync(ct);

                if (syntaxRoot is null || semanticModel is null) continue;

                // Find all type declarations
                var typeDeclarations = syntaxRoot.DescendantNodes()
                    .Where(n => n is TypeDeclarationSyntax);

                foreach (var node in typeDeclarations)
                {
                    if (node is not TypeDeclarationSyntax typeDecl) continue;

                    var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (symbol is null) continue;

                    // Apply filters
                    if (!MatchesFilters(symbol, typeDecl, input)) continue;

                    var typeInfo = BuildTypeInfo(symbol, typeDecl, document.FilePath!);
                    types.Add(typeInfo);
                }
            }

            // Build filter description
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(input.NamespaceFilter)) filters.Add($"namespace contains '{input.NamespaceFilter}'");
            if (!string.IsNullOrEmpty(input.NameFilter)) filters.Add($"name contains '{input.NameFilter}'");
            if (!input.IncludeInterfaces) filters.Add("excluding interfaces");
            if (!input.IncludeAbstract) filters.Add("excluding abstract");
            if (!input.IncludeRecords) filters.Add("excluding records");

            var output = new ListClassesOutput
            {
                ProjectName = project.Name,
                Types = types.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToList(),
                AppliedFilters = filters.Count > 0 ? string.Join(", ", filters) : null,
            };

            _logger.LogInformation("Found {Count} types in project {ProjectName}", types.Count, project.Name);
            return ToolResult<ListClassesOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list classes in project {ProjectName}", input.ProjectName);
            return ToolResult<ListClassesOutput>.Fail($"Failed to list classes: {ex.Message}");
        }
    }

    private async Task<Project?> FindProjectByNameAsync(string projectName, CancellationToken ct)
    {
        // Try to find a solution in current directory
        var solutionPath = _workspace.FindSolutionFile(Environment.CurrentDirectory);
        if (solutionPath is not null)
        {
            var solution = await _workspace.GetSolutionAsync(solutionPath, ct);
            return solution.Projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
        }

        // Try direct project path match
        var projectFiles = _workspace.FindProjectFiles(Environment.CurrentDirectory);
        var matchingFile = projectFiles.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (matchingFile is not null)
        {
            return await _workspace.GetProjectAsync(matchingFile, ct);
        }

        return null;
    }

    private static bool MatchesFilters(INamedTypeSymbol symbol, TypeDeclarationSyntax syntax, ListClassesInput input)
    {
        // Filter by type kind
        if (symbol.TypeKind == TypeKind.Interface && !input.IncludeInterfaces)
            return false;

        if (symbol.IsAbstract && symbol.TypeKind == TypeKind.Class && !input.IncludeAbstract)
            return false;

        if (syntax is RecordDeclarationSyntax && !input.IncludeRecords)
            return false;

        // Filter by namespace
        if (!string.IsNullOrEmpty(input.NamespaceFilter))
        {
            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            if (!ns.Contains(input.NamespaceFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Filter by name
        if (!string.IsNullOrEmpty(input.NameFilter))
        {
            if (!symbol.Name.Contains(input.NameFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static TypeInfo BuildTypeInfo(INamedTypeSymbol symbol, TypeDeclarationSyntax syntax, string filePath)
    {
        var kind = syntax switch
        {
            RecordDeclarationSyntax => "record",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            _ => "class"
        };

        var interfaces = symbol.Interfaces
            .Select(i => i.Name)
            .ToList();

        var location = syntax.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new TypeInfo
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "",
            Kind = kind,
            FilePath = filePath,
            LineNumber = lineSpan.StartLinePosition.Line + 1,
            BaseType = symbol.BaseType?.Name == "Object" ? null : symbol.BaseType?.Name,
            Interfaces = interfaces,
            IsAbstract = symbol.IsAbstract,
            IsSealed = symbol.IsSealed,
            IsPublic = symbol.DeclaredAccessibility == Accessibility.Public,
            MemberCount = symbol.GetMembers().Length,
        };
    }
}
