// <copyright file="IPythonRefactoringService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for Python code refactoring operations using the rope library.
/// </summary>
public interface IPythonRefactoringService
{
    /// <summary>
    /// Rename a Python symbol and update all references.
    /// </summary>
    /// <param name="request">The rename request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<PythonRefactoringResult> RenameSymbolAsync(
        PythonRenameRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract a code region into a new method.
    /// </summary>
    /// <param name="request">The extract method request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<PythonRefactoringResult> ExtractMethodAsync(
        PythonExtractMethodRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract an expression into a variable.
    /// </summary>
    /// <param name="request">The extract variable request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<PythonRefactoringResult> ExtractVariableAsync(
        PythonExtractVariableRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all references to a Python symbol.
    /// </summary>
    /// <param name="request">The find references request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of reference locations.</returns>
    Task<PythonFindReferencesResult> FindReferencesAsync(
        PythonFindReferencesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the definition of a Python symbol.
    /// </summary>
    /// <param name="request">The find definition request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definition location.</returns>
    Task<PythonFindDefinitionResult> FindDefinitionAsync(
        PythonFindDefinitionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Python refactoring operation.
/// </summary>
public record PythonRefactoringResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Error type if failed.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Whether this was a preview only.</summary>
    public bool Preview { get; init; }

    /// <summary>List of changed file paths.</summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    /// <summary>Description of the changes.</summary>
    public string? Description { get; init; }

    /// <summary>Detailed file changes for preview mode.</summary>
    public IReadOnlyList<PythonFileChange>? FileChanges { get; init; }
}

/// <summary>
/// A file change from a Python refactoring operation.
/// </summary>
public record PythonFileChange
{
    /// <summary>Path to the changed file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Original file content.</summary>
    public string? OldContent { get; init; }

    /// <summary>New file content.</summary>
    public string? NewContent { get; init; }
}

/// <summary>
/// Request to rename a Python symbol.
/// </summary>
public record PythonRenameRequest
{
    /// <summary>Root path of the Python project.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol in the file.</summary>
    public required int Offset { get; init; }

    /// <summary>New name for the symbol.</summary>
    public required string NewName { get; init; }

    /// <summary>Whether to preview changes without applying them.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to extract code into a method.
/// </summary>
public record PythonExtractMethodRequest
{
    /// <summary>Root path of the Python project.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the code.</summary>
    public required string FilePath { get; init; }

    /// <summary>Start character offset of the code region.</summary>
    public required int StartOffset { get; init; }

    /// <summary>End character offset of the code region.</summary>
    public required int EndOffset { get; init; }

    /// <summary>Name for the new method.</summary>
    public required string NewName { get; init; }

    /// <summary>Whether to preview changes without applying them.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to extract an expression into a variable.
/// </summary>
public record PythonExtractVariableRequest
{
    /// <summary>Root path of the Python project.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the expression.</summary>
    public required string FilePath { get; init; }

    /// <summary>Start character offset of the expression.</summary>
    public required int StartOffset { get; init; }

    /// <summary>End character offset of the expression.</summary>
    public required int EndOffset { get; init; }

    /// <summary>Name for the new variable.</summary>
    public required string NewName { get; init; }

    /// <summary>Whether to preview changes without applying them.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to find references to a symbol.
/// </summary>
public record PythonFindReferencesRequest
{
    /// <summary>Root path of the Python project.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol in the file.</summary>
    public required int Offset { get; init; }
}

/// <summary>
/// Result of finding references.
/// </summary>
public record PythonFindReferencesResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>List of reference locations.</summary>
    public IReadOnlyList<PythonReference> References { get; init; } = [];

    /// <summary>Total count of references found.</summary>
    public int Count { get; init; }
}

/// <summary>
/// A reference to a Python symbol.
/// </summary>
public record PythonReference
{
    /// <summary>Path to the file containing the reference.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the reference.</summary>
    public required int Offset { get; init; }

    /// <summary>Whether this is the definition of the symbol.</summary>
    public bool IsDefinition { get; init; }

    /// <summary>Whether this is a write to the symbol.</summary>
    public bool IsWrite { get; init; }
}

/// <summary>
/// Request to find a symbol's definition.
/// </summary>
public record PythonFindDefinitionRequest
{
    /// <summary>Root path of the Python project.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol in the file.</summary>
    public required int Offset { get; init; }
}

/// <summary>
/// Result of finding a definition.
/// </summary>
public record PythonFindDefinitionResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Whether a definition was found.</summary>
    public bool Found { get; init; }

    /// <summary>Path to the file containing the definition.</summary>
    public string? FilePath { get; init; }

    /// <summary>Character offset of the definition.</summary>
    public int? Offset { get; init; }

    /// <summary>Line number of the definition (1-based).</summary>
    public int? Line { get; init; }

    /// <summary>Message if not found.</summary>
    public string? Message { get; init; }
}
