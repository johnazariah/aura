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
    public async Task<RefactoringResult> GenerateConstructorAsync(GenerateConstructorRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating constructor for {Class} in {Solution}", request.ClassName, request.SolutionPath);
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
            var members = classSymbol.GetMembers().Where(m => m is IFieldSymbol { IsReadOnly: true, IsStatic: false } or IPropertySymbol { IsReadOnly: true, IsStatic: false }).ToList();
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
                parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName)).WithType(SyntaxFactory.ParseTypeName(typeName)));
                assignments.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(member.Name), SyntaxFactory.IdentifierName(paramName))));
            }

            var constructor = SyntaxFactory.ConstructorDeclaration(classNode.Identifier).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword))).WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters))).WithBody(SyntaxFactory.Block(assignments));
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
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
                }

                _workspaceService.ClearCache();
            }

            return RefactoringResult.Succeeded($"Generated constructor with {members.Count} parameters for '{request.ClassName}'", document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate constructor");
            return RefactoringResult.Failed($"Failed to generate constructor: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> AddPropertyAsync(AddPropertyRequest request, CancellationToken ct = default)
    {
        var memberKind = request.IsField ? "field" : "property";
        _logger.LogInformation("Adding {Kind} {Name} to {Class} in {Solution}", memberKind, request.PropertyName, request.ClassName, request.SolutionPath);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            // Clear cache to ensure fresh view of the codebase (external changes may have occurred)
            _workspaceService.InvalidateCache(request.SolutionPath);

            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            var typeSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (typeSymbol is null)
            {
                return RefactoringResult.Failed($"Type '{request.ClassName}' not found");
            }

            // Check if property/field already exists (idempotency)
            var existingMember = typeSymbol.GetMembers(request.PropertyName).FirstOrDefault();
            if (existingMember != null)
            {
                _logger.LogInformation("{Kind} {Name} already exists in {Class}, skipping", memberKind, request.PropertyName, request.ClassName);
                return RefactoringResult.Succeeded($"{memberKind} '{request.PropertyName}' already exists in '{request.ClassName}'");
            }

            var typeDecl = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (typeDecl is null)
            {
                return RefactoringResult.Failed("Could not find type declaration");
            }

            var typeSyntax = await typeDecl.GetSyntaxAsync(ct);
            TypeDeclarationSyntax? typeNode = typeSyntax switch
            {
                ClassDeclarationSyntax classDecl => classDecl,
                RecordDeclarationSyntax recordDecl => recordDecl,
                StructDeclarationSyntax structDecl => structDecl,
                _ => null
            };
            if (typeNode is null)
            {
                return RefactoringResult.Failed($"Unsupported type declaration: {typeSyntax?.GetType().Name ?? "null"}");
            }

            var document = solution.GetDocument(typeDecl.SyntaxTree);
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
            var modifiers = BuildModifiers(request.AccessModifier, request.IsStatic, request.IsReadonly, request.IsRequired);
            MemberDeclarationSyntax member;
            if (request.IsField)
            {
                // Generate field
                var variable = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(request.PropertyName));
                if (request.InitialValue != null)
                {
                    variable = variable.WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(request.InitialValue)));
                }

                member = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(request.PropertyType)).WithVariables(SyntaxFactory.SingletonSeparatedList(variable))).WithModifiers(modifiers).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                // Generate property
                var accessors = new List<AccessorDeclarationSyntax>();
                if (request.HasGetter)
                {
                    accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }

                if (request.HasInit)
                {
                    // Use init accessor (C# 9+)
                    accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }
                else if (request.HasSetter)
                {
                    accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }

                var property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(request.PropertyType), SyntaxFactory.Identifier(request.PropertyName)).WithModifiers(modifiers).WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
                if (request.InitialValue != null)
                {
                    property = property.WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(request.InitialValue))).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                }

                // Add attributes if provided
                if (request.Attributes?.Count > 0)
                {
                    property = property.WithAttributeLists(BuildAttributeListSyntax(request.Attributes));
                }

                // Add XML documentation if provided
                if (!string.IsNullOrWhiteSpace(request.Documentation))
                {
                    var docComment = $"""
                        /// <summary>
                        /// {request.Documentation}
                        /// </summary>

                        """;
                    var leadingTrivia = SyntaxFactory.ParseLeadingTrivia(docComment);
                    property = property.WithLeadingTrivia(leadingTrivia);
                }

                member = property;
            }

            var newTypeNode = typeNode.AddMembers(member);
            var newRoot = root.ReplaceNode(typeNode, newTypeNode);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
            if (request.Preview)
            {
                var originalText = (await document.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would add {memberKind} '{request.PropertyName}' to '{request.ClassName}'",
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

            return RefactoringResult.Succeeded($"Added {memberKind} '{request.PropertyName}' to '{request.ClassName}'", document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {Kind}", memberKind);
            return RefactoringResult.Failed($"Failed to add {memberKind}: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> AddMethodAsync(AddMethodRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding method {Method} to {Class} in {Solution}", request.MethodName, request.ClassName, request.SolutionPath);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            // Clear cache to ensure fresh view of the codebase (external changes may have occurred)
            _workspaceService.InvalidateCache(request.SolutionPath);

            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            var classSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (classSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            // Check if method with same signature already exists (idempotency)
            var requestParamTypes = request.Parameters?.Select(p => p.Type).ToList() ?? [];
            var existingMethod = classSymbol.GetMembers(request.MethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                {
                    // Match by parameter count first
                    if (m.Parameters.Length != requestParamTypes.Count)
                        return false;

                    // Match each parameter type
                    for (int i = 0; i < m.Parameters.Length; i++)
                    {
                        var existingType = m.Parameters[i].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        var requestType = requestParamTypes[i];

                        // Normalize array syntax (int[] vs System.Int32[])
                        if (!TypesMatch(existingType, requestType))
                            return false;
                    }

                    return true;
                });
            if (existingMethod != null)
            {
                var signature = requestParamTypes.Count > 0
                    ? $"{request.MethodName}({string.Join(", ", requestParamTypes)})"
                    : $"{request.MethodName}()";
                _logger.LogInformation("Method {Method} already exists in {Class}, skipping", signature, request.ClassName);
                return RefactoringResult.Succeeded($"Method '{signature}' already exists in '{request.ClassName}'");
            }

            var classDecl = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (classDecl is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var typeNode = await classDecl.GetSyntaxAsync(ct) as TypeDeclarationSyntax;
            if (typeNode is null)
            {
                return RefactoringResult.Failed("Could not parse type declaration");
            }

            // Check if this is an interface
            var isInterface = typeNode is InterfaceDeclarationSyntax;

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

            // Determine test attribute: use caller-specified, or auto-detect from class
            // (not applicable for interfaces)
            string? testFramework = null;
            if (!isInterface)
            {
                if (!string.IsNullOrEmpty(request.TestAttribute))
                {
                    // Caller specified the attribute directly (e.g., "Fact", "Test", "TestMethod")
                    testFramework = TestFrameworkConstants.InferFrameworkFromAttribute(request.TestAttribute);
                    if (testFramework == FrameworkXUnit && !request.TestAttribute.Equals("fact", StringComparison.OrdinalIgnoreCase) && !request.TestAttribute.Equals("theory", StringComparison.OrdinalIgnoreCase))
                    {
                        // Unknown attribute, use as-is
                        testFramework = request.TestAttribute;
                    }
                }
                else if (typeNode is ClassDeclarationSyntax classNode)
                {
                    // Auto-detect if this is a test class
                    testFramework = TestFrameworkConstants.DetectFrameworkFromAttributes(classNode);
                }
            }

            // Build modifiers (interfaces don't use explicit modifiers)
            var modifiers = new List<SyntaxToken>();
            if (!isInterface)
            {
                modifiers.Add(SyntaxFactory.Token(request.AccessModifier switch
                {
                    "private" => SyntaxKind.PrivateKeyword,
                    "protected" => SyntaxKind.ProtectedKeyword,
                    "internal" => SyntaxKind.InternalKeyword,
                    _ => SyntaxKind.PublicKeyword
                }));
                if (request.IsStatic)
                {
                    modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                }

                // Add method modifiers (virtual, override, abstract, sealed, new)
                if (!string.IsNullOrEmpty(request.MethodModifier))
                {
                    var methodModKind = request.MethodModifier.ToLowerInvariant() switch
                    {
                        "virtual" => SyntaxKind.VirtualKeyword,
                        "override" => SyntaxKind.OverrideKeyword,
                        "abstract" => SyntaxKind.AbstractKeyword,
                        "sealed" => SyntaxKind.SealedKeyword,
                        "new" => SyntaxKind.NewKeyword,
                        _ => SyntaxKind.None
                    };
                    if (methodModKind != SyntaxKind.None)
                    {
                        modifiers.Add(SyntaxFactory.Token(methodModKind));
                    }
                }

                if (request.IsAsync)
                {
                    modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
                }
            }

            // Build parameters
            var parameters = request.Parameters?.Select((p, index) =>
            {
                var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name)).WithType(SyntaxFactory.ParseTypeName(p.Type));
                // Add 'this' modifier to first parameter for extension methods
                if (request.IsExtension && index == 0)
                {
                    param = param.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword)));
                }

                if (p.DefaultValue != null)
                {
                    param = param.WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(p.DefaultValue)));
                }

                return param;
            }).ToList() ?? [];
            // Check if this is an abstract method (no body)
            var isAbstract = request.MethodModifier?.Equals("abstract", StringComparison.OrdinalIgnoreCase) == true;
            // Build body (unless abstract)
            BlockSyntax? methodBody = null;
            // Interfaces and abstract methods have no body
            if (!isAbstract && !isInterface)
            {
                if (!string.IsNullOrWhiteSpace(request.Body))
                {
                    // Try to parse as a block first (e.g., "{ statement1; statement2; }")
                    // If not wrapped in braces, parse as individual statements
                    var bodyText = request.Body.Trim();
                    if (bodyText.StartsWith('{') && bodyText.EndsWith('}'))
                    {
                        // Parse entire block
                        methodBody = (BlockSyntax)SyntaxFactory.ParseStatement(bodyText);
                    }
                    else
                    {
                        // Parse as individual statements - split by semicolons and newlines
                        var statements = new List<StatementSyntax>();
                        // Try parsing the whole thing as multiple statements
                        var parsed = SyntaxFactory.ParseStatement("{ " + bodyText + " }");
                        if (parsed is BlockSyntax block && !block.ContainsDiagnostics)
                        {
                            statements.AddRange(block.Statements);
                        }
                        else
                        {
                            // Fallback: parse as single statement
                            statements.Add(SyntaxFactory.ParseStatement(bodyText + (bodyText.EndsWith(';') ? "" : ";")));
                        }

                        methodBody = SyntaxFactory.Block(statements);
                    }
                }
                else
                {
                    methodBody = SyntaxFactory.Block(SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("NotImplementedException")).WithArgumentList(SyntaxFactory.ArgumentList())));
                }
            }

            var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(request.ReturnType), SyntaxFactory.Identifier(request.MethodName)).WithModifiers(SyntaxFactory.TokenList(modifiers)).WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)));
            // Add generic type parameters if provided
            if (request.TypeParameters?.Count > 0)
            {
                var (typeParamList, constraintClauses) = BuildTypeParameterSyntax(request.TypeParameters);
                method = method.WithTypeParameterList(typeParamList);
                if (constraintClauses.Count > 0)
                {
                    method = method.WithConstraintClauses(constraintClauses);
                }
            }

            // Abstract methods and interface methods have no body, just a semicolon
            if (isAbstract || isInterface)
            {
                method = method.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                method = method.WithBody(methodBody);
            }

            // Build attribute lists (combine test framework attribute with custom attributes)
            var allAttributeLists = new List<AttributeListSyntax>();
            // Add test attribute if this is a test class
            if (testFramework != null)
            {
                var testAttribute = CreateTestAttribute(testFramework);
                allAttributeLists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testAttribute)));
                _logger.LogDebug("Added {Framework} test attribute to method {Method}", testFramework, request.MethodName);
            }

            // Add custom attributes if provided
            if (request.Attributes?.Count > 0)
            {
                allAttributeLists.AddRange(BuildAttributeListSyntax(request.Attributes));
            }

            if (allAttributeLists.Count > 0)
            {
                method = method.WithAttributeLists(SyntaxFactory.List(allAttributeLists));
            }

            // Add XML documentation if provided
            if (!string.IsNullOrWhiteSpace(request.Documentation))
            {
                var docComment = $"""
                    /// <summary>
                    /// {request.Documentation}
                    /// </summary>

                    """;
                var leadingTrivia = SyntaxFactory.ParseLeadingTrivia(docComment);
                method = method.WithLeadingTrivia(leadingTrivia);
            }

            var newTypeNode = typeNode.AddMembers(method);
            var newRoot = root.ReplaceNode(typeNode, newTypeNode);
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
                using (await AcquireFileLockAsync(document.FilePath, ct))
                {
                    await File.WriteAllTextAsync(document.FilePath, formattedRoot.ToFullString(), ct);
                }

                _workspaceService.ClearCache();
            }

            return RefactoringResult.Succeeded($"Added method '{request.MethodName}' to '{request.ClassName}'", document.FilePath != null ? [document.FilePath] : []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add method");
            return RefactoringResult.Failed($"Failed to add method: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> CreateTypeAsync(CreateTypeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating {Kind} {Type} in {Directory}", request.TypeKind, request.TypeName, request.TargetDirectory);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        // Create directory if it doesn't exist (common for new features)
        if (!Directory.Exists(request.TargetDirectory))
        {
            _logger.LogInformation("Creating directory: {Directory}", request.TargetDirectory);
            Directory.CreateDirectory(request.TargetDirectory);
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            // Find the project that contains this directory
            var project = FindProjectForDirectory(solution, request.TargetDirectory);
            if (project is null)
            {
                return RefactoringResult.Failed($"Could not find a project containing directory: {request.TargetDirectory}");
            }

            // Infer namespace from project and directory structure
            var targetNamespace = request.Namespace ?? InferNamespace(project, request.TargetDirectory);

            // Check if type already exists in the solution (idempotency)
            var existingType = await FindTypeAsync(solution, request.TypeName, ct);
            if (existingType != null)
            {
                var existingNamespace = existingType.ContainingNamespace?.ToDisplayString();
                if (existingNamespace == targetNamespace)
                {
                    _logger.LogInformation("{Kind} {Type} already exists in namespace {Namespace}, skipping",
                        request.TypeKind, request.TypeName, targetNamespace);
                    return RefactoringResult.Succeeded(
                        $"{request.TypeKind} '{request.TypeName}' already exists in namespace '{targetNamespace}'");
                }
            }

            // Build the type file
            var targetPath = Path.Combine(request.TargetDirectory, $"{request.TypeName}.cs");
            if (File.Exists(targetPath))
            {
                _logger.LogInformation("File already exists: {Path}, skipping", targetPath);
                return RefactoringResult.Succeeded($"File already exists: {targetPath}");
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
            var compilationUnit = SyntaxFactory.CompilationUnit().WithUsings(SyntaxFactory.List(usings)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(targetNamespace)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeDeclaration))));
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
            using (await AcquireFileLockAsync(targetPath, ct))
            {
                await File.WriteAllTextAsync(targetPath, fileContent, ct);
            }

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

    /// <summary>
    /// Check if two type names match, handling common variations like "int[]" vs "Int32[]".
    /// </summary>
    private static bool TypesMatch(string existingType, string requestType)
    {
        // Direct match
        if (existingType.Equals(requestType, StringComparison.Ordinal))
            return true;

        // Normalize common aliases
        var normalizedExisting = NormalizeTypeName(existingType);
        var normalizedRequest = NormalizeTypeName(requestType);

        return normalizedExisting.Equals(normalizedRequest, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalize type names by expanding C# aliases to their CLR names.
    /// </summary>
    private static string NormalizeTypeName(string typeName)
    {
        // Handle array/nullable suffix
        var suffix = "";
        var baseName = typeName;

        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            suffix = "[]";
            baseName = typeName[..^2];
        }
        else if (typeName.EndsWith("?", StringComparison.Ordinal))
        {
            suffix = "?";
            baseName = typeName[..^1];
        }

        // Map C# aliases to CLR types
        var normalized = baseName switch
        {
            "int" => "Int32",
            "uint" => "UInt32",
            "long" => "Int64",
            "ulong" => "UInt64",
            "short" => "Int16",
            "ushort" => "UInt16",
            "byte" => "Byte",
            "sbyte" => "SByte",
            "float" => "Single",
            "double" => "Double",
            "decimal" => "Decimal",
            "bool" => "Boolean",
            "char" => "Char",
            "string" => "String",
            "object" => "Object",
            _ => baseName
        };

        return normalized + suffix;
    }
}
