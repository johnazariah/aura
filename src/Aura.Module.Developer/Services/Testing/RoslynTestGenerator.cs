// <copyright file="RoslynTestGenerator.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Testing;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Test generation service using Roslyn for C# code.
/// </summary>
public sealed partial class RoslynTestGenerator : ITestGenerationService
{
    private readonly IRoslynWorkspaceService _workspaceService;
    private readonly ILogger<RoslynTestGenerator> _logger;

    public RoslynTestGenerator(
        IRoslynWorkspaceService workspaceService,
        ILogger<RoslynTestGenerator> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TestGenerationResult> GenerateTestsAsync(TestGenerationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating tests for {Target} in {Solution}", request.Target, request.SolutionPath);

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);

            // Find the target symbol(s)
            var targetSymbol = await FindTargetSymbolAsync(solution, request.Target, ct);
            if (targetSymbol is null)
            {
                return TestGenerationResult.Failed($"Target '{request.Target}' not found in solution");
            }

            // Analyze testable surface
            var analysis = await AnalyzeTestSurfaceAsync(solution, targetSymbol, ct);

            // If analyze only, return just the analysis
            if (request.AnalyzeOnly)
            {
                return TestGenerationResult.Succeeded(
                    $"Analysis complete: {analysis.TestableMembers.Count} testable members, {analysis.Gaps.Count} gaps identified",
                    analysis: analysis,
                    stoppingReason: "Analyze only mode");
            }

            // Determine how many tests to generate
            var testsToGenerate = DetermineTestCount(request, analysis);
            if (testsToGenerate == 0)
            {
                return TestGenerationResult.Succeeded(
                    "No tests needed - all public methods appear to have tests",
                    analysis: analysis,
                    stoppingReason: "All public methods have at least one test");
            }

            // Generate the tests
            var generated = await GenerateTestCodeAsync(solution, targetSymbol, analysis, testsToGenerate, request, ct);

            var stoppingReason = DetermineStoppingReason(request, analysis, generated);

            return TestGenerationResult.Succeeded(
                $"Generated {generated.TestsAdded} test(s) in {Path.GetFileName(generated.TestFilePath)}",
                analysis: analysis,
                generated: generated,
                stoppingReason: stoppingReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate tests for {Target}", request.Target);
            return TestGenerationResult.Failed($"Test generation failed: {ex.Message}");
        }
    }

    private async Task<ISymbol?> FindTargetSymbolAsync(Solution solution, string target, CancellationToken ct)
    {
        // Check if target is qualified (e.g., "Class.Method")
        var parts = target.Split('.');
        var typeName = parts[0];
        var memberName = parts.Length > 1 ? parts[1] : null;

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            // Search all types
            foreach (var typeSymbol in GetAllTypes(compilation))
            {
                if (typeSymbol.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    if (memberName is not null)
                    {
                        // Looking for a specific member
                        var member = typeSymbol.GetMembers(memberName).FirstOrDefault();
                        if (member is not null) return member;
                    }
                    else
                    {
                        return typeSymbol;
                    }
                }
            }
        }

        return null;
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

    private async Task<TestAnalysis> AnalyzeTestSurfaceAsync(Solution solution, ISymbol targetSymbol, CancellationToken ct)
    {
        var testableMembers = new List<TestableMember>();
        var existingTests = new List<ExistingTestInfo>();
        var gaps = new List<TestGap>();

        // Get the type to analyze
        var typeSymbol = targetSymbol as INamedTypeSymbol ?? (targetSymbol as IMethodSymbol)?.ContainingType;
        if (typeSymbol is null)
        {
            return new TestAnalysis
            {
                TestableMembers = testableMembers,
                ExistingTests = existingTests,
                Gaps = gaps,
                DetectedFramework = "xunit",
                DetectedMockingLibrary = "nsubstitute",
                SuggestedTestCount = 0
            };
        }

        // Collect testable members
        var targetMethods = new List<IMethodSymbol>();

        if (targetSymbol is IMethodSymbol methodSymbol)
        {
            // Single method target
            targetMethods.Add(methodSymbol);
        }
        else
        {
            // Class target - get all public methods
            targetMethods.AddRange(
                typeSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.DeclaredAccessibility == Accessibility.Public)
                    .Where(m => m.MethodKind == MethodKind.Ordinary)
                    .Where(m => !m.IsStatic || m.ContainingType.IsStatic));
        }

        foreach (var method in targetMethods)
        {
            var testableMember = CreateTestableMember(method);
            testableMembers.Add(testableMember);
        }

        // Find existing tests
        var detectedFramework = await DetectTestFrameworkAsync(solution, typeSymbol, ct);
        var detectedMockingLibrary = DetectMockingLibrary(solution);
        existingTests = await FindExistingTestsAsync(solution, typeSymbol, ct);

        // Identify gaps
        gaps = IdentifyTestGaps(testableMembers, existingTests);

        var suggestedCount = gaps.Count(g => g.Kind == TestGapKind.NoTests) +
                             Math.Min(5, gaps.Count(g => g.Kind != TestGapKind.NoTests));

        return new TestAnalysis
        {
            TestableMembers = testableMembers,
            ExistingTests = existingTests,
            Gaps = gaps,
            DetectedFramework = detectedFramework,
            DetectedMockingLibrary = detectedMockingLibrary,
            SuggestedTestCount = suggestedCount
        };
    }

    private static TestableMember CreateTestableMember(IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IsNullable = p.NullableAnnotation == NullableAnnotation.Annotated ||
                        (p.Type.IsReferenceType && p.NullableAnnotation != NullableAnnotation.NotAnnotated),
            HasDefaultValue = p.HasExplicitDefaultValue
        }).ToList();

        // Look for documented exceptions
        var exceptions = new List<string>();
        var docComment = method.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(docComment))
        {
            var exceptionMatches = ExceptionDocRegex().Matches(docComment);
            foreach (Match match in exceptionMatches)
            {
                exceptions.Add(match.Groups[1].Value);
            }
        }

        return new TestableMember
        {
            Name = method.Name,
            Signature = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            ReturnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            Parameters = parameters,
            IsAsync = method.IsAsync || method.ReturnType.Name == "Task" || method.ReturnType.Name == "ValueTask",
            ThrowsExceptions = exceptions,
            ContainingType = method.ContainingType.Name
        };
    }

    [GeneratedRegex(@"<exception cref=""T:([^""]+)""")]
    private static partial Regex ExceptionDocRegex();

    private async Task<string> DetectTestFrameworkAsync(Solution solution, INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        // Find test projects by convention
        var testProjects = solution.Projects.Where(p =>
            p.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

        foreach (var project in testProjects)
        {
            var refs = project.MetadataReferences.Select(r => r.Display ?? "").ToList();

            if (refs.Any(r => r.Contains("xunit", StringComparison.OrdinalIgnoreCase)))
                return "xunit";
            if (refs.Any(r => r.Contains("nunit", StringComparison.OrdinalIgnoreCase)))
                return "nunit";
            if (refs.Any(r => r.Contains("MSTest", StringComparison.OrdinalIgnoreCase)))
                return "mstest";
        }

        return "xunit"; // default
    }

    /// <summary>
    /// Detects the mocking library used in the test project.
    /// </summary>
    private string DetectMockingLibrary(Solution solution)
    {
        // Find test projects by convention
        var testProjects = solution.Projects.Where(p =>
            p.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

        foreach (var project in testProjects)
        {
            var refs = project.MetadataReferences.Select(r => r.Display ?? "").ToList();

            if (refs.Any(r => r.Contains("NSubstitute", StringComparison.OrdinalIgnoreCase)))
                return "nsubstitute";
            if (refs.Any(r => r.Contains("Moq", StringComparison.OrdinalIgnoreCase) &&
                         !r.Contains("NSubstitute", StringComparison.OrdinalIgnoreCase)))
                return "moq";
            if (refs.Any(r => r.Contains("FakeItEasy", StringComparison.OrdinalIgnoreCase)))
                return "fakeiteasy";
        }

        return "nsubstitute"; // default - most common in modern .NET
    }

    private async Task<List<ExistingTestInfo>> FindExistingTestsAsync(Solution solution, INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        var result = new List<ExistingTestInfo>();
        var typeName = typeSymbol.Name;

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var fileName = Path.GetFileNameWithoutExtension(document.Name);

                // Check if this looks like a test file for our type
                if (!fileName.Contains(typeName, StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                if (syntaxTree is null) continue;

                var root = await syntaxTree.GetRootAsync(ct);

                // Count test methods (by attribute)
                var testMethods = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.AttributeLists.SelectMany(a => a.Attributes)
                        .Any(attr =>
                        {
                            var name = attr.Name.ToString();
                            return name is "Fact" or "Theory" or "Test" or "TestMethod"
                                or "FactAttribute" or "TheoryAttribute" or "TestAttribute" or "TestMethodAttribute";
                        }))
                    .ToList();

                if (testMethods.Count > 0)
                {
                    // Find which methods from the SUT are being tested
                    var testedMethods = new HashSet<string>();
                    var sutMembers = typeSymbol.GetMembers().OfType<IMethodSymbol>().Select(m => m.Name).ToHashSet();

                    foreach (var testMethod in testMethods)
                    {
                        // Look for method invocations in the test
                        var invocations = testMethod.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Select(i => i.Expression)
                            .OfType<MemberAccessExpressionSyntax>()
                            .Select(m => m.Name.Identifier.Text);

                        foreach (var invocation in invocations)
                        {
                            if (sutMembers.Contains(invocation))
                            {
                                testedMethods.Add(invocation);
                            }
                        }
                    }

                    result.Add(new ExistingTestInfo
                    {
                        FilePath = document.FilePath ?? document.Name,
                        TestCount = testMethods.Count,
                        TestedMethods = testedMethods.ToList()
                    });
                }
            }
        }

        return result;
    }

    private static List<TestGap> IdentifyTestGaps(IReadOnlyList<TestableMember> members, IReadOnlyList<ExistingTestInfo> existingTests)
    {
        var gaps = new List<TestGap>();
        var testedMethods = existingTests.SelectMany(t => t.TestedMethods).ToHashSet();

        foreach (var member in members)
        {
            // Check if method has any tests
            if (!testedMethods.Contains(member.Name))
            {
                gaps.Add(new TestGap
                {
                    MethodName = member.Name,
                    Kind = TestGapKind.NoTests,
                    Description = $"No tests found for {member.Name}",
                    Priority = TestPriority.High
                });
            }

            // Check for nullable parameters
            foreach (var param in member.Parameters.Where(p => p.IsNullable))
            {
                gaps.Add(new TestGap
                {
                    MethodName = member.Name,
                    Kind = TestGapKind.NullHandling,
                    Description = $"Missing null handling test for parameter '{param.Name}'",
                    Priority = TestPriority.Medium,
                    ParameterName = param.Name
                });
            }

            // Check for documented exceptions
            foreach (var exception in member.ThrowsExceptions)
            {
                gaps.Add(new TestGap
                {
                    MethodName = member.Name,
                    Kind = TestGapKind.ErrorHandling,
                    Description = $"Missing test for {exception} exception",
                    Priority = TestPriority.Medium,
                    ExceptionType = exception
                });
            }

            // Check for async methods
            if (member.IsAsync)
            {
                gaps.Add(new TestGap
                {
                    MethodName = member.Name,
                    Kind = TestGapKind.AsyncBehavior,
                    Description = "Consider testing async completion behavior",
                    Priority = TestPriority.Low
                });
            }
        }

        return gaps;
    }

    private static int DetermineTestCount(TestGenerationRequest request, TestAnalysis analysis)
    {
        if (request.Count.HasValue)
        {
            return Math.Min(request.Count.Value, request.MaxTests);
        }

        // Comprehensive mode: generate tests for all gaps, up to max
        return Math.Min(analysis.SuggestedTestCount, request.MaxTests);
    }

    private async Task<GeneratedTests> GenerateTestCodeAsync(
        Solution solution,
        ISymbol targetSymbol,
        TestAnalysis analysis,
        int testsToGenerate,
        TestGenerationRequest request,
        CancellationToken ct)
    {
        var typeSymbol = targetSymbol as INamedTypeSymbol ?? (targetSymbol as IMethodSymbol)?.ContainingType;
        if (typeSymbol is null)
        {
            throw new InvalidOperationException("Could not determine containing type for test generation");
        }

        // Find or determine test file location
        var (testFilePath, fileExists) = DetermineTestFilePath(solution, typeSymbol);

        // Generate test methods based on gaps
        var testMethods = GenerateTestMethods(analysis, testsToGenerate, request.Focus, analysis.DetectedFramework);

        // Build the test file content
        var testCode = fileExists
            ? await AppendToExistingTestFileAsync(testFilePath, testMethods, typeSymbol, analysis.DetectedFramework, analysis.DetectedMockingLibrary, ct)
            : GenerateNewTestFile(typeSymbol, testMethods, analysis.DetectedFramework, analysis.DetectedMockingLibrary);

        // Write the file
        var directory = Path.GetDirectoryName(testFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(testFilePath, testCode, ct);

        // Clear workspace cache so subsequent operations see the new file
        _workspaceService.ClearCache();

        return new GeneratedTests
        {
            TestFilePath = testFilePath,
            FileCreated = !fileExists,
            TestsAdded = testMethods.Count,
            Tests = testMethods
        };
    }

    private (string path, bool exists) DetermineTestFilePath(Solution solution, INamedTypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.Name;
        var testClassName = $"{typeName}Tests";

        // Get the source project containing the type
        var sourceProject = FindSourceProject(solution, typeSymbol);
        var sourceProjectName = sourceProject?.Name;

        // First, check for existing test files
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var fileName = Path.GetFileNameWithoutExtension(document.Name);
                if (fileName.Equals(testClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return (document.FilePath!, true);
                }
            }
        }

        // Find the MATCHING test project for the source project
        // Convention: SourceProject → SourceProject.Tests
        Project? testProject = null;

        if (!string.IsNullOrEmpty(sourceProjectName))
        {
            // Try exact match first: Aura.Module.Developer → Aura.Module.Developer.Tests
            testProject = solution.Projects.FirstOrDefault(p =>
                p.Name.Equals($"{sourceProjectName}.Tests", StringComparison.OrdinalIgnoreCase));

            // If no exact match, try to find a test project that references the source project
            if (testProject is null)
            {
                testProject = solution.Projects.FirstOrDefault(p =>
                    p.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                    !p.Name.Contains("Integration", StringComparison.OrdinalIgnoreCase) &&
                    p.ProjectReferences.Any(r => r.ProjectId == sourceProject?.Id));
            }
        }

        // Fallback: Find any unit test project (not integration)
        testProject ??= solution.Projects.FirstOrDefault(p =>
            p.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Contains("Integration", StringComparison.OrdinalIgnoreCase));

        if (testProject is not null)
        {
            // Get a reference document to determine the directory structure
            var refDoc = testProject.Documents.FirstOrDefault();
            if (refDoc?.FilePath is not null)
            {
                var projectDir = Path.GetDirectoryName(refDoc.FilePath)!;
                return (Path.Combine(projectDir, $"{testClassName}.cs"), false);
            }
        }

        // Fallback: Create next to source
        var sourceLocation = typeSymbol.Locations.FirstOrDefault();
        if (sourceLocation?.SourceTree?.FilePath is not null)
        {
            var sourceDir = Path.GetDirectoryName(sourceLocation.SourceTree.FilePath)!;
            return (Path.Combine(sourceDir, $"{testClassName}.cs"), false);
        }

        throw new InvalidOperationException("Could not determine test file location");
    }

    private static Project? FindSourceProject(Solution solution, INamedTypeSymbol typeSymbol)
    {
        // Find the project that contains the source file for this type
        var sourceLocation = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (sourceLocation?.SourceTree?.FilePath is null)
        {
            return null;
        }

        var sourcePath = sourceLocation.SourceTree.FilePath;

        foreach (var project in solution.Projects)
        {
            if (project.Documents.Any(d =>
                string.Equals(d.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase)))
            {
                return project;
            }
        }

        return null;
    }

    private static List<GeneratedTestInfo> GenerateTestMethods(
        TestAnalysis analysis,
        int testsToGenerate,
        TestFocus focus,
        string framework)
    {
        var testMethods = new List<GeneratedTestInfo>();
        var gapsToAddress = analysis.Gaps.OrderByDescending(g => g.Priority).ToList();

        // Filter by focus if specified
        if (focus != TestFocus.All)
        {
            gapsToAddress = focus switch
            {
                TestFocus.HappyPath => gapsToAddress.Where(g => g.Kind == TestGapKind.NoTests).ToList(),
                TestFocus.EdgeCases => gapsToAddress.Where(g => g.Kind is TestGapKind.NullHandling or TestGapKind.BoundaryValues).ToList(),
                TestFocus.ErrorHandling => gapsToAddress.Where(g => g.Kind == TestGapKind.ErrorHandling).ToList(),
                _ => gapsToAddress
            };
        }

        foreach (var gap in gapsToAddress.Take(testsToGenerate))
        {
            var testName = GenerateTestName(gap, analysis);
            testMethods.Add(new GeneratedTestInfo
            {
                TestName = testName,
                Description = gap.Description,
                TargetMethod = gap.MethodName
            });
        }

        return testMethods;
    }

    private static string GenerateTestName(TestGap gap, TestAnalysis analysis)
    {
        var member = analysis.TestableMembers.FirstOrDefault(m => m.Name == gap.MethodName);

        return gap.Kind switch
        {
            TestGapKind.NoTests => $"{gap.MethodName}_WhenCalled_ReturnsExpectedResult",
            TestGapKind.NullHandling when !string.IsNullOrEmpty(gap.ParameterName) =>
                $"{gap.MethodName}_WithNull{ToPascalCase(gap.ParameterName)}_ThrowsArgumentNullException",
            TestGapKind.NullHandling => $"{gap.MethodName}_WithNullInput_ThrowsArgumentNullException",
            TestGapKind.ErrorHandling when !string.IsNullOrEmpty(gap.ExceptionType) =>
                $"{gap.MethodName}_WhenInvalid_Throws{GetExceptionShortName(gap.ExceptionType)}",
            TestGapKind.ErrorHandling => $"{gap.MethodName}_WithInvalidInput_ThrowsExpectedException",
            TestGapKind.BoundaryValues => $"{gap.MethodName}_WithBoundaryValue_HandlesCorrectly",
            TestGapKind.AsyncBehavior => $"{gap.MethodName}_WhenAwaited_CompletesSuccessfully",
            _ => $"{gap.MethodName}_Test"
        };
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string GetExceptionShortName(string exceptionType)
    {
        // Handle full type names like "System.ArgumentException" -> "ArgumentException"
        var shortName = exceptionType.Contains('.') ? exceptionType[(exceptionType.LastIndexOf('.') + 1)..] : exceptionType;
        return shortName;
    }

    private string GenerateNewTestFile(INamedTypeSymbol typeSymbol, List<GeneratedTestInfo> testMethods, string framework, string mockingLibrary)
    {
        var sb = new StringBuilder();
        var typeName = typeSymbol.Name;
        var typeNamespace = typeSymbol.ContainingNamespace.ToDisplayString();
        var hasDependencies = GetConstructorDependencies(typeSymbol).Count > 0;

        // Usings
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This file was generated by Aura Test Generator.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();

        switch (framework)
        {
            case "xunit":
                sb.AppendLine("using Xunit;");
                break;
            case "nunit":
                sb.AppendLine("using NUnit.Framework;");
                break;
            case "mstest":
                sb.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
                break;
        }

        // Add mocking library using if there are dependencies
        if (hasDependencies)
        {
            sb.AppendLine(GetMockingLibraryUsing(mockingLibrary));
        }

        sb.AppendLine($"using {typeNamespace};");
        sb.AppendLine();

        // Namespace and class
        sb.AppendLine($"namespace {typeNamespace}.Tests;");
        sb.AppendLine();

        if (framework == "mstest")
        {
            sb.AppendLine("[TestClass]");
        }
        else if (framework == "nunit")
        {
            sb.AppendLine("[TestFixture]");
        }

        sb.AppendLine($"public class {typeName}Tests");
        sb.AppendLine("{");

        // Generate each test method
        foreach (var test in testMethods)
        {
            var member = typeSymbol.GetMembers(test.TargetMethod).OfType<IMethodSymbol>().FirstOrDefault();
            GenerateTestMethod(sb, test, member, typeName, framework, mockingLibrary);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateTestMethod(
        StringBuilder sb,
        GeneratedTestInfo test,
        IMethodSymbol? method,
        string typeName,
        string framework,
        string mockingLibrary = "nsubstitute")
    {
        var isAsync = method?.IsAsync == true ||
                      method?.ReturnType.Name is "Task" or "ValueTask";

        sb.AppendLine();

        // Attribute
        var attr = framework switch
        {
            "xunit" => "[Fact]",
            "nunit" => "[Test]",
            "mstest" => "[TestMethod]",
            _ => "[Fact]"
        };
        sb.AppendLine($"    {attr}");

        // Method signature
        var asyncKeyword = isAsync ? "async " : "";
        var returnType = isAsync ? "Task" : "void";
        sb.AppendLine($"    public {asyncKeyword}{returnType} {test.TestName}()");
        sb.AppendLine("    {");

        // Arrange
        sb.AppendLine("        // Arrange");

        // Generate parameter declarations with realistic values
        var paramDeclarations = new List<string>();
        var paramNames = new List<string>();

        if (method is not null)
        {
            foreach (var param in method.Parameters)
            {
                var (declaration, varName) = GenerateParameterSetup(param, mockingLibrary);
                if (!string.IsNullOrEmpty(declaration))
                {
                    paramDeclarations.Add(declaration);
                }
                paramNames.Add(varName);
            }
        }

        // Output parameter declarations
        foreach (var decl in paramDeclarations)
        {
            sb.AppendLine($"        {decl}");
        }

        // Constructor - try to identify if it needs mocks
        var constructorDeps = GetConstructorDependencies(method?.ContainingType);
        if (constructorDeps.Count > 0)
        {
            // Generate mocks for each dependency using detected library
            var mockVarNames = new List<string>();
            foreach (var dep in constructorDeps)
            {
                var varName = $"_{ToCamelCase(dep.ParameterName)}";
                var typeName2 = dep.TypeName;
                var mockCreation = GenerateMockCreation(typeName2, mockingLibrary);
                sb.AppendLine($"        var {varName} = {mockCreation};");
                mockVarNames.Add(GetMockValue(varName, mockingLibrary));
            }

            var ctorArgs = string.Join(", ", mockVarNames);
            sb.AppendLine($"        var sut = new {typeName}({ctorArgs});");
        }
        else
        {
            sb.AppendLine($"        var sut = new {typeName}();");
        }
        sb.AppendLine();

        // Act
        sb.AppendLine("        // Act");
        var awaitKeyword = isAsync ? "await " : "";
        if (method is not null)
        {
            var paramList = string.Join(", ", paramNames);
            var returnTypeStr = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (returnTypeStr != "void" && !returnTypeStr.StartsWith("Task", StringComparison.Ordinal))
            {
                sb.AppendLine($"        var result = {awaitKeyword}sut.{method.Name}({paramList});");
            }
            else if (returnTypeStr.StartsWith("Task<", StringComparison.Ordinal))
            {
                sb.AppendLine($"        var result = {awaitKeyword}sut.{method.Name}({paramList});");
            }
            else
            {
                sb.AppendLine($"        {awaitKeyword}sut.{method.Name}({paramList});");
            }
        }
        else
        {
            sb.AppendLine($"        // TODO: Call {test.TargetMethod}");
        }
        sb.AppendLine();

        // Assert
        sb.AppendLine("        // Assert");
        var assertNotNull = framework switch
        {
            "xunit" => "Assert.NotNull(result);",
            "nunit" => "Assert.That(result, Is.Not.Null);",
            "mstest" => "Assert.IsNotNull(result);",
            _ => "Assert.NotNull(result);"
        };

        if (method?.ReturnType.SpecialType != SpecialType.System_Void &&
            method?.ReturnType.Name != "Task")
        {
            sb.AppendLine($"        // TODO: Add meaningful assertions");
            sb.AppendLine($"        {assertNotNull}");
        }
        else
        {
            sb.AppendLine("        // TODO: Add assertions to verify expected behavior");
        }

        sb.AppendLine("    }");
    }

    private async Task<string> AppendToExistingTestFileAsync(
        string testFilePath,
        List<GeneratedTestInfo> testMethods,
        INamedTypeSymbol typeSymbol,
        string framework,
        string mockingLibrary,
        CancellationToken ct)
    {
        var existingContent = await File.ReadAllTextAsync(testFilePath, ct);
        var tree = CSharpSyntaxTree.ParseText(existingContent);
        var root = await tree.GetRootAsync(ct);

        // Find the test class
        var testClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text.EndsWith("Tests", StringComparison.Ordinal));

        if (testClass is null)
        {
            // No test class found, generate new file
            return GenerateNewTestFile(typeSymbol, testMethods, framework, mockingLibrary);
        }

        // Get existing method names to avoid duplicates
        var existingMethodNames = testClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);

        // Filter out tests that would duplicate existing methods
        var methodsToAdd = testMethods
            .Where(t => !existingMethodNames.Contains(t.TestName))
            .ToList();

        if (methodsToAdd.Count == 0)
        {
            _logger.LogInformation("All test methods already exist in {File}, skipping append", testFilePath);
            return existingContent;
        }

        _logger.LogInformation("Appending {Count} new test methods to {File} (skipped {Skipped} duplicates)",
            methodsToAdd.Count, testFilePath, testMethods.Count - methodsToAdd.Count);

        // Generate new methods as text
        var newMethods = new StringBuilder();
        foreach (var test in methodsToAdd)
        {
            var member = typeSymbol.GetMembers(test.TargetMethod).OfType<IMethodSymbol>().FirstOrDefault();
            GenerateTestMethod(newMethods, test, member, typeSymbol.Name, framework, mockingLibrary);
        }

        // Insert before the closing brace of the class
        var closingBrace = testClass.CloseBraceToken;
        var insertPosition = closingBrace.SpanStart;

        var newContent = existingContent.Insert(insertPosition, newMethods.ToString());
        return newContent;
    }

    private static string DetermineStoppingReason(
        TestGenerationRequest request,
        TestAnalysis analysis,
        GeneratedTests generated)
    {
        if (request.Count.HasValue && generated.TestsAdded >= request.Count.Value)
        {
            return $"Requested count ({request.Count.Value}) reached";
        }

        var noTestGaps = analysis.Gaps.Count(g => g.Kind == TestGapKind.NoTests);
        if (generated.TestsAdded >= noTestGaps && noTestGaps > 0)
        {
            return "All public methods now have at least one test";
        }

        if (generated.TestsAdded >= request.MaxTests)
        {
            var remaining = analysis.Gaps.Count - generated.TestsAdded;
            return $"Max tests ({request.MaxTests}) reached, {remaining} gaps remaining";
        }

        return "Test generation complete";
    }

    /// <summary>
    /// Generate a variable declaration and value for a method parameter.
    /// </summary>
    private static (string declaration, string varName) GenerateParameterSetup(IParameterSymbol param, string mockingLibrary)
    {
        var paramName = param.Name;
        var varName = paramName;
        var typeSymbol = param.Type;

        // Handle common types with realistic test values
        var value = GenerateTestValue(typeSymbol, paramName, mockingLibrary);

        if (value.StartsWith("new ", StringComparison.Ordinal) ||
            value.Contains('\n') ||
            value.Length > 40)
        {
            // Complex value - use a variable
            var declaration = $"var {varName} = {value};";
            return (declaration, varName);
        }
        else
        {
            // Simple value - can be inlined
            return (string.Empty, value);
        }
    }

    /// <summary>
    /// Generate a realistic test value for a given type.
    /// </summary>
    private static string GenerateTestValue(ITypeSymbol typeSymbol, string contextName, string mockingLibrary)
    {
        var typeName = typeSymbol.Name;
        var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Handle nullable types
        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var innerType = namedType.TypeArguments[0];
            return GenerateTestValue(innerType, contextName, mockingLibrary);
        }

        // Handle special types
        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_String:
                return $"\"test-{contextName}\"";
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
                return "42";
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_UInt16:
                return "42u";
            case SpecialType.System_Double:
                return "3.14";
            case SpecialType.System_Single:
                return "3.14f";
            case SpecialType.System_Decimal:
                return "99.99m";
            case SpecialType.System_Boolean:
                return "true";
            case SpecialType.System_DateTime:
                return "DateTime.UtcNow";
            case SpecialType.System_Byte:
                return "(byte)1";
            case SpecialType.System_Char:
                return "'X'";
        }

        // Handle common framework types by name
        return typeName switch
        {
            "Guid" => "Guid.NewGuid()",
            "DateTimeOffset" => "DateTimeOffset.UtcNow",
            "TimeSpan" => "TimeSpan.FromMinutes(5)",
            "Uri" => "new Uri(\"https://example.com\")",
            "CancellationToken" => "CancellationToken.None",
            _ => GenerateComplexTypeValue(typeSymbol, contextName, mockingLibrary)
        };
    }

    /// <summary>
    /// Generate values for complex/custom types including enums.
    /// </summary>
    private static string GenerateComplexTypeValue(ITypeSymbol typeSymbol, string contextName, string mockingLibrary)
    {
        // Handle enums - use the FIRST actual enum member
        if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
        {
            var firstMember = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .FirstOrDefault();

            if (firstMember is not null)
            {
                return $"{enumType.Name}.{firstMember.Name}";
            }

            // Fallback to default if no members found
            return $"default({enumType.Name})";
        }

        // Handle List<T>, IList<T>, IEnumerable<T>, IReadOnlyList<T>, etc.
        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var typeArg = genericType.TypeArguments[0];
            var typeArgName = typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (genericType.Name is "List" or "IList" or "ICollection" or "IEnumerable" or "IReadOnlyList" or "IReadOnlyCollection")
            {
                var innerValue = GenerateTestValue(typeArg, "item", mockingLibrary);
                return $"new List<{typeArgName}> {{ {innerValue} }}";
            }

            if (genericType.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary" && genericType.TypeArguments.Length == 2)
            {
                var keyType = genericType.TypeArguments[0];
                var valueType = genericType.TypeArguments[1];
                var keyTypeName = keyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var valueTypeName = valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var keyValue = GenerateTestValue(keyType, "key", mockingLibrary);
                var valueValue = GenerateTestValue(valueType, "value", mockingLibrary);
                return $"new Dictionary<{keyTypeName}, {valueTypeName}> {{ [{keyValue}] = {valueValue} }}";
            }

            if (genericType.Name is "Task" or "ValueTask")
            {
                if (genericType.TypeArguments.Length > 0)
                {
                    var innerValue = GenerateTestValue(typeArg, contextName, mockingLibrary);
                    return $"Task.FromResult({innerValue})";
                }
                return "Task.CompletedTask";
            }
        }

        // Handle arrays
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementType = arrayType.ElementType;
            var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var innerValue = GenerateTestValue(elementType, "item", mockingLibrary);
            return $"new {elementTypeName}[] {{ {innerValue} }}";
        }

        // Handle interfaces - use detected mocking library
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            var interfaceTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return GenerateInlineMock(interfaceTypeName, mockingLibrary);
        }

        // For other types, try to instantiate with new()
        // This will work for simple DTOs/records
        if (typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct)
        {
            // Check if it has a parameterless constructor
            if (typeSymbol is INamedTypeSymbol classType)
            {
                var hasParameterlessCtor = classType.Constructors
                    .Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);

                if (hasParameterlessCtor)
                {
                    return $"new {classType.Name}()";
                }
            }
        }

        // Fallback to default with comment
        return $"default! /* {typeSymbol.Name} - provide test instance */";
    }

    /// <summary>
    /// Check if a type has constructor dependencies (typically interfaces).
    /// </summary>
    private static bool HasDependencyInjection(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol is null) return false;

        return typeSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .Any(c => c.Parameters.Any(p => p.Type.TypeKind == TypeKind.Interface));
    }

    /// <summary>
    /// Gets constructor dependencies for the primary public constructor.
    /// </summary>
    private static List<(string ParameterName, string TypeName)> GetConstructorDependencies(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol is null) return new List<(string, string)>();

        // Find the primary constructor (usually the one with the most interface parameters)
        var primaryCtor = typeSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Count(p => p.Type.TypeKind == TypeKind.Interface))
            .FirstOrDefault();

        if (primaryCtor is null) return new List<(string, string)>();

        return primaryCtor.Parameters
            .Select(p => (
                ParameterName: p.Name,
                TypeName: p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
            .ToList();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Gets the using statement for a mocking library.
    /// </summary>
    private static string GetMockingLibraryUsing(string mockingLibrary)
    {
        return mockingLibrary.ToLowerInvariant() switch
        {
            "nsubstitute" => "using NSubstitute;",
            "moq" => "using Moq;",
            "fakeiteasy" => "using FakeItEasy;",
            _ => "using NSubstitute;"
        };
    }

    /// <summary>
    /// Generates the mock creation expression for the given type.
    /// </summary>
    private static string GenerateMockCreation(string typeName, string mockingLibrary)
    {
        return mockingLibrary.ToLowerInvariant() switch
        {
            "nsubstitute" => $"Substitute.For<{typeName}>()",
            "moq" => $"new Mock<{typeName}>()",
            "fakeiteasy" => $"A.Fake<{typeName}>()",
            _ => $"Substitute.For<{typeName}>()"
        };
    }

    /// <summary>
    /// Gets the value to pass to constructor from a mock variable.
    /// Moq requires .Object, others don't.
    /// </summary>
    private static string GetMockValue(string varName, string mockingLibrary)
    {
        return mockingLibrary.ToLowerInvariant() switch
        {
            "moq" => $"{varName}.Object",
            _ => varName
        };
    }

    /// <summary>
    /// Generates an inline mock/substitute for an interface parameter.
    /// </summary>
    private static string GenerateInlineMock(string typeName, string mockingLibrary)
    {
        return mockingLibrary.ToLowerInvariant() switch
        {
            "nsubstitute" => $"Substitute.For<{typeName}>()",
            "moq" => $"Mock.Of<{typeName}>()",
            "fakeiteasy" => $"A.Fake<{typeName}>()",
            _ => $"Substitute.For<{typeName}>()"
        };
    }
}
