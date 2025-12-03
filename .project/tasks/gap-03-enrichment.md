# Task: Cross-File Relationship Enrichment

## Overview
Add enrichment phase to extract richer relationships: transitive call chains, type usage across files, and test-to-code mappings.

## Parent Spec
`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 3

## Goals
1. Add `UsesType`, `Tests`, and `Documents` edge types
2. Compute transitive relationships (what tests cover this method?)
3. Map test files to the code they test
4. Extract type usage from method parameters, return types, locals

## Data Model Changes

### Extend CodeEdgeType Enum

**File:** `src/Aura.Foundation/Data/Entities/CodeEdge.cs`

```csharp
public enum CodeEdgeType
{
    // Existing...
    Contains,
    Declares,
    Inherits,
    Implements,
    References,
    Calls,
    Uses,
    Overrides,
    
    // New - Gap 2 (Content Files)
    Documents = 20,
    CoLocated = 21,
    
    // New - Gap 3 (Enrichment)
    /// <summary>Method uses this type (parameter, return, local, field access).</summary>
    UsesType = 30,
    
    /// <summary>Test method directly or transitively tests this code element.</summary>
    Tests = 31,
    
    /// <summary>Test class is the test suite for this class.</summary>
    TestSuiteFor = 32,
}
```

## Enrichment Service

### IGraphEnrichmentService

**File:** `src/Aura.Foundation/Rag/IGraphEnrichmentService.cs`

```csharp
public interface IGraphEnrichmentService
{
    /// <summary>
    /// Runs all enrichment passes on the code graph.
    /// </summary>
    Task<EnrichmentResult> EnrichAsync(
        string workspacePath,
        EnrichmentOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes test-to-code mappings.
    /// </summary>
    Task<int> EnrichTestMappingsAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts type usage from methods.
    /// </summary>
    Task<int> EnrichTypeUsageAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes transitive call relationships.
    /// </summary>
    Task<int> ComputeTransitiveCallsAsync(
        string workspacePath,
        int maxDepth = 5,
        CancellationToken cancellationToken = default);
}

public record EnrichmentResult(
    int TestMappingsCreated,
    int TypeUsagesCreated,
    int TransitiveEdgesCreated,
    TimeSpan Duration,
    List<string> Warnings);

public record EnrichmentOptions
{
    public bool IncludeTestMappings { get; init; } = true;
    public bool IncludeTypeUsage { get; init; } = true;
    public bool IncludeTransitiveCalls { get; init; } = false; // Can be slow
    public int TransitiveMaxDepth { get; init; } = 5;
}
```

## Implementation: GraphEnrichmentService

**File:** `src/Aura.Foundation/Rag/GraphEnrichmentService.cs`

```csharp
public sealed class GraphEnrichmentService : IGraphEnrichmentService
{
    private readonly AuraDbContext _dbContext;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly ILogger<GraphEnrichmentService> _logger;

    public async Task<EnrichmentResult> EnrichAsync(
        string workspacePath,
        EnrichmentOptions? options,
        CancellationToken ct)
    {
        options ??= new EnrichmentOptions();
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        var testMappings = 0;
        var typeUsages = 0;
        var transitive = 0;

        if (options.IncludeTestMappings)
        {
            testMappings = await EnrichTestMappingsAsync(workspacePath, ct);
        }

        if (options.IncludeTypeUsage)
        {
            typeUsages = await EnrichTypeUsageAsync(workspacePath, ct);
        }

        if (options.IncludeTransitiveCalls)
        {
            transitive = await ComputeTransitiveCallsAsync(
                workspacePath, options.TransitiveMaxDepth, ct);
        }

        return new EnrichmentResult(
            testMappings, typeUsages, transitive, stopwatch.Elapsed, warnings);
    }
}
```

## Test Mapping Enrichment

### Test File Detection

```csharp
public async Task<int> EnrichTestMappingsAsync(
    string workspacePath,
    CancellationToken ct)
{
    var edgesCreated = 0;

    // Find all test files by convention
    var testFiles = await _dbContext.CodeNodes
        .Where(n => n.WorkspacePath == workspacePath)
        .Where(n => n.NodeType == CodeNodeType.File)
        .Where(n => n.FilePath!.Contains("Tests") || 
                   n.FilePath!.Contains("Test") ||
                   n.Name!.EndsWith("Tests.cs") ||
                   n.Name!.EndsWith("Test.cs"))
        .ToListAsync(ct);

    foreach (var testFile in testFiles)
    {
        // Find test classes in this file
        var testClasses = await _dbContext.CodeNodes
            .Where(n => n.FilePath == testFile.FilePath)
            .Where(n => n.NodeType == CodeNodeType.Class)
            .ToListAsync(ct);

        foreach (var testClass in testClasses)
        {
            // Infer the class under test from naming convention
            // e.g., WorkflowServiceTests -> WorkflowService
            var classUnderTestName = InferClassUnderTest(testClass.Name!);
            if (classUnderTestName == null) continue;

            var classesUnderTest = await _dbContext.CodeNodes
                .Where(n => n.WorkspacePath == workspacePath)
                .Where(n => n.NodeType == CodeNodeType.Class)
                .Where(n => n.Name == classUnderTestName)
                .ToListAsync(ct);

            foreach (var cut in classesUnderTest)
            {
                // Create TestSuiteFor edge
                var exists = await _dbContext.CodeEdges.AnyAsync(
                    e => e.SourceId == testClass.Id && 
                         e.TargetId == cut.Id && 
                         e.EdgeType == CodeEdgeType.TestSuiteFor, ct);

                if (!exists)
                {
                    _dbContext.CodeEdges.Add(new CodeEdge
                    {
                        Id = Guid.NewGuid(),
                        EdgeType = CodeEdgeType.TestSuiteFor,
                        SourceId = testClass.Id,
                        TargetId = cut.Id,
                    });
                    edgesCreated++;
                }
            }

            // Find test methods and create Tests edges
            var testMethods = await _dbContext.CodeNodes
                .Join(_dbContext.CodeEdges,
                    n => n.Id, e => e.TargetId,
                    (n, e) => new { Node = n, Edge = e })
                .Where(x => x.Edge.SourceId == testClass.Id && 
                           x.Edge.EdgeType == CodeEdgeType.Contains &&
                           x.Node.NodeType == CodeNodeType.Method)
                .Select(x => x.Node)
                .ToListAsync(ct);

            foreach (var testMethod in testMethods)
            {
                // Find what methods this test calls (transitively)
                var calledMethods = await GetTransitiveCallsAsync(testMethod.Id, 3, ct);
                
                foreach (var calledMethod in calledMethods)
                {
                    // Skip if the called method is itself a test
                    if (calledMethod.FilePath?.Contains("Test") == true) continue;

                    var exists = await _dbContext.CodeEdges.AnyAsync(
                        e => e.SourceId == testMethod.Id &&
                             e.TargetId == calledMethod.Id &&
                             e.EdgeType == CodeEdgeType.Tests, ct);

                    if (!exists)
                    {
                        _dbContext.CodeEdges.Add(new CodeEdge
                        {
                            Id = Guid.NewGuid(),
                            EdgeType = CodeEdgeType.Tests,
                            SourceId = testMethod.Id,
                            TargetId = calledMethod.Id,
                            PropertiesJson = JsonSerializer.Serialize(new { transitive = true }),
                        });
                        edgesCreated++;
                    }
                }
            }
        }
    }

    await _dbContext.SaveChangesAsync(ct);
    return edgesCreated;
}

private string? InferClassUnderTest(string testClassName)
{
    // Common patterns: FooTests, FooTest, FooSpec
    var patterns = new[] { "Tests", "Test", "Spec", "_Tests", "_Test" };
    foreach (var pattern in patterns)
    {
        if (testClassName.EndsWith(pattern))
        {
            return testClassName[..^pattern.Length];
        }
    }
    return null;
}
```

## Type Usage Enrichment

### Extract Type References via Roslyn

```csharp
public async Task<int> EnrichTypeUsageAsync(
    string workspacePath,
    CancellationToken ct)
{
    var edgesCreated = 0;

    // Find solution file in workspace
    var solutionPath = Directory.GetFiles(workspacePath, "*.sln").FirstOrDefault();
    if (solutionPath == null)
    {
        _logger.LogWarning("No solution file found in {WorkspacePath}", workspacePath);
        return 0;
    }

    var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);

    foreach (var project in solution.Projects)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation == null) continue;

        foreach (var document in project.Documents)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(ct);
            var semanticModel = compilation.GetSemanticModel(syntaxTree!);

            // Find all method declarations
            var root = await syntaxTree!.GetRootAsync(ct);
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                if (methodSymbol == null) continue;

                var usedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

                // Return type
                if (methodSymbol.ReturnType is INamedTypeSymbol returnType)
                {
                    CollectTypes(returnType, usedTypes);
                }

                // Parameters
                foreach (var param in methodSymbol.Parameters)
                {
                    if (param.Type is INamedTypeSymbol paramType)
                    {
                        CollectTypes(paramType, usedTypes);
                    }
                }

                // Local variables and field accesses
                var dataFlow = semanticModel.AnalyzeDataFlow(method.Body ?? (SyntaxNode)method.ExpressionBody!);
                foreach (var variable in dataFlow.VariablesDeclared)
                {
                    if (variable.Type is INamedTypeSymbol varType)
                    {
                        CollectTypes(varType, usedTypes);
                    }
                }

                // Create UsesType edges
                foreach (var usedType in usedTypes)
                {
                    // Find the type node in our graph
                    var typeNodes = await _dbContext.CodeNodes
                        .Where(n => n.WorkspacePath == workspacePath)
                        .Where(n => n.FullName == usedType.ToDisplayString() ||
                                   n.Name == usedType.Name)
                        .Where(n => n.NodeType == CodeNodeType.Class ||
                                   n.NodeType == CodeNodeType.Interface ||
                                   n.NodeType == CodeNodeType.Record ||
                                   n.NodeType == CodeNodeType.Struct)
                        .ToListAsync(ct);

                    // Find the method node in our graph
                    var methodNode = await _dbContext.CodeNodes
                        .Where(n => n.WorkspacePath == workspacePath)
                        .Where(n => n.FullName == methodSymbol.ToDisplayString() ||
                                   n.Name == methodSymbol.Name)
                        .Where(n => n.NodeType == CodeNodeType.Method)
                        .FirstOrDefaultAsync(ct);

                    if (methodNode == null) continue;

                    foreach (var typeNode in typeNodes)
                    {
                        var exists = await _dbContext.CodeEdges.AnyAsync(
                            e => e.SourceId == methodNode.Id &&
                                 e.TargetId == typeNode.Id &&
                                 e.EdgeType == CodeEdgeType.UsesType, ct);

                        if (!exists)
                        {
                            _dbContext.CodeEdges.Add(new CodeEdge
                            {
                                Id = Guid.NewGuid(),
                                EdgeType = CodeEdgeType.UsesType,
                                SourceId = methodNode.Id,
                                TargetId = typeNode.Id,
                            });
                            edgesCreated++;
                        }
                    }
                }
            }
        }
    }

    await _dbContext.SaveChangesAsync(ct);
    return edgesCreated;
}

private void CollectTypes(INamedTypeSymbol type, HashSet<INamedTypeSymbol> collected)
{
    // Skip system types
    if (type.ContainingNamespace?.ToDisplayString()?.StartsWith("System") == true)
        return;

    collected.Add(type);

    // Include generic type arguments
    foreach (var typeArg in type.TypeArguments.OfType<INamedTypeSymbol>())
    {
        CollectTypes(typeArg, collected);
    }
}
```

## Transitive Call Computation

```csharp
public async Task<int> ComputeTransitiveCallsAsync(
    string workspacePath,
    int maxDepth,
    CancellationToken ct)
{
    // This creates a "TransitiveCalls" property on existing Calls edges
    // Rather than creating new edges (which would explode in count)
    
    var methods = await _dbContext.CodeNodes
        .Where(n => n.WorkspacePath == workspacePath)
        .Where(n => n.NodeType == CodeNodeType.Method)
        .ToListAsync(ct);

    var updated = 0;

    foreach (var method in methods)
    {
        var transitiveTargets = await GetTransitiveCallsAsync(method.Id, maxDepth, ct);
        
        // Store in PropertiesJson
        if (transitiveTargets.Any())
        {
            var existingProps = string.IsNullOrEmpty(method.PropertiesJson)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(method.PropertiesJson)!;

            existingProps["transitiveCallCount"] = transitiveTargets.Count;
            existingProps["transitiveCallIds"] = transitiveTargets.Select(t => t.Id).ToList();
            
            // Update is tricky with init-only properties; may need to use raw SQL
            updated++;
        }
    }

    return updated;
}

private async Task<List<CodeNode>> GetTransitiveCallsAsync(Guid methodId, int maxDepth, CancellationToken ct)
{
    var visited = new HashSet<Guid> { methodId };
    var result = new List<CodeNode>();
    var frontier = new Queue<(Guid Id, int Depth)>();
    frontier.Enqueue((methodId, 0));

    while (frontier.Count > 0)
    {
        var (currentId, depth) = frontier.Dequeue();
        if (depth >= maxDepth) continue;

        var callees = await _dbContext.CodeEdges
            .Where(e => e.SourceId == currentId && e.EdgeType == CodeEdgeType.Calls)
            .Select(e => e.Target!)
            .ToListAsync(ct);

        foreach (var callee in callees)
        {
            if (visited.Add(callee.Id))
            {
                result.Add(callee);
                frontier.Enqueue((callee.Id, depth + 1));
            }
        }
    }

    return result;
}
```

## Query Support

### Find Tests for Code

**File:** `src/Aura.Foundation/Rag/ICodeGraphService.cs`

Add methods:
```csharp
/// <summary>
/// Finds test methods that directly or transitively test a code element.
/// </summary>
Task<IReadOnlyList<CodeNode>> FindTestsAsync(
    string symbolName,
    string? containingTypeName = null,
    string? workspacePath = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Finds all types that a method uses (parameters, return, locals).
/// </summary>
Task<IReadOnlyList<CodeNode>> FindTypeUsageAsync(
    string methodName,
    string? containingTypeName = null,
    string? workspacePath = null,
    CancellationToken cancellationToken = default);
```

## API Endpoints

**File:** `src/Aura.Api/Controllers/GraphController.cs`

```csharp
[HttpGet("tests/{symbolName}")]
public async Task<ActionResult<IReadOnlyList<CodeNode>>> FindTests(
    string symbolName,
    [FromQuery] string? containingType,
    [FromQuery] string? workspacePath,
    CancellationToken ct)
{
    var tests = await _graphService.FindTestsAsync(symbolName, containingType, workspacePath, ct);
    return Ok(tests);
}

[HttpPost("enrich")]
public async Task<ActionResult<EnrichmentResult>> Enrich(
    [FromQuery] string workspacePath,
    [FromBody] EnrichmentOptions? options,
    CancellationToken ct)
{
    var result = await _enrichmentService.EnrichAsync(workspacePath, options, ct);
    return Ok(result);
}
```

## Testing

### Unit Tests
- `GraphEnrichmentServiceTests.cs` - Mock Roslyn, verify edge creation
- `TestMappingTests.cs` - Test file detection, naming conventions

### Integration Tests
- Index a project with tests
- Verify `Tests` edges exist between test methods and production code
- Verify `UsesType` edges for method parameters

## Rollout Plan

1. **Phase 1**: Add new edge types (migration)
2. **Phase 2**: Implement test mapping enrichment
3. **Phase 3**: Implement type usage enrichment via Roslyn
4. **Phase 4**: Add transitive computation (optional)
5. **Phase 5**: Add query methods and API

## Dependencies
- `RoslynWorkspaceService` in Developer module
- Existing `ICodeGraphService`

## Estimated Effort
- **Medium complexity**, **Medium effort**
- Roslyn semantic analysis is powerful but requires careful handling

## Success Criteria
- [ ] "find tests for WorkflowService.Execute" returns test methods
- [ ] Test classes have `TestSuiteFor` edges to production classes
- [ ] Methods have `UsesType` edges to their parameter/return types
- [ ] Enrichment completes for Aura solution in < 60 seconds
