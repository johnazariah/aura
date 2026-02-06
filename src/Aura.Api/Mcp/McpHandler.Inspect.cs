using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    /// <summary>
    /// aura_inspect - Examine code structure.
    /// Routes to: type_members, list_types.
    /// Auto-detects language from solutionPath (C#) vs projectPath (TypeScript/Python).
    /// </summary>
    private async Task<object> InspectAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args.GetRequiredString("operation");
        var language = DetectLanguageFromArgs(args);

        return (operation, language) switch
        {
            ("type_members", "typescript") => await TypeScriptInspectTypeAsync(args, ct),
            ("list_types", "typescript") => await TypeScriptListTypesAsync(args, ct),
            ("type_members", _) => await GetTypeMembersAsync(args, ct),
            ("list_types", _) => await ListClassesFromInspect(args, ct),
            _ => throw new ArgumentException($"Unknown inspect operation: {operation}")
        };
    }

    private async Task<object> ListClassesFromInspect(JsonElement? args, CancellationToken ct)
    {
        // Adapts from new schema to existing ListClassesAsync
        return await ListClassesAsync(args, ct);
    }

    private async Task<object> GetTypeMembersAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args.GetStringOrDefault("typeName");
        var worktreeInfo = DetectWorktreeFromArgs(args);
        // Try code graph first
        var results = await _graphService.GetTypeMembersAsync(typeName, cancellationToken: ct);
        if (results.Count > 0)
        {
            return results.Select(n => new { name = n.Name, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
        }

        // Fallback to Roslyn if code graph returns empty and solutionPath is provided
        string? solutionPath = null;
        if (args.HasValue && args.Value.TryGetProperty("solutionPath", out var solEl))
        {
            solutionPath = solEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return Array.Empty<object>();
        }

        return await GetTypeMembersViaRoslynAsync(solutionPath, typeName, worktreeInfo, ct);
    }

    private async Task<object> GetTypeMembersViaRoslynAsync(string solutionPath, string typeName, DetectedWorktree? worktreeInfo, CancellationToken ct)
    {
        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            INamedTypeSymbol? typeSymbol = null;
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                    continue;
                // Try exact match first, then partial match
                typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol is null)
                {
                    // Try finding by simple name
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = await tree.GetRootAsync(ct);
                        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Identifier.Text == typeName || t.Identifier.Text.EndsWith(typeName));
                        foreach (var typeDecl in typeDeclarations)
                        {
                            if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol found)
                            {
                                typeSymbol = found;
                                break;
                            }
                        }

                        if (typeSymbol != null)
                            break;
                    }
                }

                if (typeSymbol != null)
                    break;
            }

            if (typeSymbol is null)
            {
                return Array.Empty<object>();
            }

            // Get all members
            var members = typeSymbol.GetMembers().Where(m => !m.IsImplicitlyDeclared && m.CanBeReferencedByName).Select(m =>
            {
                var location = m.Locations.FirstOrDefault();
                var filePath = location?.SourceTree?.FilePath ?? "";
                return new
                {
                    name = m.Name,
                    kind = m.Kind.ToString(),
                    signature = GetMemberSignature(m),
                    filePath = TranslatePathIfWorktree(filePath, worktreeInfo),
                    line = location?.GetLineSpan().StartLinePosition.Line + 1 ?? 0
                };
            }).ToList();
            return members;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Roslyn fallback for type_members failed for {TypeName}", typeName);
            return Array.Empty<object>();
        }
    }

    private static string GetMemberSignature(ISymbol member)
    {
        return member switch
        {
            IMethodSymbol method => $"{method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"))})",
            IPropertySymbol prop => $"{prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {prop.Name}",
            IFieldSymbol field => $"{field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {field.Name}",
            IEventSymbol evt => $"event {evt.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {evt.Name}",
            _ => member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }

    private async Task<object> ListClassesAsync(JsonElement? args, CancellationToken ct)
    {
        string solutionPath = "";
        string projectName = "";
        string? namespaceFilter = null;
        string? nameFilter = null;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("solutionPath", out var solEl))
                solutionPath = solEl.GetString() ?? "";
            if (args.Value.TryGetProperty("projectName", out var projEl))
                projectName = projEl.GetString() ?? "";
            if (args.Value.TryGetProperty("namespaceFilter", out var nsEl))
                namespaceFilter = nsEl.GetString();
            if (args.Value.TryGetProperty("nameFilter", out var nameEl))
                nameFilter = nameEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new
            {
                error = $"Solution file not found: {solutionPath}"
            };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var project = solution.Projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
            if (project is null)
            {
                var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                return new
                {
                    error = $"Project '{projectName}' not found. Available: {available}"
                };
            }

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
            {
                return new
                {
                    error = "Failed to get compilation"
                };
            }

            var types = new List<object>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var typeDecl in typeDeclarations)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(typeDecl, ct);
                    if (symbol is not INamedTypeSymbol namedType)
                        continue;
                    var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
                    var name = namedType.Name;
                    // Apply filters
                    if (namespaceFilter != null && !ns.Contains(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (nameFilter != null && !name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var lineSpan = typeDecl.GetLocation().GetLineSpan();
                    types.Add(new
                    {
                        name,
                        fullName = namedType.ToDisplayString(),
                        @namespace = ns,
                        kind = typeDecl switch
                        {
                            InterfaceDeclarationSyntax => "interface",
                            RecordDeclarationSyntax => "record",
                            StructDeclarationSyntax => "struct",
                            _ => "class"
                        },
                        filePath = syntaxTree.FilePath,
                        line = lineSpan.StartLinePosition.Line + 1,
                        isAbstract = namedType.IsAbstract,
                        isSealed = namedType.IsSealed,
                        memberCount = namedType.GetMembers().Length
                    });
                }
            }

            return new
            {
                projectName = project.Name,
                totalTypes = types.Count,
                types = types.OrderBy(t => ((dynamic)t).fullName).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list classes in {Project}", projectName);
            return new
            {
                error = $"Failed to list classes: {ex.Message}"
            };
        }
    }
}
