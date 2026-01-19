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
    // Import constants from shared class
    private const string FrameworkXUnit = TestFrameworkConstants.FrameworkXUnit;
    private const string FrameworkNUnit = TestFrameworkConstants.FrameworkNUnit;
    private const string FrameworkMsTest = TestFrameworkConstants.FrameworkMsTest;
    private const string MockLibNSubstitute = TestFrameworkConstants.MockLibNSubstitute;
    private const string MockLibMoq = TestFrameworkConstants.MockLibMoq;
    private const string MockLibFakeItEasy = TestFrameworkConstants.MockLibFakeItEasy;
    private const string NsNSubstitute = TestFrameworkConstants.NsNSubstitute;
    private const string NsMoq = TestFrameworkConstants.NsMoq;
    private const string NsFakeItEasy = TestFrameworkConstants.NsFakeItEasy;
    private const string AttrFact = TestFrameworkConstants.AttrFact;
    private const string AttrTheory = TestFrameworkConstants.AttrTheory;
    private const string AttrTest = TestFrameworkConstants.AttrTest;
    private const string AttrTestMethod = TestFrameworkConstants.AttrTestMethod;

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
                DetectedFramework = FrameworkXUnit,
                DetectedMockingLibrary = MockLibNSubstitute,
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

    private static bool IsTestProject(Project project) => TestFrameworkConstants.IsTestProject(project);

    private static bool IsUnitTestProject(Project project) => TestFrameworkConstants.IsUnitTestProject(project);

    private Task<string> DetectTestFrameworkAsync(Solution solution, INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        var testProject = solution.Projects.FirstOrDefault(IsTestProject);
        if (testProject is null)
            return Task.FromResult(FrameworkXUnit);

        return Task.FromResult(TestFrameworkConstants.DetectFrameworkFromReferences(testProject.MetadataReferences));
    }

    /// <summary>
    /// Detects the mocking library used in the test project.
    /// </summary>
    private string DetectMockingLibrary(Solution solution)
    {
        var testProject = solution.Projects.FirstOrDefault(IsTestProject);
        if (testProject is null)
            return MockLibNSubstitute;

        return TestFrameworkConstants.DetectMockingLibraryFromReferences(testProject.MetadataReferences);
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
                            return name is AttrFact or AttrTheory or AttrTest or AttrTestMethod
                                or AttrFact + "Attribute" or AttrTheory + "Attribute" or AttrTest + "Attribute" or AttrTestMethod + "Attribute";
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

        // Identify overloaded methods (same name appears multiple times)
        var methodNameCounts = members.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.Count());

        foreach (var member in members)
        {
            // Compute parameter signature for overload disambiguation
            var hasOverloads = methodNameCounts.TryGetValue(member.Name, out var count) && count > 1;
            var paramSignature = hasOverloads ? ComputeParameterSignature(member.Parameters) : null;

            // Check if method has any tests
            if (!testedMethods.Contains(member.Name))
            {
                gaps.Add(new TestGap
                {
                    MethodName = member.Name,
                    Kind = TestGapKind.NoTests,
                    Description = $"No tests found for {member.Name}",
                    Priority = TestPriority.High,
                    ParameterSignature = paramSignature
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
                    ParameterName = param.Name,
                    ParameterSignature = paramSignature
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
                    ExceptionType = exception,
                    ParameterSignature = paramSignature
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
                    Priority = TestPriority.Low,
                    ParameterSignature = paramSignature
                });
            }
        }

        return gaps;
    }

    /// <summary>
    /// Computes a simplified parameter signature for use in test method names.
    /// E.g., for (string name, int count) returns "String_Int".
    /// </summary>
    private static string ComputeParameterSignature(IReadOnlyList<ParameterInfo> parameters)
    {
        if (parameters.Count == 0)
            return "NoParams";

        var parts = parameters.Select(p => SimplifyTypeName(p.Type));
        return string.Join("_", parts);
    }

    /// <summary>
    /// Simplifies a type name for use in test method names.
    /// Removes generics, namespaces, and converts to PascalCase.
    /// </summary>
    private static string SimplifyTypeName(string typeName)
    {
        // Remove nullable suffix
        typeName = typeName.TrimEnd('?');

        // Handle generic types: take just the base name
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex > 0)
            typeName = typeName[..genericIndex];

        // Remove namespace if present
        var dotIndex = typeName.LastIndexOf('.');
        if (dotIndex >= 0)
            typeName = typeName[(dotIndex + 1)..];

        // Ensure first letter is uppercase
        if (typeName.Length > 0 && char.IsLower(typeName[0]))
            typeName = char.ToUpperInvariant(typeName[0]) + typeName[1..];

        return typeName;
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
        var (testFilePath, fileExists) = DetermineTestFilePath(solution, typeSymbol, request.OutputDirectory);

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

        // Optionally validate compilation
        IReadOnlyList<string>? diagnostics = null;
        bool? compilesSuccessfully = null;

        if (request.ValidateCompilation)
        {
            (compilesSuccessfully, diagnostics) = await ValidateGeneratedCodeAsync(solution, testFilePath, testCode, ct);
        }

        return new GeneratedTests
        {
            TestFilePath = testFilePath,
            FileCreated = !fileExists,
            TestsAdded = testMethods.Count,
            Tests = testMethods,
            CompilationDiagnostics = diagnostics,
            CompilesSuccessfully = compilesSuccessfully
        };
    }

    /// <summary>
    /// Validates the generated test code by running it through Roslyn compilation.
    /// </summary>
    private async Task<(bool success, IReadOnlyList<string> diagnostics)> ValidateGeneratedCodeAsync(
        Solution solution,
        string testFilePath,
        string testCode,
        CancellationToken ct)
    {
        try
        {
            // Find the test project this file belongs to
            var testProject = solution.Projects.FirstOrDefault(p =>
                p.Documents.Any(d => d.FilePath?.Equals(testFilePath, StringComparison.OrdinalIgnoreCase) == true) ||
                testFilePath.StartsWith(Path.GetDirectoryName(p.FilePath) ?? "", StringComparison.OrdinalIgnoreCase));

            if (testProject is null)
            {
                // Try to find any test project
                testProject = solution.Projects.FirstOrDefault(IsTestProject);
            }

            if (testProject is null)
            {
                return (true, new[] { "Warning: Could not find test project for validation" });
            }

            // Parse the generated code
            var syntaxTree = CSharpSyntaxTree.ParseText(testCode, cancellationToken: ct);

            // Get the project's compilation and add our new syntax tree
            var compilation = await testProject.GetCompilationAsync(ct);
            if (compilation is null)
            {
                return (true, new[] { "Warning: Could not get project compilation" });
            }

            // Add the new syntax tree to the compilation
            var newCompilation = compilation.AddSyntaxTrees(syntaxTree);

            // Get diagnostics (errors and warnings)
            var allDiagnostics = newCompilation.GetDiagnostics(ct);

            // Filter to only errors and warnings from our generated file
            var relevantDiagnostics = allDiagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Where(d => d.Location.SourceTree == syntaxTree ||
                           d.Location.Kind == LocationKind.None) // Include general errors
                .Select(d => $"{d.Severity}: {d.Id} - {d.GetMessage()}" +
                            (d.Location.IsInSource ? $" (line {d.Location.GetLineSpan().StartLinePosition.Line + 1})" : ""))
                .Distinct()
                .ToList();

            var hasErrors = allDiagnostics.Any(d =>
                d.Severity == DiagnosticSeverity.Error &&
                (d.Location.SourceTree == syntaxTree || d.Location.Kind == LocationKind.None));

            return (!hasErrors, relevantDiagnostics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate generated code compilation");
            return (true, new[] { $"Warning: Validation failed - {ex.Message}" });
        }
    }

    private (string path, bool exists) DetermineTestFilePath(Solution solution, INamedTypeSymbol typeSymbol, string? outputDirectory = null)
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
                    IsUnitTestProject(p) &&
                    p.ProjectReferences.Any(r => r.ProjectId == sourceProject?.Id));
            }
        }

        // Fallback: Find any unit test project (not integration)
        testProject ??= solution.Projects.FirstOrDefault(IsUnitTestProject);

        if (testProject is not null)
        {
            var testProjectDir = Path.GetDirectoryName(testProject.FilePath)!;

            // If outputDirectory is specified, use it
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                // If it's an absolute path, use it directly
                // Otherwise, treat it as relative to the test project
                var targetDir = Path.IsPathRooted(outputDirectory)
                    ? outputDirectory
                    : Path.Combine(testProjectDir, outputDirectory);
                return (Path.Combine(targetDir, $"{testClassName}.cs"), false);
            }

            // Mirror the source file's folder structure in the test project
            // e.g., Services/Testing/Foo.cs → Services/Testing/FooTests.cs
            var typeSourceLocation = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource);
            if (typeSourceLocation?.SourceTree?.FilePath is not null && sourceProject?.FilePath is not null)
            {
                var sourceProjectDir = Path.GetDirectoryName(sourceProject.FilePath)!;
                var sourceFilePath = typeSourceLocation.SourceTree.FilePath;

                // Get relative path from source project to source file
                if (sourceFilePath.StartsWith(sourceProjectDir, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = Path.GetRelativePath(sourceProjectDir, sourceFilePath);
                    var relativeDir = Path.GetDirectoryName(relativePath);

                    if (!string.IsNullOrEmpty(relativeDir))
                    {
                        // Create same folder structure in test project
                        var testDir = Path.Combine(testProjectDir, relativeDir);
                        return (Path.Combine(testDir, $"{testClassName}.cs"), false);
                    }
                }
            }

            // Fallback: place in test project root
            return (Path.Combine(testProjectDir, $"{testClassName}.cs"), false);
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

        // Build base method name, appending parameter signature for overloads
        var methodPart = !string.IsNullOrEmpty(gap.ParameterSignature)
            ? $"{gap.MethodName}_With{gap.ParameterSignature}"
            : gap.MethodName;

        return gap.Kind switch
        {
            TestGapKind.NoTests => $"{methodPart}_WhenCalled_ReturnsExpectedResult",
            TestGapKind.NullHandling when !string.IsNullOrEmpty(gap.ParameterName) =>
                $"{methodPart}_WithNull{ToPascalCase(gap.ParameterName)}_ThrowsArgumentNullException",
            TestGapKind.NullHandling => $"{methodPart}_WithNullInput_ThrowsArgumentNullException",
            TestGapKind.ErrorHandling when !string.IsNullOrEmpty(gap.ExceptionType) =>
                $"{methodPart}_WhenInvalid_Throws{GetExceptionShortName(gap.ExceptionType)}",
            TestGapKind.ErrorHandling => $"{methodPart}_WithInvalidInput_ThrowsExpectedException",
            TestGapKind.BoundaryValues => $"{methodPart}_WithBoundaryValue_HandlesCorrectly",
            TestGapKind.AsyncBehavior => $"{methodPart}_WhenAwaited_CompletesSuccessfully",
            _ => $"{methodPart}_Test"
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
        var constructorDeps = GetConstructorDependencies(typeSymbol);
        var hasDependencies = constructorDeps.Count > 0;

        // Check if any dependencies use IOptions<T>
        var hasOptionsPattern = constructorDeps.Any(d => d.TypeName.StartsWith("IOptions<", StringComparison.Ordinal));

        // Check if any method parameters are interfaces (will need mocking)
        var hasInterfaceParameters = HasInterfaceParameters(typeSymbol, testMethods);

        // Collect all required namespaces from method parameters and return types
        var requiredNamespaces = CollectRequiredNamespaces(typeSymbol, testMethods);

        // Usings
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This file was generated by Aura Test Generator.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();

        switch (framework)
        {
            case FrameworkXUnit:
                sb.AppendLine("using Xunit;");
                break;
            case FrameworkNUnit:
                sb.AppendLine("using NUnit.Framework;");
                break;
            case FrameworkMsTest:
                sb.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
                break;
        }

        // Add mocking library using if there are constructor dependencies OR interface method parameters
        if (hasDependencies || hasInterfaceParameters)
        {
            sb.AppendLine(GetMockingLibraryUsing(mockingLibrary));
        }

        // Add Microsoft.Extensions.Options if IOptions<T> is used
        if (hasOptionsPattern)
        {
            sb.AppendLine("using Microsoft.Extensions.Options;");
        }

        sb.AppendLine($"using {typeNamespace};");

        // Add additional namespaces required by parameter types
        foreach (var ns in requiredNamespaces.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (ns != typeNamespace && !ns.StartsWith("System.", StringComparison.Ordinal) &&
                ns != "System")
            {
                sb.AppendLine($"using {ns};");
            }
        }

        // Common System namespaces often needed
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();

        // Namespace and class
        sb.AppendLine($"namespace {typeNamespace}.Tests;");
        sb.AppendLine();

        if (framework == FrameworkMsTest)
        {
            sb.AppendLine("[TestClass]");
        }
        else if (framework == FrameworkNUnit)
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

    /// <summary>
    /// Checks if any method parameters in the test methods are interfaces.
    /// Used to determine if mocking library namespace is needed.
    /// </summary>
    private static bool HasInterfaceParameters(INamedTypeSymbol typeSymbol, List<GeneratedTestInfo> testMethods)
    {
        foreach (var test in testMethods)
        {
            var methods = typeSymbol.GetMembers(test.TargetMethod).OfType<IMethodSymbol>();
            foreach (var method in methods)
            {
                foreach (var param in method.Parameters)
                {
                    if (param.Type.TypeKind == TypeKind.Interface)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Collects all namespaces required by parameter types and return types of testable methods.
    /// This ensures generated test code has all necessary using statements.
    /// </summary>
    private static HashSet<string> CollectRequiredNamespaces(INamedTypeSymbol typeSymbol, List<GeneratedTestInfo> testMethods)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var visitedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var test in testMethods)
        {
            var methods = typeSymbol.GetMembers(test.TargetMethod).OfType<IMethodSymbol>();
            foreach (var method in methods)
            {
                // Collect from return type
                CollectNamespacesFromType(method.ReturnType, namespaces, visitedTypes);

                // Collect from parameters
                foreach (var param in method.Parameters)
                {
                    CollectNamespacesFromType(param.Type, namespaces, visitedTypes);
                }
            }
        }

        // Collect from constructor dependencies (using actual type symbols, not strings)
        CollectConstructorDependencyNamespaces(typeSymbol, namespaces, visitedTypes);

        return namespaces;
    }

    /// <summary>
    /// Collects namespaces from constructor parameter types.
    /// </summary>
    private static void CollectConstructorDependencyNamespaces(
        INamedTypeSymbol typeSymbol,
        HashSet<string> namespaces,
        HashSet<ITypeSymbol> visitedTypes)
    {
        // Find the primary constructor (usually the one with the most interface parameters)
        var primaryCtor = typeSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Count(p => p.Type.TypeKind == TypeKind.Interface))
            .FirstOrDefault();

        if (primaryCtor is null) return;

        foreach (var param in primaryCtor.Parameters)
        {
            CollectNamespacesFromType(param.Type, namespaces, visitedTypes);
        }
    }

    /// <summary>
    /// Recursively collects namespaces from a type symbol, including generic type arguments
    /// and required properties (since we generate object initializers for them).
    /// </summary>
    private static void CollectNamespacesFromType(ITypeSymbol typeSymbol, HashSet<string> namespaces, HashSet<ITypeSymbol>? visitedTypes = null)
    {
        // Prevent infinite recursion for cyclic type references
        visitedTypes ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        if (!visitedTypes.Add(typeSymbol))
            return;

        // Skip special types (int, string, etc.)
        if (typeSymbol.SpecialType != SpecialType.None)
            return;

        // Get the containing namespace
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
        {
            namespaces.Add(ns);
        }

        // Handle generic type arguments
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                CollectNamespacesFromType(typeArg, namespaces, visitedTypes);
            }
        }

        // Handle arrays
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            CollectNamespacesFromType(arrayType.ElementType, namespaces, visitedTypes);
        }

        // Collect from required properties (since we generate object initializers for them)
        if (typeSymbol is INamedTypeSymbol classType && typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct)
        {
            var requiredProps = GetRequiredProperties(classType);
            foreach (var prop in requiredProps)
            {
                CollectNamespacesFromType(prop.Type, namespaces, visitedTypes);
            }
        }
    }

    private static void GenerateTestMethod(
        StringBuilder sb,
        GeneratedTestInfo test,
        IMethodSymbol? method,
        string typeName,
        string framework,
        string mockingLibrary = MockLibNSubstitute)
    {
        var isAsync = method?.IsAsync == true ||
                      method?.ReturnType.Name is "Task" or "ValueTask";

        sb.AppendLine();

        // Attribute
        var attr = framework switch
        {
            FrameworkXUnit => "[Fact]",
            FrameworkNUnit => "[Test]",
            FrameworkMsTest => "[TestMethod]",
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

        // Check if the class is static - static classes can't be instantiated
        var containingType = method?.ContainingType;
        var isStaticClass = containingType?.IsStatic == true;

        if (isStaticClass)
        {
            // Static class - no SUT instantiation needed, methods are called directly on the type
            // No mock setup since static classes typically don't have constructor dependencies
        }
        else
        {
            // Constructor - try to identify if it needs mocks
            var constructorDeps = GetConstructorDependencies(containingType);
            if (constructorDeps.Count > 0)
            {
                // Generate mocks for each dependency using detected library
                var mockVarNames = new List<string>();
                foreach (var dep in constructorDeps)
                {
                    var varName = $"_{ToCamelCase(dep.ParameterName)}";
                    var typeName2 = dep.TypeName;

                    // Special handling for IOptions<T> - use Options.Create() instead of mocking
                    if (typeName2.StartsWith("IOptions<", StringComparison.Ordinal))
                    {
                        // Extract the inner type: IOptions<FooConfig> -> FooConfig
                        var innerType = typeName2.Substring(9, typeName2.Length - 10); // Remove "IOptions<" and ">"
                        sb.AppendLine($"        var {varName} = Options.Create(new {innerType}());");
                        mockVarNames.Add(varName);
                    }
                    else
                    {
                        var mockCreation = GenerateMockCreation(typeName2, mockingLibrary);
                        sb.AppendLine($"        var {varName} = {mockCreation};");
                        mockVarNames.Add(GetMockValue(varName, mockingLibrary));
                    }
                }

                var ctorArgs = string.Join(", ", mockVarNames);
                sb.AppendLine($"        var sut = new {typeName}({ctorArgs});");
            }
            else
            {
                sb.AppendLine($"        var sut = new {typeName}();");
            }
        }
        sb.AppendLine();

        // Act
        sb.AppendLine("        // Act");
        var awaitKeyword = isAsync ? "await " : "";

        // For static classes, call the method on the type directly; otherwise on sut
        var callTarget = isStaticClass ? typeName : "sut";

        if (method is not null)
        {
            var paramList = string.Join(", ", paramNames);
            var returnTypeStr = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (returnTypeStr != "void" && !returnTypeStr.StartsWith("Task", StringComparison.Ordinal))
            {
                sb.AppendLine($"        var result = {awaitKeyword}{callTarget}.{method.Name}({paramList});");
            }
            else if (returnTypeStr.StartsWith("Task<", StringComparison.Ordinal))
            {
                sb.AppendLine($"        var result = {awaitKeyword}{callTarget}.{method.Name}({paramList});");
            }
            else
            {
                sb.AppendLine($"        {awaitKeyword}{callTarget}.{method.Name}({paramList});");
            }
        }
        else
        {
            sb.AppendLine($"        // TODO: Call {test.TargetMethod}");
        }
        sb.AppendLine();

        // Assert
        sb.AppendLine("        // Assert");
        GenerateAssertions(sb, method, framework);

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates meaningful assertions based on the method's return type.
    /// </summary>
    private static void GenerateAssertions(StringBuilder sb, IMethodSymbol? method, string framework)
    {
        if (method is null)
        {
            sb.AppendLine("        // TODO: Add assertions to verify expected behavior");
            return;
        }

        var returnType = method.ReturnType;

        // Unwrap Task<T> and ValueTask<T>
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.Name is "Task" or "ValueTask" &&
            namedType.TypeArguments.Length > 0)
        {
            returnType = namedType.TypeArguments[0];
        }

        // Handle void and Task (non-generic)
        if (returnType.SpecialType == SpecialType.System_Void || returnType.Name == "Task")
        {
            GenerateVoidAssertion(sb, framework);
            return;
        }

        // Handle boolean
        if (returnType.SpecialType == SpecialType.System_Boolean)
        {
            GenerateBoolAssertion(sb, framework);
            return;
        }

        // Handle string
        if (returnType.SpecialType == SpecialType.System_String)
        {
            GenerateStringAssertion(sb, framework);
            return;
        }

        // Handle numeric types
        if (IsNumericType(returnType))
        {
            GenerateNumericAssertion(sb, framework);
            return;
        }

        // Handle collections
        if (IsCollectionType(returnType))
        {
            GenerateCollectionAssertion(sb, framework);
            return;
        }

        // Handle nullable reference types - assert not null
        if (returnType.NullableAnnotation == NullableAnnotation.Annotated ||
            returnType.TypeKind is TypeKind.Class or TypeKind.Interface)
        {
            GenerateNotNullAssertion(sb, framework);
            return;
        }

        // Fallback for value types - just verify no exception was thrown
        sb.AppendLine("        // Value returned successfully - add specific assertions as needed");
    }

    private static bool IsNumericType(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or
        SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or
        SpecialType.System_Byte or SpecialType.System_SByte => true,
        _ => false
    };

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol) return true;

        if (type is INamedTypeSymbol namedType)
        {
            // Check if it implements IEnumerable (but not string)
            if (type.SpecialType == SpecialType.System_String) return false;

            return namedType.Name is "List" or "Array" or "IList" or "ICollection" or
                   "IEnumerable" or "IReadOnlyList" or "IReadOnlyCollection" or
                   "Dictionary" or "IDictionary" or "IReadOnlyDictionary" or
                   "HashSet" or "ISet" ||
                   namedType.AllInterfaces.Any(i => i.Name == "IEnumerable");
        }

        return false;
    }

    private static void GenerateBoolAssertion(StringBuilder sb, string framework)
    {
        var assertion = framework switch
        {
            FrameworkXUnit => "Assert.True(result);",
            FrameworkNUnit => "Assert.That(result, Is.True);",
            FrameworkMsTest => "Assert.IsTrue(result);",
            _ => "Assert.True(result);"
        };
        sb.AppendLine($"        {assertion}");
    }

    private static void GenerateStringAssertion(StringBuilder sb, string framework)
    {
        var assertions = framework switch
        {
            FrameworkXUnit => ("Assert.NotNull(result);", "Assert.NotEmpty(result);"),
            FrameworkNUnit => ("Assert.That(result, Is.Not.Null);", "Assert.That(result, Is.Not.Empty);"),
            FrameworkMsTest => ("Assert.IsNotNull(result);", "Assert.IsFalse(string.IsNullOrEmpty(result));"),
            _ => ("Assert.NotNull(result);", "Assert.NotEmpty(result);")
        };
        sb.AppendLine($"        {assertions.Item1}");
        sb.AppendLine($"        {assertions.Item2}");
    }

    private static void GenerateNumericAssertion(StringBuilder sb, string framework)
    {
        var assertion = framework switch
        {
            FrameworkXUnit => "Assert.True(result >= 0); // Adjust expected value",
            FrameworkNUnit => "Assert.That(result, Is.GreaterThanOrEqualTo(0)); // Adjust expected value",
            FrameworkMsTest => "Assert.IsTrue(result >= 0); // Adjust expected value",
            _ => "Assert.True(result >= 0);"
        };
        sb.AppendLine($"        {assertion}");
    }

    private static void GenerateCollectionAssertion(StringBuilder sb, string framework)
    {
        var assertions = framework switch
        {
            FrameworkXUnit => ("Assert.NotNull(result);", "Assert.NotEmpty(result);"),
            FrameworkNUnit => ("Assert.That(result, Is.Not.Null);", "Assert.That(result, Is.Not.Empty);"),
            FrameworkMsTest => ("Assert.IsNotNull(result);", "Assert.IsTrue(result.Any());"),
            _ => ("Assert.NotNull(result);", "Assert.NotEmpty(result);")
        };
        sb.AppendLine($"        {assertions.Item1}");
        sb.AppendLine($"        {assertions.Item2}");
    }

    private static void GenerateNotNullAssertion(StringBuilder sb, string framework)
    {
        var assertion = framework switch
        {
            FrameworkXUnit => "Assert.NotNull(result);",
            FrameworkNUnit => "Assert.That(result, Is.Not.Null);",
            FrameworkMsTest => "Assert.IsNotNull(result);",
            _ => "Assert.NotNull(result);"
        };
        sb.AppendLine($"        {assertion}");
    }

    private static void GenerateVoidAssertion(StringBuilder sb, string framework)
    {
        // For void methods, we just confirm no exception was thrown
        sb.AppendLine("        // Method completed without throwing - add behavior verification if needed");
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

        // Collect required namespaces for the new tests
        var requiredNamespaces = CollectRequiredNamespaces(typeSymbol, methodsToAdd);

        // Add mocking library namespace if there are dependencies OR interface method parameters
        var constructorDeps = GetConstructorDependencies(typeSymbol);
        var hasInterfaceParams = HasInterfaceParameters(typeSymbol, methodsToAdd);
        if (constructorDeps.Count > 0 || hasInterfaceParams)
        {
            requiredNamespaces.Add(GetMockingLibraryNamespace(mockingLibrary));
        }

        // Add Microsoft.Extensions.Options if IOptions<T> is used in constructor
        if (constructorDeps.Any(d => d.TypeName.StartsWith("IOptions<", StringComparison.Ordinal)))
        {
            requiredNamespaces.Add("Microsoft.Extensions.Options");
        }

        // Get existing usings in the file
        var existingUsings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        // Find missing usings that need to be added
        var missingUsings = requiredNamespaces
            .Where(ns => !string.IsNullOrEmpty(ns) && !existingUsings.Contains(ns))
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToList();

        // Generate new methods as text
        var newMethods = new StringBuilder();
        foreach (var test in methodsToAdd)
        {
            var member = typeSymbol.GetMembers(test.TargetMethod).OfType<IMethodSymbol>().FirstOrDefault();
            GenerateTestMethod(newMethods, test, member, typeSymbol.Name, framework, mockingLibrary);
        }

        // Start with existing content
        var newContent = existingContent;

        // Add missing usings at the top of the file (after any existing usings)
        if (missingUsings.Count > 0)
        {
            // Find the last using directive to insert after it
            var lastUsing = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .LastOrDefault();

            if (lastUsing is not null)
            {
                var insertAfterUsing = lastUsing.Span.End;
                var newUsingsText = new StringBuilder();
                foreach (var ns in missingUsings)
                {
                    newUsingsText.AppendLine();
                    newUsingsText.Append($"using {ns};");
                }

                newContent = newContent.Insert(insertAfterUsing, newUsingsText.ToString());

                // Recalculate the closing brace position since we modified the file
                var updatedTree = CSharpSyntaxTree.ParseText(newContent);
                var updatedRoot = await updatedTree.GetRootAsync(ct);
                var updatedTestClass = updatedRoot.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text.EndsWith("Tests", StringComparison.Ordinal));

                if (updatedTestClass is not null)
                {
                    var updatedClosingBrace = updatedTestClass.CloseBraceToken;
                    newContent = newContent.Insert(updatedClosingBrace.SpanStart, newMethods.ToString());
                }
            }
            else
            {
                // No existing usings, add at the very top
                var newUsingsText = new StringBuilder();
                foreach (var ns in missingUsings)
                {
                    newUsingsText.AppendLine($"using {ns};");
                }
                newUsingsText.AppendLine();
                newContent = newUsingsText.ToString() + newContent;

                // Insert test methods before closing brace (recalculate position)
                var updatedTree = CSharpSyntaxTree.ParseText(newContent);
                var updatedRoot = await updatedTree.GetRootAsync(ct);
                var updatedTestClass = updatedRoot.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text.EndsWith("Tests", StringComparison.Ordinal));

                if (updatedTestClass is not null)
                {
                    var updatedClosingBrace = updatedTestClass.CloseBraceToken;
                    newContent = newContent.Insert(updatedClosingBrace.SpanStart, newMethods.ToString());
                }
            }

            _logger.LogInformation("Added {Count} missing using statements to {File}", missingUsings.Count, testFilePath);
        }
        else
        {
            // No missing usings, just insert test methods before the closing brace of the class
            var closingBrace = testClass.CloseBraceToken;
            var insertPosition = closingBrace.SpanStart;
            newContent = newContent.Insert(insertPosition, newMethods.ToString());
        }

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
                    // Check for required properties - must be initialized in object initializer
                    var requiredProps = GetRequiredProperties(classType);
                    if (requiredProps.Count > 0)
                    {
                        var initializers = requiredProps
                            .Select(p => $"{p.Name} = {GenerateTestValue(p.Type, p.Name.ToLowerInvariant(), mockingLibrary)}")
                            .ToList();
                        return $"new {classType.Name} {{ {string.Join(", ", initializers)} }}";
                    }

                    return $"new {classType.Name}()";
                }

                // No parameterless constructor - check for primary constructor or required-only init
                // Try to find a constructor we can call, or fall through to default
                var requiredPropsNoctor = GetRequiredProperties(classType);
                if (requiredPropsNoctor.Count > 0)
                {
                    // Type has required properties but no parameterless ctor - might be a record or have primary ctor
                    // Generate object creation with required properties
                    var initializers = requiredPropsNoctor
                        .Select(p => $"{p.Name} = {GenerateTestValue(p.Type, p.Name.ToLowerInvariant(), mockingLibrary)}")
                        .ToList();
                    return $"new {classType.Name} {{ {string.Join(", ", initializers)} }}";
                }
            }
        }

        // Fallback to default with comment
        return $"default! /* {typeSymbol.Name} - provide test instance */";
    }

    /// <summary>
    /// Get all required properties from a type (C# 11+ required modifier).
    /// </summary>
    private static List<IPropertySymbol> GetRequiredProperties(INamedTypeSymbol typeSymbol)
    {
        var result = new List<IPropertySymbol>();

        // Walk up the inheritance chain to find all required properties
        var current = typeSymbol;
        while (current is not null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.IsRequired)
                {
                    // Avoid duplicates from inheritance
                    if (!result.Any(p => p.Name == prop.Name))
                    {
                        result.Add(prop);
                    }
                }
            }

            current = current.BaseType;
        }

        return result;
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

    #region Mocking Library Helpers

    /// <summary>
    /// Gets the namespace for a mocking library (e.g., "NSubstitute").
    /// </summary>
    private static string GetMockingLibraryNamespace(string mockingLibrary) =>
        TestFrameworkConstants.GetMockingLibraryNamespace(mockingLibrary) ?? NsNSubstitute;

    /// <summary>
    /// Gets the using statement for a mocking library.
    /// </summary>
    private static string GetMockingLibraryUsing(string mockingLibrary)
    {
        return $"using {GetMockingLibraryNamespace(mockingLibrary)};";
    }

    /// <summary>
    /// Generates the mock creation expression for the given type.
    /// </summary>
    private static string GenerateMockCreation(string typeName, string mockingLibrary)
    {
        return mockingLibrary.ToLowerInvariant() switch
        {
            MockLibNSubstitute => $"Substitute.For<{typeName}>()",
            MockLibMoq => $"new Mock<{typeName}>()",
            MockLibFakeItEasy => $"A.Fake<{typeName}>()",
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
            MockLibMoq => $"{varName}.Object",
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
            MockLibNSubstitute => $"Substitute.For<{typeName}>()",
            MockLibMoq => $"Mock.Of<{typeName}>()",
            MockLibFakeItEasy => $"A.Fake<{typeName}>()",
            _ => $"Substitute.For<{typeName}>()"
        };
    }

    #endregion
}
