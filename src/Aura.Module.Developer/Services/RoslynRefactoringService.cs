// <copyright file="RoslynRefactoringService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for performing Roslyn-based code refactoring operations.
/// </summary>
public sealed class RoslynRefactoringService : IRoslynRefactoringService
{
    private readonly IRoslynWorkspaceService _workspaceService;
    private readonly ILogger<RoslynRefactoringService> _logger;

    public RoslynRefactoringService(
        IRoslynWorkspaceService workspaceService,
        ILogger<RoslynRefactoringService> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BlastRadiusResult> AnalyzeRenameAsync(RenameSymbolRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Analyzing blast radius for renaming {Symbol} to {NewName} in {Solution}",
            request.SymbolName, request.NewName, request.SolutionPath);

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the primary symbol
            var primarySymbol = await FindSymbolAsync(
                solution, request.SymbolName, request.ContainingType, request.FilePath, ct);

            if (primarySymbol is null)
            {
                return new BlastRadiusResult
                {
                    Operation = "rename",
                    Symbol = request.SymbolName,
                    NewName = request.NewName,
                    RelatedSymbols = [],
                    SuggestedPlan = [],
                    Error = $"Symbol '{request.SymbolName}' not found"
                };
            }

            // Collect related symbols by naming convention
            var relatedSymbols = await DiscoverRelatedSymbolsAsync(
                solution, request.SymbolName, primarySymbol, ct);

            // Build suggested plan
            var suggestedPlan = BuildSuggestedPlan(
                request.SymbolName, request.NewName, relatedSymbols);

            // Calculate totals (only count symbols where we actually counted references)
            var totalReferences = relatedSymbols.Where(s => s.ReferenceCount > 0).Sum(s => s.ReferenceCount);
            var filesAffected = relatedSymbols.Select(s => s.FilePath).Distinct().Count();
            var filesToRename = relatedSymbols.Count(s =>
                Path.GetFileNameWithoutExtension(s.FilePath)
                    .Equals(s.Name, StringComparison.OrdinalIgnoreCase));

            return new BlastRadiusResult
            {
                Operation = "rename",
                Symbol = request.SymbolName,
                NewName = request.NewName,
                RelatedSymbols = relatedSymbols,
                TotalReferences = totalReferences,
                FilesAffected = filesAffected,
                FilesToRename = filesToRename,
                SuggestedPlan = suggestedPlan,
                AwaitsConfirmation = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze blast radius for {Symbol}", request.SymbolName);
            return new BlastRadiusResult
            {
                Operation = "rename",
                Symbol = request.SymbolName,
                NewName = request.NewName,
                RelatedSymbols = [],
                SuggestedPlan = [],
                Error = ex.Message
            };
        }
    }

    private async Task<IReadOnlyList<RelatedSymbol>> DiscoverRelatedSymbolsAsync(
        Solution solution,
        string primaryName,
        ISymbol primarySymbol,
        CancellationToken ct)
    {
        const int MaxRelatedSymbols = 50;

        var related = new List<RelatedSymbol>();
        var seenSymbols = new HashSet<string>();

        // Add the primary symbol first - only count references for this one
        _logger.LogDebug("Counting references for primary symbol {Symbol}...", primaryName);
        var primaryRefs = await SymbolFinder.FindReferencesAsync(primarySymbol, solution, ct);
        var primaryRefCount = primaryRefs.Sum(r => r.Locations.Count());

        related.Add(new RelatedSymbol
        {
            Name = primaryName,
            Kind = primarySymbol.Kind.ToString(),
            FilePath = primarySymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
            ReferenceCount = primaryRefCount
        });
        seenSymbols.Add(primarySymbol.ToDisplayString());

        _logger.LogDebug("Primary symbol has {Count} references. Scanning for related symbols...", primaryRefCount);

        // Find related symbols by naming convention (prefix matching)
        // E.g., if renaming "Workflow", also find "WorkflowStep", "IWorkflowService", etc.
        // NOTE: We skip reference counting for related symbols to avoid O(n) FindReferencesAsync calls
        var relatedCount = 0;
        foreach (var project in solution.Projects)
        {
            if (relatedCount >= MaxRelatedSymbols)
            {
                _logger.LogInformation("Reached maximum of {Max} related symbols, stopping scan", MaxRelatedSymbols);
                break;
            }

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            foreach (var typeSymbol in GetAllTypes(compilation))
            {
                if (relatedCount >= MaxRelatedSymbols) break;

                if (seenSymbols.Contains(typeSymbol.ToDisplayString()))
                    continue;

                var typeName = typeSymbol.Name;

                // Check if this type is related by naming convention
                // Rules:
                // 1. Starts with primaryName: WorkflowStep, WorkflowStatus → related
                // 2. Interface starting with I + primaryName: IWorkflow, IWorkflowService → related
                // 3. EndsWith is too broad (GitHubWorkflow is NOT related to Workflow)
                bool isRelated =
                    typeName.StartsWith(primaryName, StringComparison.Ordinal) || // WorkflowStep
                    (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]) &&
                     typeName[1..].StartsWith(primaryName, StringComparison.Ordinal)); // IWorkflow, IWorkflowService

                if (!isRelated) continue;

                seenSymbols.Add(typeSymbol.ToDisplayString());
                relatedCount++;

                _logger.LogDebug("Found related symbol: {TypeName} ({Kind})", typeName, typeSymbol.TypeKind);

                // Don't count references for related symbols - too expensive
                // Just record that they exist and would need renaming
                // ReferenceCount = 0 means "not counted" (we only count the primary symbol)
                related.Add(new RelatedSymbol
                {
                    Name = typeName,
                    Kind = typeSymbol.TypeKind.ToString(),
                    FilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
                    ReferenceCount = 0
                });
            }
        }

        _logger.LogInformation("Found {Count} related symbols for {Primary}", related.Count - 1, primaryName);

        return related.OrderByDescending(s => s.ReferenceCount).ToList();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.GlobalNamespace);

        while (stack.Count > 0)
        {
            var ns = stack.Pop();

            foreach (var type in ns.GetTypeMembers())
            {
                yield return type;
            }

            foreach (var childNs in ns.GetNamespaceMembers())
            {
                stack.Push(childNs);
            }
        }
    }

    private static IReadOnlyList<SuggestedOperation> BuildSuggestedPlan(
        string oldName,
        string newName,
        IReadOnlyList<RelatedSymbol> relatedSymbols)
    {
        var plan = new List<SuggestedOperation>();
        var order = 1;

        foreach (var symbol in relatedSymbols)
        {
            // Calculate the new name by replacing the old name pattern
            var suggestedNewName = symbol.Name.Replace(oldName, newName);

            // Handle interface prefix (IWorkflow -> IStory)
            if (symbol.Name.StartsWith("I") && symbol.Name.Length > 1 && char.IsUpper(symbol.Name[1]))
            {
                if (symbol.Name[1..].StartsWith(oldName))
                {
                    suggestedNewName = "I" + symbol.Name[1..].Replace(oldName, newName);
                }
            }

            plan.Add(new SuggestedOperation
            {
                Order = order++,
                Operation = "rename",
                Target = symbol.Name,
                NewValue = suggestedNewName,
                ReferenceCount = symbol.ReferenceCount
            });
        }

        // Add file renames for types that match their filename
        foreach (var symbol in relatedSymbols)
        {
            var fileName = Path.GetFileNameWithoutExtension(symbol.FilePath);
            if (fileName.Equals(symbol.Name, StringComparison.OrdinalIgnoreCase))
            {
                var newFileName = symbol.Name.Replace(oldName, newName);
                if (symbol.Name.StartsWith("I") && symbol.Name.Length > 1 && char.IsUpper(symbol.Name[1]))
                {
                    if (symbol.Name[1..].StartsWith(oldName))
                    {
                        newFileName = "I" + symbol.Name[1..].Replace(oldName, newName);
                    }
                }

                plan.Add(new SuggestedOperation
                {
                    Order = order++,
                    Operation = "rename_file",
                    Target = symbol.FilePath,
                    NewValue = Path.Combine(
                        Path.GetDirectoryName(symbol.FilePath) ?? "",
                        newFileName + Path.GetExtension(symbol.FilePath)),
                    ReferenceCount = 0
                });
            }
        }

        return plan;
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> RenameSymbolAsync(RenameSymbolRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Renaming symbol {OldName} to {NewName} in {Solution}",
            request.SymbolName, request.NewName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            _logger.LogDebug("Loading solution...");
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            _logger.LogDebug("Solution loaded with {Count} projects", solution.ProjectIds.Count);

            // Find the symbol
            _logger.LogDebug("Finding symbol {Symbol}...", request.SymbolName);
            var symbol = await FindSymbolAsync(
                solution, request.SymbolName, request.ContainingType, request.FilePath, ct);

            if (symbol is null)
            {
                return RefactoringResult.Failed(
                    $"Symbol '{request.SymbolName}' not found" +
                    (request.ContainingType != null ? $" in type '{request.ContainingType}'" : ""));
            }

            _logger.LogDebug("Found symbol: {Symbol} ({Kind})", symbol.ToDisplayString(), symbol.Kind);

            // Perform the rename
            _logger.LogInformation("Performing Roslyn rename (this may take a moment)...");
            var newSolution = await Renamer.RenameSymbolAsync(
                solution,
                symbol,
                new SymbolRenameOptions(),
                request.NewName,
                ct);
            _logger.LogDebug("Roslyn rename complete, computing changed documents...");

            // Get changed documents
            var changedDocs = GetChangedDocuments(solution, newSolution);
            _logger.LogInformation("Rename affects {Count} documents", changedDocs.Count);

            if (request.Preview)
            {
                // Return preview without applying
                var preview = new List<FileChange>();
                foreach (var doc in changedDocs)
                {
                    var originalDoc = solution.GetDocument(doc.Id);
                    if (originalDoc is null) continue;

                    var originalText = (await originalDoc.GetTextAsync(ct)).ToString();
                    var newText = (await doc.GetTextAsync(ct)).ToString();
                    preview.Add(new FileChange(doc.FilePath ?? "", originalText, newText));
                }

                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would rename '{request.SymbolName}' to '{request.NewName}' in {changedDocs.Count} files",
                    Preview = preview
                };
            }

            // Apply changes to disk
            var modifiedFiles = new List<string>();
            foreach (var doc in changedDocs)
            {
                if (doc.FilePath is null) continue;
                var text = await doc.GetTextAsync(ct);
                await File.WriteAllTextAsync(doc.FilePath, text.ToString(), ct);
                modifiedFiles.Add(doc.FilePath);
                _logger.LogDebug("Updated file: {Path}", doc.FilePath);
            }

            // Clear workspace cache so subsequent operations see the updated files
            _workspaceService.ClearCache();
            _logger.LogDebug("Cleared workspace cache after rename");

            // Optionally validate with build and grep for residuals
            ValidationResult? validation = null;
            if (request.Validate)
            {
                validation = await ValidateRefactoringAsync(
                    request.SolutionPath, request.SymbolName, ct);
            }

            return new RefactoringResult
            {
                Success = true,
                Message = $"Renamed '{request.SymbolName}' to '{request.NewName}' in {modifiedFiles.Count} files",
                ModifiedFiles = modifiedFiles,
                Validation = validation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename symbol {Symbol}", request.SymbolName);
            return RefactoringResult.Failed($"Failed to rename: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> ChangeMethodSignatureAsync(ChangeSignatureRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Changing signature of {Type}.{Method} in {Solution}",
            request.ContainingType, request.MethodName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the method symbol
            var methodSymbol = await FindSymbolAsync(
                solution, request.MethodName, request.ContainingType, null, ct) as IMethodSymbol;

            if (methodSymbol is null)
            {
                return RefactoringResult.Failed(
                    $"Method '{request.MethodName}' not found in type '{request.ContainingType}'");
            }

            // Find all references to update call sites
            var references = await SymbolFinder.FindReferencesAsync(methodSymbol, solution, ct);
            var modifiedFiles = new List<string>();

            // Find the method declaration
            var methodDecl = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (methodDecl is null)
            {
                return RefactoringResult.Failed("Could not find method declaration");
            }

            var methodNode = await methodDecl.GetSyntaxAsync(ct) as MethodDeclarationSyntax;
            if (methodNode is null)
            {
                return RefactoringResult.Failed("Could not parse method declaration");
            }

            var document = solution.GetDocument(methodDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document containing method");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Build new parameter list
            var existingParams = methodNode.ParameterList.Parameters.ToList();
            var newParams = new List<ParameterSyntax>(existingParams);

            // Remove parameters
            if (request.RemoveParameters?.Count > 0)
            {
                newParams = newParams.Where(p =>
                    !request.RemoveParameters.Contains(p.Identifier.Text)).ToList();
            }

            // Add parameters
            if (request.AddParameters?.Count > 0)
            {
                foreach (var param in request.AddParameters)
                {
                    var paramSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(param.Name))
                        .WithType(SyntaxFactory.ParseTypeName(param.Type));

                    if (param.DefaultValue != null)
                    {
                        paramSyntax = paramSyntax.WithDefault(
                            SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.ParseExpression(param.DefaultValue)));
                    }

                    newParams.Add(paramSyntax);
                }
            }

            // Create new parameter list
            var newParamList = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(newParams));

            var newMethodNode = methodNode.WithParameterList(newParamList);

            // Replace in tree
            var newRoot = root.ReplaceNode(methodNode, newMethodNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                var newText = formattedRoot.ToFullString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would update method signature in {document.FilePath}",
                    Preview = [new FileChange(document.FilePath ?? "", originalText, newText)]
                };
            }

            // Write to disk
            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
                modifiedFiles.Add(document.FilePath);
            }

            // Update call sites with default values
            if (request.AddParameters?.Count > 0)
            {
                foreach (var refGroup in references)
                {
                    foreach (var location in refGroup.Locations)
                    {
                        if (location.Document.FilePath is null) continue;
                        if (modifiedFiles.Contains(location.Document.FilePath)) continue;

                        var callDoc = location.Document;
                        var callRoot = await callDoc.GetSyntaxRootAsync(ct);
                        if (callRoot is null) continue;

                        var callNode = callRoot.FindNode(location.Location.SourceSpan);
                        if (callNode?.Parent is not InvocationExpressionSyntax invocation) continue;

                        // Add default arguments for new parameters
                        var newArgs = invocation.ArgumentList.Arguments.ToList();
                        foreach (var param in request.AddParameters)
                        {
                            var argExpr = param.DefaultValue != null
                                ? SyntaxFactory.ParseExpression(param.DefaultValue)
                                : SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(param.Type));
                            newArgs.Add(SyntaxFactory.Argument(argExpr));
                        }

                        var newInvocation = invocation.WithArgumentList(
                            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArgs)));

                        var newCallRoot = callRoot.ReplaceNode(invocation, newInvocation);
                        await File.WriteAllTextAsync(callDoc.FilePath, newCallRoot.ToFullString(), ct);
                        modifiedFiles.Add(callDoc.FilePath);
                    }
                }
            }

            return RefactoringResult.Succeeded(
                $"Changed signature of '{request.ContainingType}.{request.MethodName}', updated {modifiedFiles.Count} files",
                modifiedFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change method signature");
            return RefactoringResult.Failed($"Failed to change signature: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> ImplementInterfaceAsync(ImplementInterfaceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Implementing {Interface} on {Class} in {Solution}",
            request.InterfaceName, request.ClassName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the class
            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            // Find the interface
            var interfaceSymbol = await FindTypeAsync(solution, request.InterfaceName, ct) as INamedTypeSymbol;
            if (interfaceSymbol is null || interfaceSymbol.TypeKind != TypeKind.Interface)
            {
                return RefactoringResult.Failed($"Interface '{request.InterfaceName}' not found");
            }

            // Get the class declaration
            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var classNode = await classDecl.GetSyntaxAsync(ct) as ClassDeclarationSyntax;
            if (classNode is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var document = solution.GetDocument(classDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document containing class");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Find already implemented members
            var existingMembers = classSymbol.GetMembers().Select(m => m.Name).ToHashSet();

            // Generate stub implementations
            var membersToAdd = new List<MemberDeclarationSyntax>();

            foreach (var member in interfaceSymbol.GetMembers())
            {
                if (existingMembers.Contains(member.Name)) continue;

                switch (member)
                {
                    case IMethodSymbol method when !method.IsStatic:
                        membersToAdd.Add(GenerateMethodStub(method, request.ExplicitImplementation, interfaceSymbol));
                        break;

                    case IPropertySymbol property:
                        membersToAdd.Add(GeneratePropertyStub(property, request.ExplicitImplementation, interfaceSymbol));
                        break;
                }
            }

            if (membersToAdd.Count == 0)
            {
                return RefactoringResult.Succeeded(
                    $"Class '{request.ClassName}' already implements all members of '{request.InterfaceName}'",
                    []);
            }

            // Add interface to base list if not present
            var baseList = classNode.BaseList ?? SyntaxFactory.BaseList();
            var hasInterface = baseList.Types.Any(t =>
                t.Type.ToString().Contains(request.InterfaceName));

            if (!hasInterface)
            {
                var interfaceType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(request.InterfaceName));
                baseList = baseList.AddTypes(interfaceType);
            }

            var newClassNode = classNode
                .WithBaseList(baseList)
                .AddMembers(membersToAdd.ToArray());

            var newRoot = root.ReplaceNode(classNode, newClassNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would add {membersToAdd.Count} member stubs to '{request.ClassName}'",
                    Preview = [new FileChange(document.FilePath ?? "", originalText, formattedRoot.ToFullString())]
                };
            }

            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
            }

            return RefactoringResult.Succeeded(
                $"Implemented {membersToAdd.Count} members of '{request.InterfaceName}' on '{request.ClassName}'",
                document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to implement interface");
            return RefactoringResult.Failed($"Failed to implement interface: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> GenerateConstructorAsync(GenerateConstructorRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating constructor for {Class} in {Solution}",
            request.ClassName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the class
            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var classNode = await classDecl.GetSyntaxAsync(ct) as ClassDeclarationSyntax;
            if (classNode is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var document = solution.GetDocument(classDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document containing class");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Get fields/properties to initialize
            var members = classSymbol.GetMembers()
                .Where(m => m is IFieldSymbol { IsReadOnly: true, IsStatic: false } or
                            IPropertySymbol { IsReadOnly: true, IsStatic: false })
                .ToList();

            if (request.Members?.Count > 0)
            {
                members = members.Where(m => request.Members.Contains(m.Name)).ToList();
            }

            if (members.Count == 0)
            {
                return RefactoringResult.Failed("No members found to initialize");
            }

            // Generate constructor
            var parameters = new List<ParameterSyntax>();
            var assignments = new List<StatementSyntax>();

            foreach (var member in members)
            {
                var typeName = member switch
                {
                    IFieldSymbol f => f.Type.ToDisplayString(),
                    IPropertySymbol p => p.Type.ToDisplayString(),
                    _ => "object"
                };

                var paramName = ToCamelCase(member.Name);
                parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                    .WithType(SyntaxFactory.ParseTypeName(typeName)));

                assignments.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(member.Name),
                        SyntaxFactory.IdentifierName(paramName))));
            }

            var constructor = SyntaxFactory.ConstructorDeclaration(classNode.Identifier)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
                .WithBody(SyntaxFactory.Block(assignments));

            var newClassNode = classNode.AddMembers(constructor);
            var newRoot = root.ReplaceNode(classNode, newClassNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would add constructor with {members.Count} parameters to '{request.ClassName}'",
                    Preview = [new FileChange(document.FilePath ?? "", originalText, formattedRoot.ToFullString())]
                };
            }

            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
            }

            return RefactoringResult.Succeeded(
                $"Generated constructor with {members.Count} parameters for '{request.ClassName}'",
                document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate constructor");
            return RefactoringResult.Failed($"Failed to generate constructor: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> ExtractInterfaceAsync(ExtractInterfaceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting interface {Interface} from {Class} in {Solution}",
            request.InterfaceName, request.ClassName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the class
            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var classNode = await classDecl.GetSyntaxAsync(ct) as ClassDeclarationSyntax;
            if (classNode is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var document = solution.GetDocument(classDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document containing class");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Get public members
            var publicMembers = classSymbol.GetMembers()
                .Where(m => m.DeclaredAccessibility == Accessibility.Public &&
                           !m.IsStatic &&
                           m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol)
                .ToList();

            if (request.Members?.Count > 0)
            {
                publicMembers = publicMembers.Where(m => request.Members.Contains(m.Name)).ToList();
            }

            if (publicMembers.Count == 0)
            {
                return RefactoringResult.Failed("No public members found to extract");
            }

            // Generate interface members
            var interfaceMembers = new List<MemberDeclarationSyntax>();

            foreach (var member in publicMembers)
            {
                switch (member)
                {
                    case IMethodSymbol method:
                        interfaceMembers.Add(GenerateInterfaceMethod(method));
                        break;
                    case IPropertySymbol property:
                        interfaceMembers.Add(GenerateInterfaceProperty(property));
                        break;
                }
            }

            // Create interface declaration
            var interfaceDecl = SyntaxFactory.InterfaceDeclaration(request.InterfaceName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithMembers(SyntaxFactory.List(interfaceMembers));

            // Get namespace
            var namespaceDecl = classNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDecl switch
            {
                NamespaceDeclarationSyntax ns => ns.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
                _ => "Unknown"
            };

            // Create interface file content
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            var interfaceRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(SyntaxFactory.List(usings))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                    SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                        .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDecl))));

            var interfaceFilePath = Path.Combine(
                Path.GetDirectoryName(document.FilePath) ?? "",
                $"{request.InterfaceName}.cs");

            // Update class to implement interface
            var baseList = classNode.BaseList ?? SyntaxFactory.BaseList();
            var interfaceType = SyntaxFactory.SimpleBaseType(
                SyntaxFactory.ParseTypeName(request.InterfaceName));
            baseList = SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType)
                    .AddRange(baseList.Types));

            var newClassNode = classNode.WithBaseList(baseList);
            var newRoot = root.ReplaceNode(classNode, newClassNode);

            var formattedClassRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
            var formattedInterfaceRoot = Formatter.Format(interfaceRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would create interface '{request.InterfaceName}' with {publicMembers.Count} members",
                    Preview =
                    [
                        new FileChange(document.FilePath ?? "", originalText, formattedClassRoot.ToFullString()),
                        new FileChange(interfaceFilePath, "", formattedInterfaceRoot.ToFullString())
                    ]
                };
            }

            // Write files
            await File.WriteAllTextAsync(interfaceFilePath, formattedInterfaceRoot.ToFullString(), ct);
            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedClassRoot.ToFullString(), ct);
            }

            return new RefactoringResult
            {
                Success = true,
                Message = $"Extracted interface '{request.InterfaceName}' with {publicMembers.Count} members",
                CreatedFiles = [interfaceFilePath],
                ModifiedFiles = document.FilePath != null ? [document.FilePath] : []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract interface");
            return RefactoringResult.Failed($"Failed to extract interface: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> SafeDeleteAsync(SafeDeleteRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Safe deleting {Symbol} in {Solution}",
            request.SymbolName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the symbol
            var symbol = await FindSymbolAsync(
                solution, request.SymbolName, request.ContainingType, null, ct);

            if (symbol is null)
            {
                return RefactoringResult.Failed($"Symbol '{request.SymbolName}' not found");
            }

            // Find all references
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
            var allLocations = references.SelectMany(r => r.Locations).ToList();

            if (allLocations.Count > 0)
            {
                var remainingRefs = allLocations.Take(10).Select(loc =>
                {
                    var lineSpan = loc.Location.GetLineSpan();
                    var lineText = "";
                    try
                    {
                        lineText = File.ReadLines(loc.Document.FilePath ?? "")
                            .Skip(lineSpan.StartLinePosition.Line)
                            .FirstOrDefault()?.Trim() ?? "";
                    }
                    catch { }

                    return new SymbolReference(
                        loc.Document.FilePath ?? "",
                        lineSpan.StartLinePosition.Line + 1,
                        lineText);
                }).ToList();

                return new RefactoringResult
                {
                    Success = false,
                    Message = $"Cannot delete '{request.SymbolName}': {allLocations.Count} references remain",
                    Error = $"Symbol has {allLocations.Count} remaining references",
                    RemainingReferences = remainingRefs
                };
            }

            // No references - safe to delete
            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (declaration is null)
            {
                return RefactoringResult.Failed("Could not find symbol declaration");
            }

            var syntaxNode = await declaration.GetSyntaxAsync(ct);
            var document = solution.GetDocument(declaration.SyntaxTree);
            if (document?.FilePath is null)
            {
                return RefactoringResult.Failed("Could not find document");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                var newRoot = root.RemoveNode(syntaxNode, SyntaxRemoveOptions.KeepNoTrivia);
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would delete '{request.SymbolName}'",
                    Preview = [new FileChange(document.FilePath, originalText, newRoot?.ToFullString() ?? "")]
                };
            }

            var updatedRoot = root.RemoveNode(syntaxNode, SyntaxRemoveOptions.KeepNoTrivia);
            if (updatedRoot != null)
            {
                var formatted = Formatter.Format(updatedRoot, document.Project.Solution.Workspace);
                await File.WriteAllTextAsync(document.FilePath, formatted.ToFullString(), ct);
            }

            return RefactoringResult.Succeeded(
                $"Deleted '{request.SymbolName}'",
                [document.FilePath]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to safely delete symbol");
            return RefactoringResult.Failed($"Failed to delete: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> AddPropertyAsync(AddPropertyRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding property {Property} to {Class} in {Solution}",
            request.PropertyName, request.ClassName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var classNode = await classDecl.GetSyntaxAsync(ct) as ClassDeclarationSyntax;
            if (classNode is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var document = solution.GetDocument(classDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Build property
            var accessors = new List<AccessorDeclarationSyntax>();
            if (request.HasGetter)
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }
            if (request.HasSetter)
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }

            var property = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.ParseTypeName(request.PropertyType),
                    SyntaxFactory.Identifier(request.PropertyName))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));

            if (request.InitialValue != null)
            {
                property = property.WithInitializer(
                    SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(request.InitialValue)))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }

            var newClassNode = classNode.AddMembers(property);
            var newRoot = root.ReplaceNode(classNode, newClassNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would add property '{request.PropertyName}' to '{request.ClassName}'",
                    Preview = [new FileChange(document.FilePath ?? "", originalText, formattedRoot.ToFullString())]
                };
            }

            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
            }

            return RefactoringResult.Succeeded(
                $"Added property '{request.PropertyName}' to '{request.ClassName}'",
                document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add property");
            return RefactoringResult.Failed($"Failed to add property: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> AddMethodAsync(AddMethodRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding method {Method} to {Class} in {Solution}",
            request.MethodName, request.ClassName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var classNode = await classDecl.GetSyntaxAsync(ct) as ClassDeclarationSyntax;
            if (classNode is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var document = solution.GetDocument(classDecl.SyntaxTree);
            if (document is null)
            {
                return RefactoringResult.Failed("Could not find document");
            }

            var root = await document.GetSyntaxRootAsync(ct);
            if (root is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Build modifiers
            var modifiers = new List<SyntaxToken>
            {
                SyntaxFactory.Token(request.AccessModifier switch
                {
                    "private" => SyntaxKind.PrivateKeyword,
                    "protected" => SyntaxKind.ProtectedKeyword,
                    "internal" => SyntaxKind.InternalKeyword,
                    _ => SyntaxKind.PublicKeyword
                })
            };

            if (request.IsAsync)
            {
                modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            // Build parameters
            var parameters = request.Parameters?.Select(p =>
            {
                var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                    .WithType(SyntaxFactory.ParseTypeName(p.Type));
                if (p.DefaultValue != null)
                {
                    param = param.WithDefault(
                        SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(p.DefaultValue)));
                }
                return param;
            }).ToList() ?? [];

            // Build body
            var bodyStatements = new List<StatementSyntax>();
            if (request.Body != null)
            {
                bodyStatements.Add(SyntaxFactory.ParseStatement(request.Body));
            }
            else
            {
                bodyStatements.Add(SyntaxFactory.ThrowStatement(
                    SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName("NotImplementedException"))
                        .WithArgumentList(SyntaxFactory.ArgumentList())));
            }

            var method = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName(request.ReturnType),
                    SyntaxFactory.Identifier(request.MethodName))
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
                .WithBody(SyntaxFactory.Block(bodyStatements));

            var newClassNode = classNode.AddMembers(method);
            var newRoot = root.ReplaceNode(classNode, newClassNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would add method '{request.MethodName}' to '{request.ClassName}'",
                    Preview = [new FileChange(document.FilePath ?? "", originalText, formattedRoot.ToFullString())]
                };
            }

            if (document.FilePath != null)
            {
                await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
            }

            return RefactoringResult.Succeeded(
                $"Added method '{request.MethodName}' to '{request.ClassName}'",
                document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add method");
            return RefactoringResult.Failed($"Failed to add method: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> MoveTypeToFileAsync(MoveTypeToFileRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Moving type {Type} to its own file in {Solution}",
            request.TypeName, request.SolutionPath);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the type
            var typeSymbol = await FindTypeAsync(solution, request.TypeName, ct);
            if (typeSymbol is null)
            {
                return RefactoringResult.Failed($"Type '{request.TypeName}' not found");
            }

            var typeDecl = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (typeDecl is null)
            {
                return RefactoringResult.Failed("Could not find type declaration");
            }

            var typeNode = await typeDecl.GetSyntaxAsync(ct) as BaseTypeDeclarationSyntax;
            if (typeNode is null)
            {
                return RefactoringResult.Failed("Could not parse type declaration");
            }

            var sourceDoc = solution.GetDocument(typeDecl.SyntaxTree);
            if (sourceDoc?.FilePath is null)
            {
                return RefactoringResult.Failed("Could not find source document");
            }

            var sourceRoot = await sourceDoc.GetSyntaxRootAsync(ct);
            if (sourceRoot is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Determine target file path
            var targetDir = request.TargetDirectory ?? Path.GetDirectoryName(sourceDoc.FilePath)!;
            var targetFileName = request.TargetFileName ?? $"{request.TypeName}.cs";
            var targetPath = Path.Combine(targetDir, targetFileName);

            // Check if already in correct file
            var sourceFileName = Path.GetFileNameWithoutExtension(sourceDoc.FilePath);
            if (sourceFileName.Equals(request.TypeName, StringComparison.OrdinalIgnoreCase))
            {
                return RefactoringResult.Succeeded(
                    $"Type '{request.TypeName}' is already in file '{sourceFileName}.cs'",
                    []);
            }

            // Check if target file already exists
            if (File.Exists(targetPath))
            {
                return RefactoringResult.Failed($"Target file already exists: {targetPath}");
            }

            // Count types in source file (classes, interfaces, records, structs, enums)
            var typesInFile = sourceRoot.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                .ToList();

            var isOnlyTypeInFile = typesInFile.Count == 1;

            if (isOnlyTypeInFile && request.UseGitMove)
            {
                // Simple case: just rename the file using git mv
                return await MoveFileWithGitAsync(sourceDoc.FilePath, targetPath, request.Preview, ct);
            }

            // Complex case: extract type to new file
            return await ExtractTypeToNewFileAsync(
                solution, sourceDoc, sourceRoot, typeNode, targetPath, request.Preview, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move type to file");
            return RefactoringResult.Failed($"Failed to move type: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> MoveFileWithGitAsync(
        string sourcePath,
        string targetPath,
        bool preview,
        CancellationToken ct)
    {
        _logger.LogDebug("Moving file with git: {Source} -> {Target}", sourcePath, targetPath);

        if (preview)
        {
            return new RefactoringResult
            {
                Success = true,
                Message = $"Preview: Would rename file using 'git mv {Path.GetFileName(sourcePath)} {Path.GetFileName(targetPath)}'",
                ModifiedFiles = [sourcePath],
                CreatedFiles = [targetPath]
            };
        }

        // Use git mv to preserve history
        var workingDir = Path.GetDirectoryName(sourcePath)!;
        var sourceFileName = Path.GetFileName(sourcePath);
        var targetFileName = Path.GetFileName(targetPath);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"mv \"{sourceFileName}\" \"{targetFileName}\"",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return RefactoringResult.Failed("Failed to start git process");
        }

        await process.WaitForExitAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git mv failed: {Error}", error);

            // Fall back to regular file move
            File.Move(sourcePath, targetPath);
            _logger.LogInformation("Fell back to File.Move");
        }

        _workspaceService.ClearCache();

        return new RefactoringResult
        {
            Success = true,
            Message = $"Renamed file '{Path.GetFileName(sourcePath)}' to '{Path.GetFileName(targetPath)}'",
            ModifiedFiles = [],
            CreatedFiles = [targetPath],
            DeletedFiles = [sourcePath]
        };
    }

    private async Task<RefactoringResult> ExtractTypeToNewFileAsync(
        Solution solution,
        Document sourceDoc,
        SyntaxNode sourceRoot,
        BaseTypeDeclarationSyntax typeNode,
        string targetPath,
        bool preview,
        CancellationToken ct)
    {
        _logger.LogDebug("Extracting type to new file: {Target}", targetPath);

        // Get namespace
        var namespaceDecl = typeNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var namespaceName = namespaceDecl switch
        {
            NamespaceDeclarationSyntax ns => ns.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
            _ => "Unknown"
        };

        // Collect using directives from source
        var usings = sourceRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        // Build new file content
        var newFileRoot = SyntaxFactory.CompilationUnit()
            .WithUsings(SyntaxFactory.List(usings))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeNode))));

        // Remove type from source file
        var newSourceRoot = sourceRoot.RemoveNode(typeNode, SyntaxRemoveOptions.KeepNoTrivia);
        if (newSourceRoot is null)
        {
            return RefactoringResult.Failed("Failed to remove type from source file");
        }

        var formattedNewFile = Formatter.Format(newFileRoot, solution.Workspace);
        var formattedSource = Formatter.Format(newSourceRoot, solution.Workspace);

        if (preview)
        {
            var originalText = (await sourceDoc.GetTextAsync(ct)).ToString();
            return new RefactoringResult
            {
                Success = true,
                Message = $"Preview: Would extract type to new file and update source",
                Preview =
                [
                    new FileChange(sourceDoc.FilePath!, originalText, formattedSource.ToFullString()),
                    new FileChange(targetPath, "", formattedNewFile.ToFullString())
                ],
                CreatedFiles = [targetPath],
                ModifiedFiles = [sourceDoc.FilePath!]
            };
        }

        // Write new file
        await File.WriteAllTextAsync(targetPath, formattedNewFile.ToFullString(), ct);

        // Update source file
        await File.WriteAllTextAsync(sourceDoc.FilePath!, formattedSource.ToFullString(), ct);

        _workspaceService.ClearCache();

        return new RefactoringResult
        {
            Success = true,
            Message = $"Extracted type '{typeNode.Identifier.Text}' to '{Path.GetFileName(targetPath)}'",
            CreatedFiles = [targetPath],
            ModifiedFiles = [sourceDoc.FilePath!]
        };
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> CreateTypeAsync(CreateTypeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating {Kind} {Type} in {Directory}",
            request.TypeKind, request.TypeName, request.TargetDirectory);

        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        if (!Directory.Exists(request.TargetDirectory))
        {
            return RefactoringResult.Failed($"Target directory not found: {request.TargetDirectory}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the project that contains this directory
            var project = FindProjectForDirectory(solution, request.TargetDirectory);
            if (project is null)
            {
                return RefactoringResult.Failed(
                    $"Could not find a project containing directory: {request.TargetDirectory}");
            }

            // Infer namespace from project and directory structure
            var targetNamespace = request.Namespace ?? InferNamespace(project, request.TargetDirectory);

            // Build the type file
            var targetPath = Path.Combine(request.TargetDirectory, $"{request.TypeName}.cs");

            if (File.Exists(targetPath))
            {
                return RefactoringResult.Failed($"File already exists: {targetPath}");
            }

            // Generate the type declaration
            var typeDeclaration = GenerateTypeDeclaration(request);
            if (typeDeclaration is null)
            {
                return RefactoringResult.Failed($"Unknown type kind: {request.TypeKind}");
            }

            // Build using directives
            var usings = new List<UsingDirectiveSyntax>();

            // Add standard usings based on context
            if (request.Interfaces?.Count > 0 || request.BaseClass != null)
            {
                // We might need usings for the base types - for now, assume they're in the same namespace
                // or the caller provides AdditionalUsings
            }

            if (request.AdditionalUsings != null)
            {
                foreach (var ns in request.AdditionalUsings)
                {
                    usings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns)));
                }
            }

            // Build the compilation unit
            var compilationUnit = SyntaxFactory.CompilationUnit()
                .WithUsings(SyntaxFactory.List(usings))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                    SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(targetNamespace))
                        .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeDeclaration))));

            var formattedRoot = Formatter.Format(compilationUnit, solution.Workspace);
            var fileContent = formattedRoot.ToFullString();

            if (request.Preview)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would create {request.TypeKind} '{request.TypeName}' in namespace '{targetNamespace}'",
                    Preview = [new FileChange(targetPath, "", fileContent)],
                    CreatedFiles = [targetPath]
                };
            }

            // Write the file
            await File.WriteAllTextAsync(targetPath, fileContent, ct);
            _logger.LogInformation("Created type file: {Path}", targetPath);

            _workspaceService.ClearCache();

            return new RefactoringResult
            {
                Success = true,
                Message = $"Created {request.TypeKind} '{request.TypeName}' in namespace '{targetNamespace}'",
                CreatedFiles = [targetPath]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create type");
            return RefactoringResult.Failed($"Failed to create type: {ex.Message}");
        }
    }

    private static Project? FindProjectForDirectory(Solution solution, string directory)
    {
        // Normalize directory path
        var targetDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);

        Project? bestMatch = null;
        var bestMatchLength = 0;

        foreach (var project in solution.Projects)
        {
            if (project.FilePath is null) continue;

            var projectDir = Path.GetDirectoryName(project.FilePath);
            if (projectDir is null) continue;

            projectDir = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar);

            // Check if target directory is under this project's directory
            if (targetDir.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
            {
                // Prefer the project with the longest matching path (most specific)
                if (projectDir.Length > bestMatchLength)
                {
                    bestMatch = project;
                    bestMatchLength = projectDir.Length;
                }
            }
        }

        return bestMatch;
    }

    private static string InferNamespace(Project project, string targetDirectory)
    {
        // Get the project's root namespace (default to project name if not set)
        var rootNamespace = project.DefaultNamespace ?? project.Name;

        // Get project directory
        var projectDir = Path.GetDirectoryName(project.FilePath);
        if (projectDir is null)
        {
            return rootNamespace;
        }

        projectDir = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar);
        var targetDir = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar);

        // If target is under project directory, append subdirectory path to namespace
        if (targetDir.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase) &&
            targetDir.Length > projectDir.Length)
        {
            var relativePath = targetDir[(projectDir.Length + 1)..];
            var subNamespace = relativePath.Replace(Path.DirectorySeparatorChar, '.');

            return $"{rootNamespace}.{subNamespace}";
        }

        return rootNamespace;
    }

    private static TypeDeclarationSyntax? GenerateTypeDeclaration(CreateTypeRequest request)
    {
        // Build modifiers
        var modifiers = new List<SyntaxToken>();

        var accessModifier = request.AccessModifier.ToLowerInvariant() switch
        {
            "internal" => SyntaxKind.InternalKeyword,
            "private" => SyntaxKind.PrivateKeyword,
            "protected" => SyntaxKind.ProtectedKeyword,
            _ => SyntaxKind.PublicKeyword
        };
        modifiers.Add(SyntaxFactory.Token(accessModifier));

        if (request.IsStatic)
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (request.IsAbstract)
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
        if (request.IsSealed)
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));

        // Build base list
        var baseTypes = new List<BaseTypeSyntax>();
        if (request.BaseClass != null)
        {
            baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(request.BaseClass)));
        }
        if (request.Interfaces != null)
        {
            foreach (var iface in request.Interfaces)
            {
                baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(iface)));
            }
        }
        var baseList = baseTypes.Count > 0
            ? SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes))
            : null;

        // Build documentation if provided
        SyntaxTriviaList leadingTrivia = default;
        if (request.DocumentationSummary != null)
        {
            var docComment = $"""
                /// <summary>
                /// {request.DocumentationSummary}
                /// </summary>

                """;
            leadingTrivia = SyntaxFactory.ParseLeadingTrivia(docComment);
        }

        // Generate the type based on kind
        TypeDeclarationSyntax? typeDecl = request.TypeKind.ToLowerInvariant() switch
        {
            "class" => SyntaxFactory.ClassDeclaration(request.TypeName)
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithBaseList(baseList)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)),

            "interface" => SyntaxFactory.InterfaceDeclaration(request.TypeName)
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithBaseList(baseList)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)),

            "record" when request.IsRecordStruct => SyntaxFactory.RecordDeclaration(
                    SyntaxKind.RecordStructDeclaration,
                    SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                    SyntaxFactory.Identifier(request.TypeName))
                .WithClassOrStructKeyword(SyntaxFactory.Token(SyntaxKind.StructKeyword))
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithBaseList(baseList)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)),

            "record" => SyntaxFactory.RecordDeclaration(
                    SyntaxKind.RecordDeclaration,
                    SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                    SyntaxFactory.Identifier(request.TypeName))
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithBaseList(baseList)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)),

            "struct" => SyntaxFactory.StructDeclaration(request.TypeName)
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithBaseList(baseList)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)),

            "enum" => null, // Enums are handled separately below

            _ => null
        };

        // Handle enum specially (it doesn't have a base list in the same way)
        if (request.TypeKind.Equals("enum", StringComparison.OrdinalIgnoreCase))
        {
            var enumDecl = SyntaxFactory.EnumDeclaration(request.TypeName)
                .WithModifiers(SyntaxFactory.TokenList(modifiers))
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

            if (leadingTrivia != default)
            {
                enumDecl = enumDecl.WithLeadingTrivia(leadingTrivia);
            }

            // EnumDeclaration is not a TypeDeclarationSyntax, so we need to cast
            // Actually, EnumDeclarationSyntax is a BaseTypeDeclarationSyntax, not TypeDeclarationSyntax
            // We need to handle this differently
            return null; // For now, skip enums - they need different handling
        }

        if (typeDecl != null && leadingTrivia != default)
        {
            typeDecl = typeDecl.WithLeadingTrivia(leadingTrivia);
        }

        return typeDecl;
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private async Task<ISymbol?> FindSymbolAsync(
        Solution solution,
        string symbolName,
        string? containingType,
        string? filePath,
        CancellationToken ct)
    {
        var candidates = new List<(ISymbol Symbol, string Location)>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            // If filePath specified, search only that file
            if (filePath != null)
            {
                var doc = project.Documents.FirstOrDefault(d =>
                    d.FilePath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true);
                if (doc is null) continue;

                var tree = await doc.GetSyntaxTreeAsync(ct);
                if (tree is null) continue;

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                // Find all type and member declarations
                var symbols = root.DescendantNodes()
                    .Where(n => n is BaseTypeDeclarationSyntax or  // class, interface, struct, record, enum
                               MethodDeclarationSyntax or
                               PropertyDeclarationSyntax or
                               FieldDeclarationSyntax)
                    .Select(n => semanticModel.GetDeclaredSymbol(n))
                    .Where(s => s?.Name == symbolName)
                    .ToList();

                if (containingType != null)
                {
                    // Support both simple name and full namespace-qualified name
                    symbols = symbols.Where(s =>
                    {
                        // For top-level types, ContainingType is null - they match if the type itself matches containingType
                        if (s?.ContainingType is null && s is INamedTypeSymbol namedType)
                        {
                            return namedType.Name == containingType ||
                                   namedType.ToDisplayString() == containingType;
                        }
                        // For members, check the containing type
                        return s?.ContainingType?.Name == containingType ||
                               s?.ContainingType?.ToDisplayString() == containingType;
                    }).ToList();
                }

                foreach (var s in symbols.Where(s => s != null))
                {
                    candidates.Add((s!, filePath));
                }
            }
            else
            {
                // Search all types in compilation
                var allTypes = GetAllTypes(compilation.GlobalNamespace);

                foreach (var type in allTypes)
                {
                    // If containingType specified, only look at members of that type
                    if (containingType != null)
                    {
                        // Support both simple name and full namespace-qualified name
                        if (type.Name == containingType || type.ToDisplayString() == containingType)
                        {
                            var member = type.GetMembers(symbolName).FirstOrDefault();
                            if (member != null)
                            {
                                var loc = member.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "";
                                candidates.Add((member, loc));
                            }
                        }
                        continue;
                    }

                    // No containingType - check if this type matches
                    if (type.Name == symbolName)
                    {
                        var loc = type.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "";
                        _logger.LogDebug("Found type match: {TypeName} ({Kind}) at {Path}",
                            type.ToDisplayString(), type.TypeKind, loc);
                        candidates.Add((type, loc));
                    }

                    // Also check members
                    var memberMatch = type.GetMembers(symbolName).FirstOrDefault();
                    if (memberMatch != null)
                    {
                        var loc = memberMatch.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "";
                        _logger.LogDebug("Found member match: {MemberName} ({Kind}) in {Type} at {Path}",
                            memberMatch.Name, memberMatch.Kind, type.Name, loc);
                        candidates.Add((memberMatch, loc));
                    }
                }
            }
        }

        // Deduplicate by symbol display string
        candidates = candidates
            .GroupBy(c => c.Symbol.ToDisplayString())
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0].Symbol;
        }

        // Multiple candidates - if no containingType specified, prefer types over members
        if (containingType == null)
        {
            var typeMatches = candidates.Where(c => c.Symbol is INamedTypeSymbol).ToList();
            if (typeMatches.Count == 1)
            {
                _logger.LogDebug("Multiple symbols named {Name} found, preferring type over member", symbolName);
                return typeMatches[0].Symbol;
            }
        }

        // Multiple candidates found - log them and throw an error requiring disambiguation
        _logger.LogWarning("Multiple symbols named {Name} found:", symbolName);
        foreach (var (sym, loc) in candidates)
        {
            _logger.LogWarning("  - {Kind} {FullName} in {File}",
                sym.Kind, sym.ToDisplayString(), Path.GetFileName(loc));
        }

        var candidateList = string.Join("\n", candidates.Select(c =>
            $"  - {c.Symbol.Kind}: {c.Symbol.ToDisplayString()} (in {Path.GetFileName(c.Location)})"));

        throw new InvalidOperationException(
            $"Multiple symbols named '{symbolName}' found. Please specify 'containingType' or 'filePath' to disambiguate:\n{candidateList}");
    }

    private async Task<INamedTypeSymbol?> FindTypeAsync(Solution solution, string typeName, CancellationToken ct)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            var allTypes = GetAllTypes(compilation.GlobalNamespace);
            var type = allTypes.FirstOrDefault(t =>
                t.Name == typeName || t.ToDisplayString().EndsWith("." + typeName));

            if (type != null) return type;
        }

        return null;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers())
            {
                yield return nested;
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNs))
            {
                yield return type;
            }
        }
    }

    private static List<Document> GetChangedDocuments(Solution oldSolution, Solution newSolution)
    {
        var changedDocs = new List<Document>();

        foreach (var projectId in newSolution.ProjectIds)
        {
            var newProject = newSolution.GetProject(projectId);
            var oldProject = oldSolution.GetProject(projectId);
            if (newProject is null || oldProject is null) continue;

            foreach (var docId in newProject.DocumentIds)
            {
                var newDoc = newProject.GetDocument(docId);
                var oldDoc = oldProject.GetDocument(docId);
                if (newDoc is null) continue;

                if (oldDoc is null || !newDoc.GetTextAsync().Result.ContentEquals(oldDoc.GetTextAsync().Result))
                {
                    changedDocs.Add(newDoc);
                }
            }
        }

        return changedDocs;
    }

    private static MethodDeclarationSyntax GenerateMethodStub(
        IMethodSymbol method, bool explicitImpl, INamedTypeSymbol interfaceSymbol)
    {
        var parameters = method.Parameters.Select(p =>
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.Type.ToDisplayString())));

        var returnType = SyntaxFactory.ParseTypeName(method.ReturnType.ToDisplayString());

        var body = method.ReturnType.SpecialType == SpecialType.System_Void
            ? SyntaxFactory.Block(SyntaxFactory.ThrowStatement(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("NotImplementedException"))
                    .WithArgumentList(SyntaxFactory.ArgumentList())))
            : SyntaxFactory.Block(SyntaxFactory.ThrowStatement(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("NotImplementedException"))
                    .WithArgumentList(SyntaxFactory.ArgumentList())));

        var methodDecl = SyntaxFactory.MethodDeclaration(returnType, method.Name)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithBody(body);

        if (explicitImpl)
        {
            methodDecl = methodDecl.WithExplicitInterfaceSpecifier(
                SyntaxFactory.ExplicitInterfaceSpecifier(
                    SyntaxFactory.ParseName(interfaceSymbol.Name)));
        }
        else
        {
            methodDecl = methodDecl.WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        return methodDecl;
    }

    private static PropertyDeclarationSyntax GeneratePropertyStub(
        IPropertySymbol property, bool explicitImpl, INamedTypeSymbol interfaceSymbol)
    {
        var accessors = new List<AccessorDeclarationSyntax>();

        if (property.GetMethod != null)
        {
            accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        if (property.SetMethod != null)
        {
            accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        var propDecl = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.ParseTypeName(property.Type.ToDisplayString()),
                property.Name)
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));

        if (explicitImpl)
        {
            propDecl = propDecl.WithExplicitInterfaceSpecifier(
                SyntaxFactory.ExplicitInterfaceSpecifier(
                    SyntaxFactory.ParseName(interfaceSymbol.Name)));
        }
        else
        {
            propDecl = propDecl.WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        return propDecl;
    }

    private static MethodDeclarationSyntax GenerateInterfaceMethod(IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(p =>
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.Type.ToDisplayString())));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(method.ReturnType.ToDisplayString()),
                method.Name)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static PropertyDeclarationSyntax GenerateInterfaceProperty(IPropertySymbol property)
    {
        var accessors = new List<AccessorDeclarationSyntax>();

        if (property.GetMethod != null)
        {
            accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        if (property.SetMethod != null)
        {
            accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        return SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.ParseTypeName(property.Type.ToDisplayString()),
                property.Name)
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Remove underscore prefix if present
        if (name.StartsWith('_'))
            name = name[1..];

        if (name.Length == 0) return name;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Validates a refactoring by running a build and checking for residuals.
    /// </summary>
    private async Task<ValidationResult> ValidateRefactoringAsync(
        string solutionPath,
        string? oldSymbolName,
        CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? ".";
        var buildSucceeded = false;
        string? buildOutput = null;
        List<string>? residuals = null;

        try
        {
            // Run dotnet build
            _logger.LogDebug("Running post-refactor build validation for {Solution}", solutionPath);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --no-restore -v q",
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is not null)
            {
                var stdout = await process.StandardOutput.ReadToEndAsync(ct);
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                buildSucceeded = process.ExitCode == 0;
                buildOutput = string.IsNullOrEmpty(stderr) ? stdout : stderr;

                // Truncate output if too long
                if (buildOutput.Length > 2000)
                {
                    buildOutput = buildOutput[..2000] + "\n... (truncated)";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Build validation failed");
            buildOutput = $"Build check failed: {ex.Message}";
        }

        // Check for residuals (grep for old symbol name in source files)
        if (!string.IsNullOrEmpty(oldSymbolName))
        {
            try
            {
                residuals = [];
                var sourceFiles = Directory.GetFiles(solutionDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("bin") && !f.Contains("obj"));

                foreach (var file in sourceFiles)
                {
                    var content = await File.ReadAllTextAsync(file, ct);
                    if (content.Contains(oldSymbolName, StringComparison.Ordinal))
                    {
                        // Find line numbers
                        var lines = content.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains(oldSymbolName, StringComparison.Ordinal))
                            {
                                var relativePath = Path.GetRelativePath(solutionDir, file);
                                residuals.Add($"{relativePath}:{i + 1}: {lines[i].Trim()}");
                            }
                        }
                    }
                }

                if (residuals.Count == 0)
                {
                    residuals = null;
                }
                else
                {
                    _logger.LogWarning("Found {Count} residual occurrences of '{Symbol}'",
                        residuals.Count, oldSymbolName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Residual check failed");
            }
        }

        return new ValidationResult
        {
            BuildSucceeded = buildSucceeded,
            BuildOutput = buildOutput,
            Residuals = residuals
        };
    }
}
