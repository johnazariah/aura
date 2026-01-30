// <copyright file="RoslynRefactoringService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Collections.Concurrent;
using System.Diagnostics;
using Aura.Module.Developer.Services.Testing;
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
public sealed partial class RoslynRefactoringService : IRoslynRefactoringService
{
    // Import constants from shared class
    private const string FrameworkXUnit = TestFrameworkConstants.FrameworkXUnit;
    private const string FrameworkNUnit = TestFrameworkConstants.FrameworkNUnit;
    private const string FrameworkMsTest = TestFrameworkConstants.FrameworkMsTest;
    private const string AttrFact = TestFrameworkConstants.AttrFact;
    private const string AttrTheory = TestFrameworkConstants.AttrTheory;
    private const string AttrTest = TestFrameworkConstants.AttrTest;
    private const string AttrTestCase = TestFrameworkConstants.AttrTestCase;
    private const string AttrTestMethod = TestFrameworkConstants.AttrTestMethod;
    private const string AttrDataTestMethod = TestFrameworkConstants.AttrDataTestMethod;

    private readonly IRoslynWorkspaceService _workspaceService;
    private readonly ILogger<RoslynRefactoringService> _logger;

    /// <summary>
    /// Per-file locks to prevent concurrent writes to the same file.
    /// Uses normalized absolute paths as keys.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    public RoslynRefactoringService(
        IRoslynWorkspaceService workspaceService,
        ILogger<RoslynRefactoringService> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a lock for the specified file path.
    /// </summary>
    private static SemaphoreSlim GetFileLock(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return FileLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Acquires exclusive access to a file for writing.
    /// </summary>
    private static async Task<IDisposable> AcquireFileLockAsync(string filePath, CancellationToken ct)
    {
        var semaphore = GetFileLock(filePath);
        await semaphore.WaitAsync(ct);
        return new FileLockReleaser(semaphore);
    }

    private sealed class FileLockReleaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
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
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
                }
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
                        using (await AcquireFileLockAsync(callDoc.FilePath, ct))
                        {
                            await File.WriteAllTextAsync(callDoc.FilePath, newCallRoot.ToFullString(), ct);
                        }
                        modifiedFiles.Add(callDoc.FilePath);
                    }
                }
            }

            _workspaceService.ClearCache();

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
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formatted.ToFullString(), ct);
                }
                _workspaceService.ClearCache();
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

    private static SyntaxTokenList BuildModifiers(string accessModifier, bool isStatic, bool isReadonly, bool isRequired = false)
    {
        var tokens = new List<SyntaxToken>();

        // Parse access modifier (can be compound like "private protected")
        var accessParts = accessModifier.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in accessParts)
        {
            var kind = part switch
            {
                "public" => SyntaxKind.PublicKeyword,
                "private" => SyntaxKind.PrivateKeyword,
                "protected" => SyntaxKind.ProtectedKeyword,
                "internal" => SyntaxKind.InternalKeyword,
                _ => SyntaxKind.None
            };
            if (kind != SyntaxKind.None)
            {
                tokens.Add(SyntaxFactory.Token(kind));
            }
        }

        if (isStatic)
        {
            tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        }

        if (isReadonly)
        {
            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
        }

        if (isRequired)
        {
            tokens.Add(SyntaxFactory.Token(SyntaxKind.RequiredKeyword));
        }

        return SyntaxFactory.TokenList(tokens);
    }

    private static SyntaxList<AttributeListSyntax> BuildAttributeListSyntax(IReadOnlyList<AttributeInfo> attributes)
    {
        var attributeLists = new List<AttributeListSyntax>();
        foreach (var attr in attributes)
        {
            AttributeSyntax attributeSyntax;
            if (attr.Arguments != null && attr.Arguments.Count > 0)
            {
                var args = attr.Arguments.Select(a =>
                    SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(a)));
                attributeSyntax = SyntaxFactory.Attribute(
                    SyntaxFactory.IdentifierName(attr.Name),
                    SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(args)));
            }
            else
            {
                attributeSyntax = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attr.Name));
            }
            attributeLists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attributeSyntax)));
        }
        return SyntaxFactory.List(attributeLists);
    }

    private static (TypeParameterListSyntax, SyntaxList<TypeParameterConstraintClauseSyntax>) BuildTypeParameterSyntax(
        IReadOnlyList<TypeParameterInfo> typeParameters)
    {
        var typeParams = typeParameters.Select(tp =>
            SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(tp.Name)));
        var typeParamList = SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParams));

        var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();
        foreach (var tp in typeParameters)
        {
            if (tp.Constraints != null && tp.Constraints.Count > 0)
            {
                var constraints = tp.Constraints.Select<string, TypeParameterConstraintSyntax>(c =>
                {
                    return c.ToLowerInvariant() switch
                    {
                        "class" => SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint),
                        "struct" => SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
                        "notnull" => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("notnull")),
                        "unmanaged" => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("unmanaged")),
                        "new()" => SyntaxFactory.ConstructorConstraint(),
                        "default" => SyntaxFactory.DefaultConstraint(),
                        _ => SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(c))
                    };
                });

                var clause = SyntaxFactory.TypeParameterConstraintClause(
                    SyntaxFactory.IdentifierName(tp.Name),
                    SyntaxFactory.SeparatedList(constraints));
                constraintClauses.Add(clause);
            }
        }

        return (typeParamList, SyntaxFactory.List(constraintClauses));
    }

    private static ClassDeclarationSyntax CreateClassDeclaration(
        string typeName,
        List<SyntaxToken> modifiers,
        BaseListSyntax? baseList,
        ParameterListSyntax? primaryConstructorParams,
        TypeParameterListSyntax? typeParameterList = null,
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default)
    {
        var classDecl = SyntaxFactory.ClassDeclaration(typeName)
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithBaseList(baseList)
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        // C# 12: Primary constructor syntax
        if (primaryConstructorParams != null)
        {
            classDecl = classDecl.WithParameterList(primaryConstructorParams);
        }

        // Generic type parameters
        if (typeParameterList != null)
        {
            classDecl = classDecl.WithTypeParameterList(typeParameterList);
            if (constraintClauses.Count > 0)
            {
                classDecl = classDecl.WithConstraintClauses(constraintClauses);
            }
        }

        return classDecl;
    }

    private static StructDeclarationSyntax CreateStructDeclaration(
        string typeName,
        List<SyntaxToken> modifiers,
        BaseListSyntax? baseList,
        TypeParameterListSyntax? typeParameterList = null,
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default)
    {
        var structDecl = SyntaxFactory.StructDeclaration(typeName)
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithBaseList(baseList)
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        if (typeParameterList != null)
        {
            structDecl = structDecl.WithTypeParameterList(typeParameterList);
            if (constraintClauses.Count > 0)
            {
                structDecl = structDecl.WithConstraintClauses(constraintClauses);
            }
        }

        return structDecl;
    }

    private static RecordDeclarationSyntax CreateRecordDeclaration(
        string typeName,
        List<SyntaxToken> modifiers,
        BaseListSyntax? baseList,
        ParameterListSyntax? primaryConstructorParams,
        bool isStruct,
        TypeParameterListSyntax? typeParameterList = null,
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default)
    {
        var recordKind = isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration;

        var recordDecl = SyntaxFactory.RecordDeclaration(
                recordKind,
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                SyntaxFactory.Identifier(typeName))
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithBaseList(baseList);

        if (isStruct)
        {
            recordDecl = recordDecl.WithClassOrStructKeyword(SyntaxFactory.Token(SyntaxKind.StructKeyword));
        }

        // Generic type parameters
        if (typeParameterList != null)
        {
            recordDecl = recordDecl.WithTypeParameterList(typeParameterList);
            if (constraintClauses.Count > 0)
            {
                recordDecl = recordDecl.WithConstraintClauses(constraintClauses);
            }
        }

        // C# 9: Positional record syntax - record Person(string Name, int Age);
        if (primaryConstructorParams != null)
        {
            // Use semicolon terminator for positional records (no body needed unless members added)
            recordDecl = recordDecl
                .WithParameterList(primaryConstructorParams)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
        }
        else
        {
            recordDecl = recordDecl
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
        }

        return recordDecl;
    }

    /// <summary>
    /// Creates the appropriate test attribute syntax for the given framework or attribute name.
    /// </summary>
    private static AttributeSyntax CreateTestAttribute(string frameworkOrAttribute)
    {
        var attributeName = TestFrameworkConstants.GetTestAttributeName(frameworkOrAttribute);
        return SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));
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

        // Build primary constructor parameter list if provided
        ParameterListSyntax? primaryConstructorParams = null;
        if (request.PrimaryConstructorParameters?.Count > 0)
        {
            var parameters = request.PrimaryConstructorParameters.Select(p =>
            {
                var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                    .WithType(SyntaxFactory.ParseTypeName(p.Type));
                if (p.DefaultValue != null)
                {
                    param = param.WithDefault(
                        SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(p.DefaultValue)));
                }
                return param;
            });
            primaryConstructorParams = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters));
        }

        // Build generic type parameter list if provided
        TypeParameterListSyntax? typeParameterList = null;
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default;
        if (request.TypeParameters?.Count > 0)
        {
            (typeParameterList, constraintClauses) = BuildTypeParameterSyntax(request.TypeParameters);
        }

        // Generate the type based on kind
        TypeDeclarationSyntax? typeDecl = request.TypeKind.ToLowerInvariant() switch
        {
            "class" => CreateClassDeclaration(request.TypeName, modifiers, baseList, primaryConstructorParams, typeParameterList, constraintClauses),

            "interface" => CreateInterfaceDeclaration(request.TypeName, modifiers, baseList, typeParameterList, constraintClauses),

            "record" when request.IsRecordStruct => CreateRecordDeclaration(
                request.TypeName, modifiers, baseList, primaryConstructorParams, isStruct: true, typeParameterList, constraintClauses),

            "record" => CreateRecordDeclaration(
                request.TypeName, modifiers, baseList, primaryConstructorParams, isStruct: false, typeParameterList, constraintClauses),

            "struct" => CreateStructDeclaration(request.TypeName, modifiers, baseList, typeParameterList, constraintClauses),

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

    private static string? GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
            EventDeclarationSyntax e => e.Identifier.Text,
            IndexerDeclarationSyntax => "this",
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            DestructorDeclarationSyntax d => "~" + d.Identifier.Text,
            OperatorDeclarationSyntax o => "operator" + o.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax co => co.ImplicitOrExplicitKeyword.Text + "operator",
            BaseTypeDeclarationSyntax t => t.Identifier.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            _ => null
        };
    }
}
