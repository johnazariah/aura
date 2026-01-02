// <copyright file="GetClassInfoTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the get_class_info tool.
/// </summary>
public record GetClassInfoInput
{
    /// <summary>Fully qualified class name (e.g., "Aura.Module.Developer.Services.WorkflowService")</summary>
    public required string ClassName { get; init; }

    /// <summary>Project name (optional if class name is unique)</summary>
    public string? ProjectName { get; init; }

    /// <summary>Include method implementations (source code)</summary>
    public bool IncludeImplementations { get; init; }

    /// <summary>Include documentation comments</summary>
    public bool IncludeDocumentation { get; init; } = true;
}

/// <summary>
/// Detailed information about a method.
/// </summary>
public record MethodDetail
{
    /// <summary>Method name</summary>
    public required string Name { get; init; }

    /// <summary>Full signature including parameters and return type</summary>
    public required string Signature { get; init; }

    /// <summary>Return type</summary>
    public required string ReturnType { get; init; }

    /// <summary>Parameters</summary>
    public required IReadOnlyList<ParameterDetail> Parameters { get; init; }

    /// <summary>Access modifier (public, private, etc.)</summary>
    public required string Accessibility { get; init; }

    /// <summary>Whether the method is async</summary>
    public bool IsAsync { get; init; }

    /// <summary>Whether the method is virtual/override/abstract</summary>
    public string? VirtualModifier { get; init; }

    /// <summary>Documentation summary</summary>
    public string? Documentation { get; init; }

    /// <summary>Implementation source code (if requested)</summary>
    public string? Implementation { get; init; }

    /// <summary>Line number in source file</summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Parameter information.
/// </summary>
public record ParameterDetail
{
    /// <summary>Parameter name</summary>
    public required string Name { get; init; }

    /// <summary>Parameter type</summary>
    public required string Type { get; init; }

    /// <summary>Whether the parameter has a default value</summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>Default value if any</summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Detailed information about a property.
/// </summary>
public record PropertyDetail
{
    /// <summary>Property name</summary>
    public required string Name { get; init; }

    /// <summary>Property type</summary>
    public required string Type { get; init; }

    /// <summary>Access modifier</summary>
    public required string Accessibility { get; init; }

    /// <summary>Has getter</summary>
    public bool HasGetter { get; init; }

    /// <summary>Has setter</summary>
    public bool HasSetter { get; init; }

    /// <summary>Is init-only</summary>
    public bool IsInitOnly { get; init; }

    /// <summary>Is required</summary>
    public bool IsRequired { get; init; }

    /// <summary>Documentation summary</summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// Output from the get_class_info tool.
/// </summary>
public record GetClassInfoOutput
{
    /// <summary>Full class name</summary>
    public required string ClassName { get; init; }

    /// <summary>Namespace</summary>
    public required string Namespace { get; init; }

    /// <summary>Type kind (class, interface, record)</summary>
    public required string Kind { get; init; }

    /// <summary>Base type if any</summary>
    public string? BaseType { get; init; }

    /// <summary>Implemented interfaces</summary>
    public required IReadOnlyList<string> Interfaces { get; init; }

    /// <summary>File path where defined</summary>
    public required string FilePath { get; init; }

    /// <summary>Class documentation summary</summary>
    public string? Documentation { get; init; }

    /// <summary>Constructor parameters (for DI understanding)</summary>
    public IReadOnlyList<ParameterDetail> ConstructorParameters { get; init; } = [];

    /// <summary>Public methods</summary>
    public required IReadOnlyList<MethodDetail> Methods { get; init; }

    /// <summary>Public properties</summary>
    public required IReadOnlyList<PropertyDetail> Properties { get; init; }

    /// <summary>Is abstract</summary>
    public bool IsAbstract { get; init; }

    /// <summary>Is sealed</summary>
    public bool IsSealed { get; init; }
}

/// <summary>
/// Gets detailed information about a specific class, interface, or record.
/// Includes methods, properties, constructors, and optionally source code.
/// </summary>
public class GetClassInfoTool(
    IRoslynWorkspaceService workspace,
    ILogger<GetClassInfoTool> logger) : TypedToolBase<GetClassInfoInput, GetClassInfoOutput>
{
    private readonly IRoslynWorkspaceService _workspace = workspace;
    private readonly ILogger<GetClassInfoTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "roslyn.get_class_info";

    /// <inheritdoc/>
    public override string Name => "Get Class Info";

    /// <inheritdoc/>
    public override string Description =>
        "Gets detailed information about a specific class, interface, or record. " +
        "Returns methods (with signatures and optionally implementations), properties, " +
        "constructors, base types, and documentation. Essential for understanding existing code " +
        "before making modifications.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "analysis"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<GetClassInfoOutput>> ExecuteAsync(
        GetClassInfoInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting class info: {ClassName}", input.ClassName);

        try
        {
            var (symbol, syntax, filePath) = await FindClassAsync(input.ClassName, input.ProjectName, ct);

            if (symbol is null || syntax is null)
            {
                return ToolResult<GetClassInfoOutput>.Fail(
                    $"Class '{input.ClassName}' not found. Use list_classes first to find the correct name.");
            }

            var methods = BuildMethodDetails(symbol, syntax, input.IncludeImplementations, input.IncludeDocumentation);
            var properties = BuildPropertyDetails(symbol, input.IncludeDocumentation);
            var constructorParams = GetConstructorParameters(symbol);

            var output = new GetClassInfoOutput
            {
                ClassName = symbol.Name,
                Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "",
                Kind = GetTypeKind(syntax),
                BaseType = symbol.BaseType?.Name == "Object" ? null : symbol.BaseType?.ToDisplayString(),
                Interfaces = symbol.Interfaces.Select(i => i.ToDisplayString()).ToList(),
                FilePath = filePath ?? "",
                Documentation = input.IncludeDocumentation ? GetDocumentation(symbol) : null,
                ConstructorParameters = constructorParams,
                Methods = methods,
                Properties = properties,
                IsAbstract = symbol.IsAbstract,
                IsSealed = symbol.IsSealed,
            };

            _logger.LogInformation("Found class with {MethodCount} methods and {PropertyCount} properties",
                methods.Count, properties.Count);

            return ToolResult<GetClassInfoOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get class info for {ClassName}", input.ClassName);
            return ToolResult<GetClassInfoOutput>.Fail($"Failed to get class info: {ex.Message}");
        }
    }

    private async Task<(INamedTypeSymbol? Symbol, TypeDeclarationSyntax? Syntax, string? FilePath)> FindClassAsync(
        string className,
        string? projectName,
        CancellationToken ct)
    {
        var solutionPath = _workspace.FindSolutionFile(Environment.CurrentDirectory);
        if (solutionPath is null)
        {
            return (null, null, null);
        }

        var solution = await _workspace.GetSolutionAsync(solutionPath, ct);

        // Search in specific project or all projects
        var projects = projectName is not null
            ? solution.Projects.Where(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            : solution.Projects;

        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                if (!document.FilePath?.EndsWith(".cs") ?? true) continue;

                var syntaxRoot = await document.GetSyntaxRootAsync(ct);
                var semanticModel = await document.GetSemanticModelAsync(ct);

                if (syntaxRoot is null || semanticModel is null) continue;

                var typeDecls = syntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDecls)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (symbol is null) continue;

                    // Match by full name or simple name
                    var fullName = symbol.ToDisplayString();
                    if (fullName.Equals(className, StringComparison.OrdinalIgnoreCase) ||
                        symbol.Name.Equals(className, StringComparison.OrdinalIgnoreCase))
                    {
                        return (symbol, typeDecl, document.FilePath);
                    }
                }
            }
        }

        return (null, null, null);
    }

    private static string GetTypeKind(TypeDeclarationSyntax syntax) => syntax switch
    {
        RecordDeclarationSyntax => "record",
        InterfaceDeclarationSyntax => "interface",
        StructDeclarationSyntax => "struct",
        _ => "class"
    };

    private static List<MethodDetail> BuildMethodDetails(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax syntax,
        bool includeImplementations,
        bool includeDocumentation)
    {
        var methods = new List<MethodDetail>();

        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            // Skip special methods like property accessors, constructors handled separately
            if (member.MethodKind != MethodKind.Ordinary) continue;
            if (member.DeclaredAccessibility == Accessibility.Private) continue;

            var methodSyntax = syntax.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == member.Name);

            var parameters = member.Parameters
                .Select(p => new ParameterDetail
                {
                    Name = p.Name,
                    Type = p.Type.ToDisplayString(),
                    HasDefaultValue = p.HasExplicitDefaultValue,
                    DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null,
                })
                .ToList();

            string? virtualMod = null;
            if (member.IsVirtual) virtualMod = "virtual";
            else if (member.IsOverride) virtualMod = "override";
            else if (member.IsAbstract) virtualMod = "abstract";

            var method = new MethodDetail
            {
                Name = member.Name,
                Signature = member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                ReturnType = member.ReturnType.ToDisplayString(),
                Parameters = parameters,
                Accessibility = member.DeclaredAccessibility.ToString().ToLowerInvariant(),
                IsAsync = member.IsAsync,
                VirtualModifier = virtualMod,
                Documentation = includeDocumentation ? GetDocumentation(member) : null,
                Implementation = includeImplementations && methodSyntax is not null
                    ? methodSyntax.ToFullString()
                    : null,
                LineNumber = methodSyntax?.GetLocation().GetLineSpan().StartLinePosition.Line + 1 ?? 0,
            };

            methods.Add(method);
        }

        return methods.OrderBy(m => m.Name).ToList();
    }

    private static List<PropertyDetail> BuildPropertyDetails(INamedTypeSymbol symbol, bool includeDocumentation)
    {
        var properties = new List<PropertyDetail>();

        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility == Accessibility.Private) continue;

            properties.Add(new PropertyDetail
            {
                Name = member.Name,
                Type = member.Type.ToDisplayString(),
                Accessibility = member.DeclaredAccessibility.ToString().ToLowerInvariant(),
                HasGetter = member.GetMethod is not null,
                HasSetter = member.SetMethod is not null,
                IsInitOnly = member.SetMethod?.IsInitOnly ?? false,
                IsRequired = member.IsRequired,
                Documentation = includeDocumentation ? GetDocumentation(member) : null,
            });
        }

        return properties.OrderBy(p => p.Name).ToList();
    }

    private static List<ParameterDetail> GetConstructorParameters(INamedTypeSymbol symbol)
    {
        // Find the primary constructor or the main constructor with most parameters
        var constructor = symbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (constructor is null) return [];

        return constructor.Parameters
            .Select(p => new ParameterDetail
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(),
                HasDefaultValue = p.HasExplicitDefaultValue,
                DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null,
            })
            .ToList();
    }

    private static string? GetDocumentation(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(xml)) return null;

        // Extract summary content - simplified parsing
        var summaryStart = xml.IndexOf("<summary>");
        var summaryEnd = xml.IndexOf("</summary>");

        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var summary = xml.Substring(summaryStart + 9, summaryEnd - summaryStart - 9);
            return summary.Trim().Replace("\n", " ").Replace("  ", " ");
        }

        return null;
    }
}
