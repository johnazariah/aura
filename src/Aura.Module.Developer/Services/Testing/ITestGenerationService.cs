// <copyright file="ITestGenerationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Testing;

/// <summary>
/// Service for automated test generation.
/// Analyzes code to identify testable surfaces and generates test methods.
/// </summary>
public interface ITestGenerationService
{
    /// <summary>
    /// Generates tests for a target symbol (class, method, or namespace).
    /// </summary>
    /// <param name="request">The test generation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing generated tests and analysis.</returns>
    Task<TestGenerationResult> GenerateTestsAsync(TestGenerationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Request for test generation.
/// </summary>
public sealed record TestGenerationRequest
{
    /// <summary>Target symbol: class name, method name (Class.Method), or namespace.</summary>
    public required string Target { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: explicit test count. If null, generates comprehensive tests.</summary>
    public int? Count { get; init; }

    /// <summary>Optional: maximum tests to generate (default: 20).</summary>
    public int MaxTests { get; init; } = 20;

    /// <summary>Optional: focus area for tests.</summary>
    public TestFocus Focus { get; init; } = TestFocus.All;

    /// <summary>Optional: override framework detection (xunit, nunit, mstest).</summary>
    public string? TestFramework { get; init; }

    /// <summary>If true, return analysis only without generating code.</summary>
    public bool AnalyzeOnly { get; init; }

    /// <summary>If true, validate generated code compiles before returning (adds latency).</summary>
    public bool ValidateCompilation { get; init; }
}

/// <summary>
/// Focus area for test generation.
/// </summary>
public enum TestFocus
{
    /// <summary>Generate all types of tests.</summary>
    All,

    /// <summary>Focus on happy path tests.</summary>
    HappyPath,

    /// <summary>Focus on edge cases (null, empty, boundaries).</summary>
    EdgeCases,

    /// <summary>Focus on error handling (exceptions, validation).</summary>
    ErrorHandling,
}

/// <summary>
/// Result of test generation.
/// </summary>
public sealed record TestGenerationResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Analysis of the testable surface.</summary>
    public TestAnalysis? Analysis { get; init; }

    /// <summary>Generated tests information.</summary>
    public GeneratedTests? Generated { get; init; }

    /// <summary>Reason why generation stopped.</summary>
    public string? StoppingReason { get; init; }

    /// <summary>Error details if failed.</summary>
    public string? Error { get; init; }

    public static TestGenerationResult Succeeded(string message, TestAnalysis? analysis = null, GeneratedTests? generated = null, string? stoppingReason = null) =>
        new() { Success = true, Message = message, Analysis = analysis, Generated = generated, StoppingReason = stoppingReason };

    public static TestGenerationResult Failed(string error) =>
        new() { Success = false, Message = error, Error = error };
}

/// <summary>
/// Analysis of the testable surface.
/// </summary>
public sealed record TestAnalysis
{
    /// <summary>Public methods available for testing.</summary>
    public required IReadOnlyList<TestableMember> TestableMembers { get; init; }

    /// <summary>Existing test files and counts.</summary>
    public required IReadOnlyList<ExistingTestInfo> ExistingTests { get; init; }

    /// <summary>Identified gaps in test coverage.</summary>
    public required IReadOnlyList<TestGap> Gaps { get; init; }

    /// <summary>Detected test framework.</summary>
    public required string DetectedFramework { get; init; }

    /// <summary>Detected mocking library (nsubstitute, moq, fakeiteasy).</summary>
    public string DetectedMockingLibrary { get; init; } = "nsubstitute";

    /// <summary>Suggested number of tests to generate.</summary>
    public int SuggestedTestCount { get; init; }
}

/// <summary>
/// A member that can be tested.
/// </summary>
public sealed record TestableMember
{
    /// <summary>Name of the method.</summary>
    public required string Name { get; init; }

    /// <summary>Full signature for display.</summary>
    public required string Signature { get; init; }

    /// <summary>Return type.</summary>
    public required string ReturnType { get; init; }

    /// <summary>Parameters.</summary>
    public required IReadOnlyList<ParameterInfo> Parameters { get; init; }

    /// <summary>Whether the method is async.</summary>
    public bool IsAsync { get; init; }

    /// <summary>Documented/inferred exceptions thrown.</summary>
    public IReadOnlyList<string> ThrowsExceptions { get; init; } = [];

    /// <summary>Containing type name.</summary>
    public required string ContainingType { get; init; }
}

/// <summary>
/// Parameter information for a testable member.
/// </summary>
public sealed record ParameterInfo
{
    /// <summary>Parameter name.</summary>
    public required string Name { get; init; }

    /// <summary>Parameter type.</summary>
    public required string Type { get; init; }

    /// <summary>Whether the parameter is nullable.</summary>
    public bool IsNullable { get; init; }

    /// <summary>Whether the parameter has a default value.</summary>
    public bool HasDefaultValue { get; init; }
}

/// <summary>
/// Information about existing tests.
/// </summary>
public sealed record ExistingTestInfo
{
    /// <summary>Path to the test file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Number of test methods in the file.</summary>
    public int TestCount { get; init; }

    /// <summary>Methods that are tested in this file.</summary>
    public IReadOnlyList<string> TestedMethods { get; init; } = [];
}

/// <summary>
/// A gap in test coverage.
/// </summary>
public sealed record TestGap
{
    /// <summary>The method with missing tests.</summary>
    public required string MethodName { get; init; }

    /// <summary>What kind of test is missing.</summary>
    public required TestGapKind Kind { get; init; }

    /// <summary>Description of the gap.</summary>
    public required string Description { get; init; }

    /// <summary>Priority for addressing this gap.</summary>
    public TestPriority Priority { get; init; } = TestPriority.Medium;

    /// <summary>Parameter name for null/boundary tests (optional, used for unique test naming).</summary>
    public string? ParameterName { get; init; }

    /// <summary>Exception type for error handling tests (optional).</summary>
    public string? ExceptionType { get; init; }

    /// <summary>Parameter signature for disambiguating overloads (e.g., "string_int" for method(string, int)).</summary>
    public string? ParameterSignature { get; init; }
}

/// <summary>
/// Kind of test gap.
/// </summary>
public enum TestGapKind
{
    /// <summary>No tests at all for this method.</summary>
    NoTests,

    /// <summary>Missing null input handling test.</summary>
    NullHandling,

    /// <summary>Missing error/exception handling test.</summary>
    ErrorHandling,

    /// <summary>Missing boundary value test.</summary>
    BoundaryValues,

    /// <summary>Missing async completion test.</summary>
    AsyncBehavior,
}

/// <summary>
/// Priority for a test gap.
/// </summary>
public enum TestPriority
{
    /// <summary>Low priority.</summary>
    Low,

    /// <summary>Medium priority.</summary>
    Medium,

    /// <summary>High priority.</summary>
    High,
}

/// <summary>
/// Information about generated tests.
/// </summary>
public sealed record GeneratedTests
{
    /// <summary>Path to the test file (created or modified).</summary>
    public required string TestFilePath { get; init; }

    /// <summary>Whether the file was created (vs modified).</summary>
    public bool FileCreated { get; init; }

    /// <summary>Number of tests added.</summary>
    public int TestsAdded { get; init; }

    /// <summary>Summary of each generated test.</summary>
    public required IReadOnlyList<GeneratedTestInfo> Tests { get; init; }

    /// <summary>Compilation diagnostics if ValidateCompilation was enabled (errors/warnings).</summary>
    public IReadOnlyList<string>? CompilationDiagnostics { get; init; }

    /// <summary>Whether the generated code compiles successfully (null if validation not run).</summary>
    public bool? CompilesSuccessfully { get; init; }
}

/// <summary>
/// Information about a single generated test.
/// </summary>
public sealed record GeneratedTestInfo
{
    /// <summary>Name of the test method.</summary>
    public required string TestName { get; init; }

    /// <summary>What aspect this test covers.</summary>
    public required string Description { get; init; }

    /// <summary>The method being tested.</summary>
    public required string TargetMethod { get; init; }
}
