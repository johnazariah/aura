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
    /// aura_navigate - Find code elements and relationships.
    /// Routes to: callers, implementations, derived_types, usages, by_attribute, extension_methods, by_return_type, references, definition.
    /// </summary>
    private async Task<object> NavigateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString() ?? throw new ArgumentException("operation is required");
        return operation switch
        {
            "callers" => await FindCallersAsync(args, ct),
            "implementations" => await FindImplementationsFromNavigate(args, ct),
            "derived_types" => await FindDerivedTypesFromNavigate(args, ct),
            "usages" => await FindUsagesAsync(args, ct),
            "by_attribute" => await FindByAttributeFromNavigate(args, ct),
            "extension_methods" => await FindExtensionMethodsFromNavigate(args, ct),
            "by_return_type" => await FindByReturnTypeFromNavigate(args, ct),
            "references" => await FindReferencesAsync(args, ct),
            "definition" => await FindDefinitionAsync(args, ct),
            _ => throw new ArgumentException($"Unknown navigate operation: {operation}")
        };
    }

    /// <summary>
    /// Find references - auto-detects language from filePath.
    /// </summary>
    private async Task<object> FindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        var language = DetectLanguageFromArgs(args);
        return language switch
        {
            "python" => await PythonFindReferencesAsync(args, ct),
            "typescript" => await TypeScriptFindReferencesAsync(args, ct),
            _ => await FindUsagesAsync(args, ct),
        };
    }

    /// <summary>
    /// Find definition - auto-detects language from filePath.
    /// </summary>
    private async Task<object> FindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var language = DetectLanguageFromArgs(args);

        if (language == "python")
        {
            if (!args.HasValue || !args.Value.TryGetProperty("projectPath", out _))
                return new { error = "projectPath is required for Python definition lookup" };
            if (!args.Value.TryGetProperty("offset", out _))
                return new { error = "offset is required for Python definition lookup" };
            return await PythonFindDefinitionAsync(args, ct);
        }

        if (language == "typescript")
        {
            if (!args.HasValue || !args.Value.TryGetProperty("projectPath", out _))
                return new { error = "projectPath is required for TypeScript definition lookup" };
            if (!args.Value.TryGetProperty("offset", out _))
                return new { error = "offset is required for TypeScript definition lookup" };
            return await TypeScriptFindDefinitionAsync(args, ct);
        }

        // For C#, find definition in code graph
        return await FindCSharpDefinitionAsync(args, ct);
    }

    /// <summary>
    /// Find C# symbol definition using Roslyn and code graph.
    /// </summary>
    private async Task<object> FindCSharpDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        string? symbolName = null;
        string? solutionPath = null;
        string? containingType = null;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("symbolName", out var symEl))
                symbolName = symEl.GetString();
            if (args.Value.TryGetProperty("solutionPath", out var solEl))
                solutionPath = solEl.GetString();
            if (args.Value.TryGetProperty("containingType", out var typeEl))
                containingType = typeEl.GetString();
        }

        if (string.IsNullOrEmpty(symbolName))
        {
            return new
            {
                error = "symbolName is required for C# definition lookup"
            };
        }

        // First try code graph
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindNodesAsync(symbolName, cancellationToken: ct);
        if (results.Count > 0)
        {
            // Filter by containing type if specified
            var filtered = containingType is not null ? results.Where(n => n.FullName?.Contains(containingType) == true).ToList() : results;
            if (filtered.Count > 0)
            {
                var node = filtered.First();
                return new
                {
                    found = true,
                    name = node.Name,
                    fullName = node.FullName,
                    kind = node.NodeType.ToString(),
                    filePath = TranslatePathIfWorktree(node.FilePath, worktreeInfo),
                    line = node.LineNumber,
                    message = $"Found {node.NodeType} {node.Name} at {node.FilePath}:{node.LineNumber}"
                };
            }
        }

        // If we have a solution, try Roslyn
        if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
        {
            try
            {
                var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync(ct);
                    if (compilation is null)
                        continue;
                    // Search all types
                    foreach (var typeSymbol in GetAllTypes(compilation))
                    {
                        // Check if this is the symbol we're looking for
                        if (typeSymbol.Name == symbolName)
                        {
                            var location = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource);
                            if (location is not null)
                            {
                                var lineSpan = location.GetLineSpan();
                                return new
                                {
                                    found = true,
                                    name = typeSymbol.Name,
                                    fullName = typeSymbol.ToDisplayString(),
                                    kind = typeSymbol.TypeKind.ToString(),
                                    filePath = TranslatePathIfWorktree(lineSpan.Path, worktreeInfo),
                                    line = lineSpan.StartLinePosition.Line + 1,
                                    message = $"Found {typeSymbol.TypeKind} {typeSymbol.Name}"
                                };
                            }
                        }

                        // Check members
                        var member = typeSymbol.GetMembers(symbolName).FirstOrDefault();
                        if (member is not null && (containingType is null || typeSymbol.Name == containingType))
                        {
                            var location = member.Locations.FirstOrDefault(l => l.IsInSource);
                            if (location is not null)
                            {
                                var lineSpan = location.GetLineSpan();
                                return new
                                {
                                    found = true,
                                    name = member.Name,
                                    fullName = member.ToDisplayString(),
                                    kind = member.Kind.ToString(),
                                    filePath = TranslatePathIfWorktree(lineSpan.Path, worktreeInfo),
                                    line = lineSpan.StartLinePosition.Line + 1,
                                    message = $"Found {member.Kind} {member.Name} in {typeSymbol.Name}"
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to find definition via Roslyn for {Symbol}", symbolName);
            }
        }

        return new
        {
            found = false,
            message = $"Symbol '{symbolName}' not found. Try specifying solutionPath for Roslyn-based lookup, or ensure the code graph is indexed."
        };
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.GlobalNamespace);
        while (stack.Count > 0)
        {
            var ns = stack.Pop();
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    stack.Push(childNs);
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                }
            }
        }
    }

    // Navigation helpers - adapt from old parameter names to new unified schema
    private async Task<object> FindImplementationsFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var language = DetectLanguageFromArgs(args);
        if (language == "typescript")
        {
            if (!args.HasValue || !args.Value.TryGetProperty("projectPath", out _))
                return new { error = "projectPath is required for TypeScript implementations lookup" };
            if (!args.Value.TryGetProperty("offset", out _))
                return new { error = "offset is required for TypeScript implementations lookup" };
            return await TypeScriptFindImplementationsAsync(args, ct);
        }

        var typeName = args?.GetProperty("symbolName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
    }

    private async Task<object> FindDerivedTypesFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("symbolName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
    }

    private async Task<object> FindByAttributeFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Delegate to existing implementation - just need to remap attributeName from symbolName if needed
        return await FindByAttributeAsync(args, ct);
    }

    private async Task<object> FindExtensionMethodsFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Remap targetType to extendedTypeName for existing implementation
        return await FindExtensionMethodsAsync(args, ct);
    }

    private async Task<object> FindByReturnTypeFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Remap targetType to returnTypeName for existing implementation
        return await FindByReturnTypeAsync(args, ct);
    }

    private async Task<object> FindImplementationsAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
    }

    private async Task<object> FindCallersAsync(JsonElement? args, CancellationToken ct)
    {
        var language = DetectLanguageFromArgs(args);
        if (language == "typescript")
        {
            if (!args.HasValue || !args.Value.TryGetProperty("projectPath", out _))
                return new { error = "projectPath is required for TypeScript callers lookup" };
            if (!args.Value.TryGetProperty("offset", out _))
                return new { error = "offset is required for TypeScript callers lookup" };
            return await TypeScriptFindCallersAsync(args, ct);
        }

        var methodName = args?.GetProperty("methodName").GetString() ?? "";
        string? containingType = null;
        if (args.HasValue && args.Value.TryGetProperty("containingType", out var typeEl))
        {
            containingType = typeEl.GetString();
        }

        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindCallersAsync(methodName, containingType, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
    }

    private async Task<object> FindDerivedTypesAsync(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("baseClassName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo), line = n.LineNumber });
    }

    private async Task<object> FindUsagesAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        string? containingType = null;
        if (args.HasValue && args.Value.TryGetProperty("containingType", out var typeEl))
        {
            containingType = typeEl.GetString();
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
            var usages = new List<object>();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                    continue;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);
                    // Find all identifier names matching our symbol
                    var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id => id.Identifier.Text == symbolName || (containingType != null && id.Identifier.Text == symbolName));
                    foreach (var identifier in identifiers)
                    {
                        var symbol = semanticModel.GetSymbolInfo(identifier, ct).Symbol;
                        if (symbol is null)
                            continue;
                        // Check if it matches containing type filter
                        if (containingType != null && symbol.ContainingType?.Name != containingType)
                            continue;
                        var location = identifier.GetLocation();
                        var lineSpan = location.GetLineSpan();
                        var line = lineSpan.StartLinePosition.Line + 1;
                        var lineText = (await syntaxTree.GetTextAsync(ct)).Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
                        usages.Add(new { filePath = syntaxTree.FilePath, line, column = lineSpan.StartLinePosition.Character + 1, codeSnippet = lineText.Length > 200 ? lineText[..200] + "..." : lineText, symbolKind = symbol.Kind.ToString(), containingMember = symbol.ContainingSymbol?.Name });
                        if (usages.Count >= 50)
                            break; // Limit results
                    }

                    if (usages.Count >= 50)
                        break;
                }

                if (usages.Count >= 50)
                    break;
            }

            return new
            {
                symbolName,
                totalUsages = usages.Count,
                wasTruncated = usages.Count >= 50,
                usages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find usages for {Symbol}", symbolName);
            return new
            {
                error = $"Failed to find usages: {ex.Message}"
            };
        }
    }

    private async Task<object> FindByAttributeAsync(JsonElement? args, CancellationToken ct)
    {
        var attributeName = args?.GetProperty("attributeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        string? targetKind = null;
        if (args.HasValue && args.Value.TryGetProperty("targetKind", out var kindEl))
        {
            targetKind = kindEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new
            {
                error = $"Solution file not found: {solutionPath}"
            };
        }

        // Normalize attribute name (remove Attribute suffix if present, add if not for matching)
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeName[..^9] : attributeName;
        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var results = new List<object>();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                    continue;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);
                    // Find all nodes with attributes
                    var nodesWithAttributes = root.DescendantNodes().Where(n => n is MemberDeclarationSyntax or ParameterSyntax).Where(n =>
                    {
                        var attrs = n switch
                        {
                            MethodDeclarationSyntax m => m.AttributeLists,
                            ClassDeclarationSyntax c => c.AttributeLists,
                            PropertyDeclarationSyntax p => p.AttributeLists,
                            FieldDeclarationSyntax f => f.AttributeLists,
                            ParameterSyntax param => param.AttributeLists,
                            _ => default
                        };
                        return attrs.Count > 0;
                    });
                    foreach (var node in nodesWithAttributes)
                    {
                        var attrs = node switch
                        {
                            MethodDeclarationSyntax m => m.AttributeLists,
                            ClassDeclarationSyntax c => c.AttributeLists,
                            PropertyDeclarationSyntax p => p.AttributeLists,
                            FieldDeclarationSyntax f => f.AttributeLists,
                            ParameterSyntax param => param.AttributeLists,
                            _ => default
                        };
                        foreach (var attrList in attrs)
                        {
                            foreach (var attr in attrList.Attributes)
                            {
                                var attrName = attr.Name.ToString();
                                // Match: HttpGet, HttpGetAttribute, [HttpGet], etc.
                                if (attrName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) || attrName.Equals(normalizedName + "Attribute", StringComparison.OrdinalIgnoreCase))
                                {
                                    var nodeKind = node switch
                                    {
                                        MethodDeclarationSyntax => "method",
                                        ClassDeclarationSyntax => "class",
                                        PropertyDeclarationSyntax => "property",
                                        FieldDeclarationSyntax => "field",
                                        ParameterSyntax => "parameter",
                                        _ => "other"
                                    };
                                    // Apply target kind filter
                                    if (targetKind != null && targetKind != "all" && !nodeKind.Equals(targetKind, StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    var nodeName = node switch
                                    {
                                        MethodDeclarationSyntax m => m.Identifier.Text,
                                        ClassDeclarationSyntax c => c.Identifier.Text,
                                        PropertyDeclarationSyntax p => p.Identifier.Text,
                                        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                                        ParameterSyntax param => param.Identifier.Text,
                                        _ => ""
                                    };
                                    var location = node.GetLocation();
                                    var lineSpan = location.GetLineSpan();
                                    results.Add(new { name = nodeName, kind = nodeKind, attribute = attrName, filePath = syntaxTree.FilePath, line = lineSpan.StartLinePosition.Line + 1 });
                                    if (results.Count >= 100)
                                        break;
                                }
                            }

                            if (results.Count >= 100)
                                break;
                        }

                        if (results.Count >= 100)
                            break;
                    }

                    if (results.Count >= 100)
                        break;
                }

                if (results.Count >= 100)
                    break;
            }

            return new
            {
                attributeName = normalizedName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find by attribute {Attribute}", attributeName);
            return new
            {
                error = $"Failed to find by attribute: {ex.Message}"
            };
        }
    }

    private async Task<object> FindExtensionMethodsAsync(JsonElement? args, CancellationToken ct)
    {
        var extendedTypeName = args?.GetProperty("extendedTypeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
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
            var results = new List<object>();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                    continue;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);
                    // Find all static classes (extension methods must be in static classes)
                    var staticClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Modifiers.Any(m => m.Text == "static"));
                    foreach (var staticClass in staticClasses)
                    {
                        var extensionMethods = staticClass.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(mod => mod.Text == "static") && m.ParameterList.Parameters.Count > 0 && m.ParameterList.Parameters[0].Modifiers.Any(mod => mod.Text == "this"));
                        foreach (var method in extensionMethods)
                        {
                            var firstParam = method.ParameterList.Parameters[0];
                            var paramTypeName = firstParam.Type?.ToString() ?? "";
                            // Check if this extends the requested type
                            if (paramTypeName.Contains(extendedTypeName, StringComparison.OrdinalIgnoreCase))
                            {
                                var location = method.GetLocation();
                                var lineSpan = location.GetLineSpan();
                                results.Add(new { methodName = method.Identifier.Text, containingClass = staticClass.Identifier.Text, extendedType = paramTypeName, returnType = method.ReturnType.ToString(), parameters = method.ParameterList.Parameters.Skip(1).Select(p => $"{p.Type} {p.Identifier}").ToList(), filePath = syntaxTree.FilePath, line = lineSpan.StartLinePosition.Line + 1 });
                                if (results.Count >= 100)
                                    break;
                            }
                        }

                        if (results.Count >= 100)
                            break;
                    }

                    if (results.Count >= 100)
                        break;
                }

                if (results.Count >= 100)
                    break;
            }

            return new
            {
                extendedTypeName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find extension methods for {Type}", extendedTypeName);
            return new
            {
                error = $"Failed to find extension methods: {ex.Message}"
            };
        }
    }

    private async Task<object> FindByReturnTypeAsync(JsonElement? args, CancellationToken ct)
    {
        var returnTypeName = args?.GetProperty("returnTypeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
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
            var results = new List<object>();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                    continue;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var root = await syntaxTree.GetRootAsync(ct);
                    // Find all methods
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var methodReturnType = method.ReturnType.ToString();
                        // Check if return type matches (including Task<T> unwrapping)
                        if (methodReturnType.Equals(returnTypeName, StringComparison.OrdinalIgnoreCase) || methodReturnType.Contains(returnTypeName, StringComparison.OrdinalIgnoreCase))
                        {
                            var containingType = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";
                            var location = method.GetLocation();
                            var lineSpan = location.GetLineSpan();
                            results.Add(new { methodName = method.Identifier.Text, containingType, returnType = methodReturnType, parameters = method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}").ToList(), filePath = syntaxTree.FilePath, line = lineSpan.StartLinePosition.Line + 1 });
                            if (results.Count >= 100)
                                break;
                        }
                    }

                    if (results.Count >= 100)
                        break;
                }

                if (results.Count >= 100)
                    break;
            }

            return new
            {
                returnTypeName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find by return type {Type}", returnTypeName);
            return new
            {
                error = $"Failed to find by return type: {ex.Message}"
            };
        }
    }
}
