// <copyright file="ITypeScriptLanguageService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for TypeScript/JavaScript code refactoring operations using ts-morph.
/// </summary>
public interface ITypeScriptLanguageService
{
    /// <summary>
    /// Rename a TypeScript/JavaScript symbol and update all references.
    /// </summary>
    /// <param name="request">The rename request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<TypeScriptRefactoringResult> RenameSymbolAsync(
        TypeScriptRenameRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract a code region into a new function.
    /// </summary>
    /// <param name="request">The extract function request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<TypeScriptRefactoringResult> ExtractFunctionAsync(
        TypeScriptExtractFunctionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract an expression into a variable.
    /// </summary>
    /// <param name="request">The extract variable request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the refactoring operation.</returns>
    Task<TypeScriptRefactoringResult> ExtractVariableAsync(
        TypeScriptExtractVariableRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all references to a TypeScript/JavaScript symbol.
    /// </summary>
    /// <param name="request">The find references request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of reference locations.</returns>
    Task<TypeScriptFindReferencesResult> FindReferencesAsync(
        TypeScriptFindReferencesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the definition of a TypeScript/JavaScript symbol.
    /// </summary>
    /// <param name="request">The find definition request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definition location.</returns>
    Task<TypeScriptFindDefinitionResult> FindDefinitionAsync(
        TypeScriptFindDefinitionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspect a TypeScript/JavaScript type and return its members.
    /// </summary>
    /// <param name="request">The inspect type request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Type information with members.</returns>
    Task<TypeScriptInspectTypeResult> InspectTypeAsync(
        TypeScriptInspectTypeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all types in a TypeScript/JavaScript project.
    /// </summary>
    /// <param name="request">The list types request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of types found.</returns>
    Task<TypeScriptListTypesResult> ListTypesAsync(
        TypeScriptListTypesRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a TypeScript refactoring operation.
/// </summary>
public record TypeScriptRefactoringResult
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
    public IReadOnlyList<string>? ChangedFiles { get; init; }

    /// <summary>Description of the change.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Request to rename a TypeScript/JavaScript symbol.
/// </summary>
public record TypeScriptRenameRequest
{
    /// <summary>Path to the project root (containing tsconfig.json).</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol in the file.</summary>
    public required int Offset { get; init; }

    /// <summary>New name for the symbol.</summary>
    public required string NewName { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to extract code into a function.
/// </summary>
public record TypeScriptExtractFunctionRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Start character offset of the code to extract.</summary>
    public required int StartOffset { get; init; }

    /// <summary>End character offset of the code to extract.</summary>
    public required int EndOffset { get; init; }

    /// <summary>Name for the new function.</summary>
    public required string NewName { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to extract an expression into a variable.
/// </summary>
public record TypeScriptExtractVariableRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Start character offset of the expression.</summary>
    public required int StartOffset { get; init; }

    /// <summary>End character offset of the expression.</summary>
    public required int EndOffset { get; init; }

    /// <summary>Name for the new variable.</summary>
    public required string NewName { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to find references to a symbol.
/// </summary>
public record TypeScriptFindReferencesRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol.</summary>
    public required int Offset { get; init; }
}

/// <summary>
/// Request to find the definition of a symbol.
/// </summary>
public record TypeScriptFindDefinitionRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Path to the file containing the symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Character offset of the symbol.</summary>
    public required int Offset { get; init; }
}

/// <summary>
/// Result of finding references.
/// </summary>
public record TypeScriptFindReferencesResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>List of reference locations.</summary>
    public IReadOnlyList<TypeScriptReferenceLocation>? References { get; init; }

    /// <summary>Total count of references.</summary>
    public int Count { get; init; }
}

/// <summary>
/// Location of a reference.
/// </summary>
public record TypeScriptReferenceLocation
{
    /// <summary>File path.</summary>
    public required string File { get; init; }

    /// <summary>Line number (1-based).</summary>
    public required int Line { get; init; }

    /// <summary>Column number (1-based).</summary>
    public required int Column { get; init; }

    /// <summary>Text at the reference.</summary>
    public string? Text { get; init; }
}

/// <summary>
/// Result of finding a definition.
/// </summary>
public record TypeScriptFindDefinitionResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Whether a definition was found.</summary>
    public bool Found { get; init; }

    /// <summary>File path of the definition.</summary>
    public string? FilePath { get; init; }

    /// <summary>Line number (1-based).</summary>
    public int? Line { get; init; }

    /// <summary>Column number (1-based).</summary>
    public int? Column { get; init; }

    /// <summary>Character offset of the definition.</summary>
    public int? Offset { get; init; }

    /// <summary>Message if not found.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Request to inspect a TypeScript/JavaScript type.
/// </summary>
public record TypeScriptInspectTypeRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Name of the type to inspect.</summary>
    public required string TypeName { get; init; }

    /// <summary>Optional file path to narrow the search.</summary>
    public string? FilePath { get; init; }
}

/// <summary>
/// Result of inspecting a type.
/// </summary>
public record TypeScriptInspectTypeResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Name of the type.</summary>
    public string? TypeName { get; init; }

    /// <summary>Kind of type (class, interface, enum, type).</summary>
    public string? Kind { get; init; }

    /// <summary>File path where the type is defined.</summary>
    public string? FilePath { get; init; }

    /// <summary>Line number of the type declaration.</summary>
    public int? Line { get; init; }

    /// <summary>Members of the type.</summary>
    public IReadOnlyList<TypeScriptMemberInfo>? Members { get; init; }
}

/// <summary>
/// Information about a type member.
/// </summary>
public record TypeScriptMemberInfo
{
    /// <summary>Name of the member.</summary>
    public required string Name { get; init; }

    /// <summary>Kind of member (property, method, constructor, getter, setter, enum-member).</summary>
    public required string Kind { get; init; }

    /// <summary>Type of the member.</summary>
    public string? Type { get; init; }

    /// <summary>Visibility (public, private, protected).</summary>
    public string? Visibility { get; init; }

    /// <summary>Whether the member is static.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether the member is async.</summary>
    public bool IsAsync { get; init; }

    /// <summary>Line number of the member.</summary>
    public int Line { get; init; }
}

/// <summary>
/// Request to list types in a TypeScript/JavaScript project.
/// </summary>
public record TypeScriptListTypesRequest
{
    /// <summary>Path to the project root.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Optional filter by type name (partial match).</summary>
    public string? NameFilter { get; init; }
}

/// <summary>
/// Result of listing types.
/// </summary>
public record TypeScriptListTypesResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>List of types found.</summary>
    public IReadOnlyList<TypeScriptTypeInfo>? Types { get; init; }

    /// <summary>Total count of types.</summary>
    public int Count { get; init; }
}

/// <summary>
/// Information about a type in the project.
/// </summary>
public record TypeScriptTypeInfo
{
    /// <summary>Name of the type.</summary>
    public required string Name { get; init; }

    /// <summary>Kind of type (class, interface, enum, type).</summary>
    public required string Kind { get; init; }

    /// <summary>File path where the type is defined.</summary>
    public required string FilePath { get; init; }

    /// <summary>Line number of the type declaration.</summary>
    public int Line { get; init; }

    /// <summary>Whether the type is exported.</summary>
    public bool IsExported { get; init; }

    /// <summary>Number of members in the type.</summary>
    public int MemberCount { get; init; }
}
