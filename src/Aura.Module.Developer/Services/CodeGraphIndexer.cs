// <copyright file="CodeGraphIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Roslyn-based indexer that populates the code graph from C# source code.
/// </summary>
public class CodeGraphIndexer : ICodeGraphIndexer
{
    private const int MaxNameLength = 500;
    private const int MaxFullNameLength = 2000;
    private const int MaxSignatureLength = 2000;
    private const int MaxFilePathLength = 1000;

    private readonly IRoslynWorkspaceService _roslynWorkspace;
    private readonly ICodeGraphService _graphService;
    private readonly ILogger<CodeGraphIndexer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGraphIndexer"/> class.
    /// </summary>
    public CodeGraphIndexer(
        IRoslynWorkspaceService roslynWorkspace,
        ICodeGraphService graphService,
        ILogger<CodeGraphIndexer> logger)
    {
        _roslynWorkspace = roslynWorkspace;
        _graphService = graphService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CodeGraphIndexResult> IndexAsync(
        string solutionOrProjectPath,
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        var nodeCount = 0;
        var edgeCount = 0;
        var projectCount = 0;
        var fileCount = 0;
        var typeCount = 0;

        // Normalize workspace path for consistent storage (lowercase, forward slashes)
        var normalizedWorkspacePath = Aura.Foundation.Rag.PathNormalizer.Normalize(workspacePath);

        try
        {
            _logger.LogInformation("Indexing {Path} for workspace {WorkspacePath}", solutionOrProjectPath, normalizedWorkspacePath);

            // Load the solution/project
            var solution = await _roslynWorkspace.GetSolutionAsync(solutionOrProjectPath, cancellationToken);
            if (solution == null)
            {
                return new CodeGraphIndexResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to load solution: {solutionOrProjectPath}",
                    Duration = stopwatch.Elapsed,
                };
            }

            // Create solution node
            var solutionNode = await _graphService.AddNodeAsync(new CodeNode
            {
                Id = Guid.NewGuid(),
                NodeType = CodeNodeType.Solution,
                Name = Path.GetFileNameWithoutExtension(solutionOrProjectPath),
                FilePath = solutionOrProjectPath,
                WorkspacePath = normalizedWorkspacePath,
            }, cancellationToken);
            nodeCount++;

            // Dictionary to track nodes for edge creation
            var nodesBySymbol = new Dictionary<string, CodeNode>();

            // Index each project
            foreach (var project in solution.Projects)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var projectResult = await IndexProjectAsync(
                    project,
                    solutionNode,
                    normalizedWorkspacePath,
                    nodesBySymbol,
                    warnings,
                    cancellationToken);

                nodeCount += projectResult.NodeCount;
                edgeCount += projectResult.EdgeCount;
                fileCount += projectResult.FileCount;
                typeCount += projectResult.TypeCount;
                projectCount++;
            }

            // Create cross-project edges (project references)
            edgeCount += await CreateProjectReferenceEdgesAsync(solution, nodesBySymbol, cancellationToken);

            // Save all changes - nodes first, then edges for FK constraints
            _logger.LogInformation("Saving {NodeCount} nodes and {EdgeCount} edges to database", nodeCount, edgeCount);
            try
            {
                await _graphService.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save graph changes: {Message}", ex.InnerException?.Message ?? ex.Message);
                throw;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Indexed {NodeCount} nodes, {EdgeCount} edges in {Duration:N2}s",
                nodeCount,
                edgeCount,
                stopwatch.Elapsed.TotalSeconds);

            return new CodeGraphIndexResult
            {
                Success = true,
                NodesCreated = nodeCount,
                EdgesCreated = edgeCount,
                ProjectsIndexed = projectCount,
                FilesIndexed = fileCount,
                TypesIndexed = typeCount,
                Duration = stopwatch.Elapsed,
                Warnings = warnings,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index {Path}", solutionOrProjectPath);
            return new CodeGraphIndexResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                NodesCreated = nodeCount,
                EdgesCreated = edgeCount,
                Duration = stopwatch.Elapsed,
                Warnings = warnings,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<CodeGraphIndexResult> ReindexAsync(
        string solutionOrProjectPath,
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Aura.Foundation.Rag.PathNormalizer.Normalize(workspacePath);
        _logger.LogInformation("Re-indexing workspace {WorkspacePath}", normalizedPath);

        // Clear existing graph
        await _graphService.ClearWorkspaceGraphAsync(normalizedPath, cancellationToken);

        // Index fresh
        return await IndexAsync(solutionOrProjectPath, workspacePath, cancellationToken);
    }

    private async Task<(int NodeCount, int EdgeCount, int FileCount, int TypeCount)> IndexProjectAsync(
        Project project,
        CodeNode solutionNode,
        string workspacePath,
        Dictionary<string, CodeNode> nodesBySymbol,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var nodeCount = 0;
        var edgeCount = 0;
        var fileCount = 0;
        var typeCount = 0;

        _logger.LogDebug("Indexing project {ProjectName}", project.Name);

        // Create project node
        var projectNode = await _graphService.AddNodeAsync(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = CodeNodeType.Project,
            Name = project.Name,
            FilePath = project.FilePath,
            WorkspacePath = workspacePath,
        }, cancellationToken);
        nodeCount++;
        nodesBySymbol[$"project:{project.Name}"] = projectNode;

        // Solution contains Project edge
        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Contains,
            SourceId = solutionNode.Id,
            TargetId = projectNode.Id,
        }, cancellationToken);
        edgeCount++;

        // Get compilation for symbol resolution
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            warnings.Add($"Could not get compilation for project {project.Name}");
            return (nodeCount, edgeCount, fileCount, typeCount);
        }

        // Index each document
        foreach (var document in project.Documents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Skip generated files
            if (document.FilePath?.Contains("obj") == true ||
                document.FilePath?.Contains(".g.cs") == true)
            {
                continue;
            }

            var docResult = await IndexDocumentAsync(
                document,
                projectNode,
                compilation,
                workspacePath,
                nodesBySymbol,
                warnings,
                cancellationToken);

            nodeCount += docResult.NodeCount;
            edgeCount += docResult.EdgeCount;
            typeCount += docResult.TypeCount;
            fileCount++;
        }

        return (nodeCount, edgeCount, fileCount, typeCount);
    }

    private async Task<(int NodeCount, int EdgeCount, int TypeCount)> IndexDocumentAsync(
        Document document,
        CodeNode projectNode,
        Compilation compilation,
        string workspacePath,
        Dictionary<string, CodeNode> nodesBySymbol,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var nodeCount = 0;
        var edgeCount = 0;
        var typeCount = 0;

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        if (syntaxTree == null)
        {
            return (nodeCount, edgeCount, typeCount);
        }

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        // Create file node
        var fileNode = await _graphService.AddNodeAsync(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = CodeNodeType.File,
            Name = Path.GetFileName(document.FilePath ?? "unknown"),
            FilePath = document.FilePath,
            WorkspacePath = workspacePath,
        }, cancellationToken);
        nodeCount++;
        if (document.FilePath != null)
        {
            nodesBySymbol[$"file:{document.FilePath}"] = fileNode;
        }

        // Project contains File edge
        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Contains,
            SourceId = projectNode.Id,
            TargetId = fileNode.Id,
        }, cancellationToken);
        edgeCount++;

        // Find all type declarations
        var typeDeclarations = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>();

        foreach (var typeDecl in typeDeclarations)
        {
            var typeResult = await IndexTypeAsync(
                typeDecl,
                fileNode,
                semanticModel,
                workspacePath,
                nodesBySymbol,
                warnings,
                cancellationToken);

            nodeCount += typeResult.NodeCount;
            edgeCount += typeResult.EdgeCount;
            typeCount++;
        }

        // Find enum declarations
        var enumDeclarations = root.DescendantNodes()
            .OfType<EnumDeclarationSyntax>();

        foreach (var enumDecl in enumDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(enumDecl);
            if (symbol == null)
            {
                continue;
            }

            var enumNode = await _graphService.AddNodeAsync(new CodeNode
            {
                Id = Guid.NewGuid(),
                NodeType = CodeNodeType.Enum,
                Name = symbol.Name,
                FullName = Truncate(symbol.ToDisplayString(), MaxFullNameLength),
                FilePath = document.FilePath,
                LineNumber = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Modifiers = GetModifiers(enumDecl.Modifiers),
                WorkspacePath = workspacePath,
            }, cancellationToken);
            nodeCount++;
            nodesBySymbol[$"type:{symbol.ToDisplayString()}"] = enumNode;

            // File contains Enum edge
            await _graphService.AddEdgeAsync(new CodeEdge
            {
                Id = Guid.NewGuid(),
                EdgeType = CodeEdgeType.Contains,
                SourceId = fileNode.Id,
                TargetId = enumNode.Id,
            }, cancellationToken);
            edgeCount++;
            typeCount++;
        }

        return (nodeCount, edgeCount, typeCount);
    }

    private async Task<(int NodeCount, int EdgeCount)> IndexTypeAsync(
        TypeDeclarationSyntax typeDecl,
        CodeNode fileNode,
        SemanticModel semanticModel,
        string workspacePath,
        Dictionary<string, CodeNode> nodesBySymbol,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var nodeCount = 0;
        var edgeCount = 0;

        var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null)
        {
            return (nodeCount, edgeCount);
        }

        var nodeType = typeDecl switch
        {
            ClassDeclarationSyntax => CodeNodeType.Class,
            InterfaceDeclarationSyntax => CodeNodeType.Interface,
            RecordDeclarationSyntax => CodeNodeType.Record,
            StructDeclarationSyntax => CodeNodeType.Struct,
            _ => CodeNodeType.Class,
        };

        var typeNode = await _graphService.AddNodeAsync(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = nodeType,
            Name = symbol.Name,
            FullName = Truncate(symbol.ToDisplayString(), MaxFullNameLength),
            FilePath = typeDecl.SyntaxTree.FilePath,
            LineNumber = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            Modifiers = GetModifiers(typeDecl.Modifiers),
            WorkspacePath = workspacePath,
        }, cancellationToken);
        nodeCount++;
        var typeKey = $"type:{symbol.ToDisplayString()}";
        nodesBySymbol[typeKey] = typeNode;

        // File contains Type edge
        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Contains,
            SourceId = fileNode.Id,
            TargetId = typeNode.Id,
        }, cancellationToken);
        edgeCount++;

        // Create namespace node/edge if namespace exists
        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
        {
            var nsKey = $"namespace:{symbol.ContainingNamespace.ToDisplayString()}";
            if (!nodesBySymbol.TryGetValue(nsKey, out var nsNode))
            {
                nsNode = await _graphService.AddNodeAsync(new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.Namespace,
                    Name = symbol.ContainingNamespace.Name,
                    FullName = Truncate(symbol.ContainingNamespace.ToDisplayString(), MaxFullNameLength),
                    WorkspacePath = workspacePath,
                }, cancellationToken);
                nodeCount++;
                nodesBySymbol[nsKey] = nsNode;
            }

            // Namespace declares Type edge
            await _graphService.AddEdgeAsync(new CodeEdge
            {
                Id = Guid.NewGuid(),
                EdgeType = CodeEdgeType.Declares,
                SourceId = nsNode.Id,
                TargetId = typeNode.Id,
            }, cancellationToken);
            edgeCount++;
        }

        // Index inheritance
        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            edgeCount += await CreateInheritanceEdgeAsync(typeNode, symbol.BaseType, nodesBySymbol, workspacePath, cancellationToken);
        }

        // Index interface implementations
        foreach (var iface in symbol.Interfaces)
        {
            edgeCount += await CreateImplementsEdgeAsync(typeNode, iface, nodesBySymbol, workspacePath, cancellationToken);
        }

        // Index members
        foreach (var member in symbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            var memberResult = await IndexMemberAsync(member, typeNode, semanticModel, workspacePath, nodesBySymbol, cancellationToken);
            nodeCount += memberResult.NodeCount;
            edgeCount += memberResult.EdgeCount;
        }

        return (nodeCount, edgeCount);
    }

    private async Task<(int NodeCount, int EdgeCount)> IndexMemberAsync(
        ISymbol member,
        CodeNode typeNode,
        SemanticModel semanticModel,
        string workspacePath,
        Dictionary<string, CodeNode> nodesBySymbol,
        CancellationToken cancellationToken)
    {
        var nodeCount = 0;
        var edgeCount = 0;

        var (nodeType, signature) = member switch
        {
            IMethodSymbol method => (
                method.MethodKind == MethodKind.Constructor ? CodeNodeType.Constructor : CodeNodeType.Method,
                method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
            IPropertySymbol prop => (CodeNodeType.Property, prop.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
            IFieldSymbol field => (CodeNodeType.Field, field.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
            IEventSymbol evt => (CodeNodeType.Event, evt.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
            _ => (CodeNodeType.Method, member.Name),
        };

        // Get location info
        var location = member.Locations.FirstOrDefault();
        var filePath = location?.SourceTree?.FilePath;
        var lineNumber = location?.GetLineSpan().StartLinePosition.Line + 1;

        // Get modifiers
        var modifiers = new List<string>();
        if (member.DeclaredAccessibility != Accessibility.NotApplicable)
        {
            modifiers.Add(member.DeclaredAccessibility.ToString().ToLowerInvariant());
        }

        if (member.IsStatic)
        {
            modifiers.Add("static");
        }

        if (member.IsAbstract)
        {
            modifiers.Add("abstract");
        }

        if (member.IsVirtual)
        {
            modifiers.Add("virtual");
        }

        if (member.IsOverride)
        {
            modifiers.Add("override");
        }

        if (member.IsSealed)
        {
            modifiers.Add("sealed");
        }

        var memberNode = await _graphService.AddNodeAsync(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = nodeType,
            Name = member.Name,
            FullName = Truncate(member.ToDisplayString(), MaxFullNameLength),
            FilePath = filePath,
            LineNumber = lineNumber,
            Signature = Truncate(signature, MaxSignatureLength),
            Modifiers = string.Join(" ", modifiers),
            WorkspacePath = workspacePath,
        }, cancellationToken);
        nodeCount++;
        nodesBySymbol[$"member:{member.ToDisplayString()}"] = memberNode;

        // Type contains Member edge
        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Contains,
            SourceId = typeNode.Id,
            TargetId = memberNode.Id,
        }, cancellationToken);
        edgeCount++;

        // Index method calls and type usages (for methods only)
        if (member is IMethodSymbol methodSymbol)
        {
            var syntax = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken);
            if (syntax != null)
            {
                edgeCount += await IndexMethodCallsAsync(memberNode, syntax, semanticModel, nodesBySymbol, workspacePath, cancellationToken);
            }

            // Index override relationship
            if (methodSymbol.IsOverride && methodSymbol.OverriddenMethod != null)
            {
                var overriddenKey = $"member:{methodSymbol.OverriddenMethod.ToDisplayString()}";
                if (nodesBySymbol.TryGetValue(overriddenKey, out var overriddenNode))
                {
                    await _graphService.AddEdgeAsync(new CodeEdge
                    {
                        Id = Guid.NewGuid(),
                        EdgeType = CodeEdgeType.Overrides,
                        SourceId = memberNode.Id,
                        TargetId = overriddenNode.Id,
                    }, cancellationToken);
                    edgeCount++;
                }
            }
        }

        return (nodeCount, edgeCount);
    }

    private async Task<int> IndexMethodCallsAsync(
        CodeNode memberNode,
        SyntaxNode syntax,
        SemanticModel semanticModel,
        Dictionary<string, CodeNode> nodesBySymbol,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var edgeCount = 0;
        var processedCalls = new HashSet<string>();

        // Find all invocation expressions
        var invocations = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            try
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
                {
                    continue;
                }

                var calleeKey = $"member:{calledMethod.ToDisplayString()}";

                // Only create edge once per unique call target
                if (!processedCalls.Add(calleeKey))
                {
                    continue;
                }

                // Try to find existing node, or create placeholder
                if (!nodesBySymbol.TryGetValue(calleeKey, out var calleeNode))
                {
                    // Create node for external method reference
                    calleeNode = await _graphService.AddNodeAsync(new CodeNode
                    {
                        Id = Guid.NewGuid(),
                        NodeType = CodeNodeType.Method,
                        Name = calledMethod.Name,
                        FullName = Truncate(calledMethod.ToDisplayString(), MaxFullNameLength),
                        Signature = Truncate(calledMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), MaxSignatureLength),
                        WorkspacePath = workspacePath,
                    }, cancellationToken);
                    nodesBySymbol[calleeKey] = calleeNode;
                }

                await _graphService.AddEdgeAsync(new CodeEdge
                {
                    Id = Guid.NewGuid(),
                    EdgeType = CodeEdgeType.Calls,
                    SourceId = memberNode.Id,
                    TargetId = calleeNode.Id,
                }, cancellationToken);
                edgeCount++;
            }
            catch (ArgumentException)
            {
                // Syntax node not in current tree - skip this invocation
            }
        }

        // Find type usages (object creations, type references)
        var objectCreations = syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        var processedTypes = new HashSet<string>();

        foreach (var creation in objectCreations)
        {
            try
            {
                var typeInfo = semanticModel.GetTypeInfo(creation);
                if (typeInfo.Type is INamedTypeSymbol createdType)
                {
                    var typeKey = $"type:{createdType.ToDisplayString()}";
                    if (!processedTypes.Add(typeKey))
                    {
                        continue;
                    }

                    if (nodesBySymbol.TryGetValue(typeKey, out var typeNode))
                    {
                        await _graphService.AddEdgeAsync(new CodeEdge
                        {
                            Id = Guid.NewGuid(),
                            EdgeType = CodeEdgeType.Uses,
                            SourceId = memberNode.Id,
                            TargetId = typeNode.Id,
                        }, cancellationToken);
                        edgeCount++;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Syntax node not in current tree - skip
            }
        }

        return edgeCount;
    }

    private async Task<int> CreateInheritanceEdgeAsync(
        CodeNode derivedNode,
        INamedTypeSymbol baseType,
        Dictionary<string, CodeNode> nodesBySymbol,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var baseKey = $"type:{baseType.ToDisplayString()}";

        if (!nodesBySymbol.TryGetValue(baseKey, out var baseNode))
        {
            // Create node for external base type
            baseNode = await _graphService.AddNodeAsync(new CodeNode
            {
                Id = Guid.NewGuid(),
                NodeType = CodeNodeType.Class,
                Name = baseType.Name,
                FullName = Truncate(baseType.ToDisplayString(), MaxFullNameLength),
                WorkspacePath = workspacePath,
            }, cancellationToken);
            nodesBySymbol[baseKey] = baseNode;
        }

        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Inherits,
            SourceId = derivedNode.Id,
            TargetId = baseNode.Id,
        }, cancellationToken);

        return 1;
    }

    private async Task<int> CreateImplementsEdgeAsync(
        CodeNode implementorNode,
        INamedTypeSymbol interfaceType,
        Dictionary<string, CodeNode> nodesBySymbol,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var ifaceKey = $"type:{interfaceType.ToDisplayString()}";

        if (!nodesBySymbol.TryGetValue(ifaceKey, out var ifaceNode))
        {
            // Create node for external interface
            ifaceNode = await _graphService.AddNodeAsync(new CodeNode
            {
                Id = Guid.NewGuid(),
                NodeType = CodeNodeType.Interface,
                Name = interfaceType.Name,
                FullName = Truncate(interfaceType.ToDisplayString(), MaxFullNameLength),
                WorkspacePath = workspacePath,
            }, cancellationToken);
            nodesBySymbol[ifaceKey] = ifaceNode;
        }

        await _graphService.AddEdgeAsync(new CodeEdge
        {
            Id = Guid.NewGuid(),
            EdgeType = CodeEdgeType.Implements,
            SourceId = implementorNode.Id,
            TargetId = ifaceNode.Id,
        }, cancellationToken);

        return 1;
    }

    private async Task<int> CreateProjectReferenceEdgesAsync(
        Solution solution,
        Dictionary<string, CodeNode> nodesBySymbol,
        CancellationToken cancellationToken)
    {
        var edgeCount = 0;

        foreach (var project in solution.Projects)
        {
            var projectKey = $"project:{project.Name}";
            if (!nodesBySymbol.TryGetValue(projectKey, out var projectNode))
            {
                continue;
            }

            foreach (var reference in project.ProjectReferences)
            {
                var referencedProject = solution.GetProject(reference.ProjectId);
                if (referencedProject == null)
                {
                    continue;
                }

                var refKey = $"project:{referencedProject.Name}";
                if (nodesBySymbol.TryGetValue(refKey, out var refNode))
                {
                    await _graphService.AddEdgeAsync(new CodeEdge
                    {
                        Id = Guid.NewGuid(),
                        EdgeType = CodeEdgeType.References,
                        SourceId = projectNode.Id,
                        TargetId = refNode.Id,
                    }, cancellationToken);
                    edgeCount++;
                }
            }
        }

        return edgeCount;
    }

    private static string GetModifiers(SyntaxTokenList modifiers)
    {
        return string.Join(" ", modifiers.Select(m => m.Text));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}

