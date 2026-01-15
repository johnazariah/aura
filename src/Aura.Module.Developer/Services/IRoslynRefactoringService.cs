// <copyright file="IRoslynRefactoringService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for performing Roslyn-based code refactoring operations.
/// Provides safe, semantic-aware code modifications.
/// </summary>
public interface IRoslynRefactoringService
{
    /// <summary>
    /// Renames a symbol and all its references across the solution.
    /// </summary>
    /// <param name="request">The rename request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing affected files or error information.</returns>
    Task<RefactoringResult> RenameSymbolAsync(RenameSymbolRequest request, CancellationToken ct = default);

    /// <summary>
    /// Changes a method signature (add/remove/reorder parameters).
    /// </summary>
    /// <param name="request">The signature change request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing affected files or error information.</returns>
    Task<RefactoringResult> ChangeMethodSignatureAsync(ChangeSignatureRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates interface implementation stubs for a class.
    /// </summary>
    /// <param name="request">The implement interface request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the modified file or error information.</returns>
    Task<RefactoringResult> ImplementInterfaceAsync(ImplementInterfaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates a constructor from fields/properties.
    /// </summary>
    /// <param name="request">The generate constructor request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the modified file or error information.</returns>
    Task<RefactoringResult> GenerateConstructorAsync(GenerateConstructorRequest request, CancellationToken ct = default);

    /// <summary>
    /// Extracts an interface from a class.
    /// </summary>
    /// <param name="request">The extract interface request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing created and modified files or error information.</returns>
    Task<RefactoringResult> ExtractInterfaceAsync(ExtractInterfaceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Safely deletes a symbol if it has no remaining references.
    /// </summary>
    /// <param name="request">The safe delete request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing deleted symbol info or error with remaining references.</returns>
    Task<RefactoringResult> SafeDeleteAsync(SafeDeleteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds a property to a class.
    /// </summary>
    /// <param name="request">The add property request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the modified file or error information.</returns>
    Task<RefactoringResult> AddPropertyAsync(AddPropertyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds a method to a class.
    /// </summary>
    /// <param name="request">The add method request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the modified file or error information.</returns>
    Task<RefactoringResult> AddMethodAsync(AddMethodRequest request, CancellationToken ct = default);
}

/// <summary>
/// Result of a refactoring operation.
/// </summary>
public sealed record RefactoringResult
{
    /// <summary>Whether the refactoring succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Human-readable message describing the result.</summary>
    public required string Message { get; init; }

    /// <summary>Files that were modified (paths).</summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    /// <summary>Files that were created (paths).</summary>
    public IReadOnlyList<string> CreatedFiles { get; init; } = [];

    /// <summary>Files that were deleted (paths).</summary>
    public IReadOnlyList<string> DeletedFiles { get; init; } = [];

    /// <summary>Preview of changes (when preview mode is enabled).</summary>
    public IReadOnlyList<FileChange>? Preview { get; init; }

    /// <summary>Error details if the operation failed.</summary>
    public string? Error { get; init; }

    /// <summary>Remaining references (for safe delete failures).</summary>
    public IReadOnlyList<SymbolReference>? RemainingReferences { get; init; }

    public static RefactoringResult Succeeded(string message, IReadOnlyList<string>? modifiedFiles = null) =>
        new() { Success = true, Message = message, ModifiedFiles = modifiedFiles ?? [] };

    public static RefactoringResult Failed(string error) =>
        new() { Success = false, Message = error, Error = error };
}

/// <summary>
/// Represents a change to a file.
/// </summary>
public sealed record FileChange(string FilePath, string OriginalContent, string NewContent);

/// <summary>
/// Represents a reference to a symbol.
/// </summary>
public sealed record SymbolReference(string FilePath, int Line, string CodeSnippet);

/// <summary>
/// Request to rename a symbol.
/// </summary>
public sealed record RenameSymbolRequest
{
    /// <summary>Current name of the symbol.</summary>
    public required string SymbolName { get; init; }

    /// <summary>New name for the symbol.</summary>
    public required string NewName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: type containing the symbol (for disambiguation).</summary>
    public string? ContainingType { get; init; }

    /// <summary>Optional: file path (for disambiguation).</summary>
    public string? FilePath { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to change a method signature.
/// </summary>
public sealed record ChangeSignatureRequest
{
    /// <summary>Name of the method to modify.</summary>
    public required string MethodName { get; init; }

    /// <summary>Type containing the method.</summary>
    public required string ContainingType { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Parameters to add.</summary>
    public IReadOnlyList<ParameterInfo>? AddParameters { get; init; }

    /// <summary>Parameter names to remove.</summary>
    public IReadOnlyList<string>? RemoveParameters { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Information about a method parameter.
/// </summary>
public sealed record ParameterInfo(string Name, string Type, string? DefaultValue = null);

/// <summary>
/// Request to implement an interface.
/// </summary>
public sealed record ImplementInterfaceRequest
{
    /// <summary>Name of the class to modify.</summary>
    public required string ClassName { get; init; }

    /// <summary>Name of the interface to implement.</summary>
    public required string InterfaceName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Use explicit interface implementation.</summary>
    public bool ExplicitImplementation { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to generate a constructor.
/// </summary>
public sealed record GenerateConstructorRequest
{
    /// <summary>Name of the class to modify.</summary>
    public required string ClassName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: specific members to initialize. If null, uses all readonly fields.</summary>
    public IReadOnlyList<string>? Members { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to extract an interface.
/// </summary>
public sealed record ExtractInterfaceRequest
{
    /// <summary>Name of the class to extract from.</summary>
    public required string ClassName { get; init; }

    /// <summary>Name for the new interface.</summary>
    public required string InterfaceName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: specific members to include. If null, uses all public members.</summary>
    public IReadOnlyList<string>? Members { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to safely delete a symbol.
/// </summary>
public sealed record SafeDeleteRequest
{
    /// <summary>Name of the symbol to delete.</summary>
    public required string SymbolName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: type containing the symbol.</summary>
    public string? ContainingType { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to add a property.
/// </summary>
public sealed record AddPropertyRequest
{
    /// <summary>Name of the class to modify.</summary>
    public required string ClassName { get; init; }

    /// <summary>Name of the new property.</summary>
    public required string PropertyName { get; init; }

    /// <summary>Type of the property.</summary>
    public required string PropertyType { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Whether to include a getter.</summary>
    public bool HasGetter { get; init; } = true;

    /// <summary>Whether to include a setter.</summary>
    public bool HasSetter { get; init; } = true;

    /// <summary>Optional initial value.</summary>
    public string? InitialValue { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to add a method.
/// </summary>
public sealed record AddMethodRequest
{
    /// <summary>Name of the class to modify.</summary>
    public required string ClassName { get; init; }

    /// <summary>Name of the new method.</summary>
    public required string MethodName { get; init; }

    /// <summary>Return type of the method.</summary>
    public required string ReturnType { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Method parameters.</summary>
    public IReadOnlyList<ParameterInfo>? Parameters { get; init; }

    /// <summary>Access modifier (public, private, etc.).</summary>
    public string AccessModifier { get; init; } = "public";

    /// <summary>Whether the method is async.</summary>
    public bool IsAsync { get; init; }

    /// <summary>Optional method body. If null, generates throw NotImplementedException().</summary>
    public string? Body { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}
