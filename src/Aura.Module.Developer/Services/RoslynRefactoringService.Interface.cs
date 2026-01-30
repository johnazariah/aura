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

namespace Aura.Module.Developer.Services;

public sealed partial class RoslynRefactoringService
{
    /// <inheritdoc/>
    public async Task<RefactoringResult> ImplementInterfaceAsync(ImplementInterfaceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Implementing {Interface} on {Class} in {Solution}", request.InterfaceName, request.ClassName, request.SolutionPath);
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
                if (existingMembers.Contains(member.Name))
                    continue;
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
                return RefactoringResult.Succeeded($"Class '{request.ClassName}' already implements all members of '{request.InterfaceName}'", []);
            }

            // Add interface to base list if not present
            var baseList = classNode.BaseList ?? SyntaxFactory.BaseList();
            var hasInterface = baseList.Types.Any(t => t.Type.ToString().Contains(request.InterfaceName));
            if (!hasInterface)
            {
                var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(request.InterfaceName));
                baseList = baseList.AddTypes(interfaceType);
            }

            var newClassNode = classNode.WithBaseList(baseList).AddMembers(membersToAdd.ToArray());
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
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
                }

                _workspaceService.ClearCache();
            }

            return RefactoringResult.Succeeded($"Implemented {membersToAdd.Count} members of '{request.InterfaceName}' on '{request.ClassName}'", document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to implement interface");
            return RefactoringResult.Failed($"Failed to implement interface: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> ExtractInterfaceAsync(ExtractInterfaceRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting interface {Interface} from {Class} in {Solution}", request.InterfaceName, request.ClassName, request.SolutionPath);
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
            var publicMembers = classSymbol.GetMembers().Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic && m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol).ToList();
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
            var interfaceDecl = SyntaxFactory.InterfaceDeclaration(request.InterfaceName).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))).WithMembers(SyntaxFactory.List(interfaceMembers));
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
            var interfaceRoot = SyntaxFactory.CompilationUnit().WithUsings(SyntaxFactory.List(usings)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDecl))));
            var interfaceFilePath = Path.Combine(Path.GetDirectoryName(document.FilePath) ?? "", $"{request.InterfaceName}.cs");
            // Update class to implement interface
            var baseList = classNode.BaseList ?? SyntaxFactory.BaseList();
            var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(request.InterfaceName));
            baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType).AddRange(baseList.Types));
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
                    Preview = [new FileChange(document.FilePath ?? "", originalText, formattedClassRoot.ToFullString()), new FileChange(interfaceFilePath, "", formattedInterfaceRoot.ToFullString())]
                };
            }

            // Write files
            using (await AcquireFileLockAsync(interfaceFilePath, ct))
            {
                await File.WriteAllTextAsync(interfaceFilePath, formattedInterfaceRoot.ToFullString(), ct);
            }

            if (document.FilePath != null)
            {
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formattedClassRoot.ToFullString(), ct);
                }
            }

            _workspaceService.ClearCache();
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

    private static InterfaceDeclarationSyntax CreateInterfaceDeclaration(string typeName, List<SyntaxToken> modifiers, BaseListSyntax? baseList, TypeParameterListSyntax? typeParameterList = null, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default)
    {
        var interfaceDecl = SyntaxFactory.InterfaceDeclaration(typeName).WithModifiers(SyntaxFactory.TokenList(modifiers)).WithBaseList(baseList).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)).WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));
        if (typeParameterList != null)
        {
            interfaceDecl = interfaceDecl.WithTypeParameterList(typeParameterList);
            if (constraintClauses.Count > 0)
            {
                interfaceDecl = interfaceDecl.WithConstraintClauses(constraintClauses);
            }
        }

        return interfaceDecl;
    }
}
