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
    /// Analyzes the blast radius of a rename operation without executing it.
    /// Discovers related symbols and reference counts.
    /// </summary>
    /// <param name="request">The rename request (only SymbolName and SolutionPath are used).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Blast radius analysis with related symbols and suggested plan.</returns>
    Task<BlastRadiusResult> AnalyzeRenameAsync(RenameSymbolRequest request, CancellationToken ct = default);

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

    /// <summary>
    /// Moves a type to its own file with matching name.
    /// If the source file contains only this type, uses git mv to preserve history.
    /// If the source file contains multiple types, extracts the type to a new file.
    /// </summary>
    /// <param name="request">The move type request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing created/modified/deleted files or error information.</returns>
    Task<RefactoringResult> MoveTypeToFileAsync(MoveTypeToFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new type file with proper namespace inferred from project structure.
    /// </summary>
    /// <param name="request">The create type request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the created file or error information.</returns>
    Task<RefactoringResult> CreateTypeAsync(CreateTypeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Moves specified members from a class to a new partial class file.
    /// Ensures the source class has the partial modifier.
    /// </summary>
    /// <param name="request">The move members request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing created/modified files or error information.</returns>
    Task<RefactoringResult> MoveMembersToPartialAsync(MoveMembersToPartialRequest request, CancellationToken ct = default);
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

    /// <summary>Build validation result (when validate=true).</summary>
    public ValidationResult? Validation { get; init; }

    public static RefactoringResult Succeeded(string message, IReadOnlyList<string>? modifiedFiles = null) =>
        new() { Success = true, Message = message, ModifiedFiles = modifiedFiles ?? [] };

    public static RefactoringResult Failed(string error) =>
        new() { Success = false, Message = error, Error = error };
}

/// <summary>
/// Result of post-refactoring build validation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>Whether the build succeeded.</summary>
    public required bool BuildSucceeded { get; init; }

    /// <summary>Build output (errors/warnings).</summary>
    public string? BuildOutput { get; init; }

    /// <summary>Residual occurrences of the old symbol name found via grep.</summary>
    public IReadOnlyList<string>? Residuals { get; init; }
}

/// <summary>
/// Result of blast radius analysis for a refactoring operation.
/// </summary>
public sealed record BlastRadiusResult
{
    /// <summary>The operation being analyzed.</summary>
    public required string Operation { get; init; }

    /// <summary>The primary symbol being refactored.</summary>
    public required string Symbol { get; init; }

    /// <summary>The new name (for rename operations).</summary>
    public string? NewName { get; init; }

    /// <summary>Related symbols discovered by naming convention.</summary>
    public required IReadOnlyList<RelatedSymbol> RelatedSymbols { get; init; }

    /// <summary>Total number of references across all related symbols.</summary>
    public int TotalReferences { get; init; }

    /// <summary>Number of files that will be modified.</summary>
    public int FilesAffected { get; init; }

    /// <summary>Number of files that should be renamed.</summary>
    public int FilesToRename { get; init; }

    /// <summary>Suggested sequence of operations.</summary>
    public required IReadOnlyList<SuggestedOperation> SuggestedPlan { get; init; }

    /// <summary>Whether this result awaits user confirmation before execution.</summary>
    public bool AwaitsConfirmation { get; init; } = true;

    /// <summary>Error message if analysis failed.</summary>
    public string? Error { get; init; }

    /// <summary>Whether the analysis succeeded.</summary>
    public bool Success => Error is null;
}

/// <summary>
/// A symbol related to the primary refactoring target.
/// </summary>
public sealed record RelatedSymbol
{
    /// <summary>Symbol name.</summary>
    public required string Name { get; init; }

    /// <summary>Symbol kind (class, interface, enum, method, etc.).</summary>
    public required string Kind { get; init; }

    /// <summary>File containing the symbol definition.</summary>
    public required string FilePath { get; init; }

    /// <summary>Number of references to this symbol.</summary>
    public int ReferenceCount { get; init; }

    /// <summary>Suggested new name (for cascade renames).</summary>
    public string? SuggestedNewName { get; init; }
}

/// <summary>
/// A suggested operation in the refactoring plan.
/// </summary>
public sealed record SuggestedOperation
{
    /// <summary>Order in the sequence.</summary>
    public required int Order { get; init; }

    /// <summary>Operation type (rename, rename_file, etc.).</summary>
    public required string Operation { get; init; }

    /// <summary>Target symbol or file.</summary>
    public required string Target { get; init; }

    /// <summary>New name or destination.</summary>
    public required string NewValue { get; init; }

    /// <summary>Number of references affected.</summary>
    public int ReferenceCount { get; init; }
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

    /// <summary>If true, run build after refactoring and check for residuals.</summary>
    public bool Validate { get; init; }
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
/// Information about a generic type parameter.
/// </summary>
/// <param name="Name">The type parameter name (e.g., "T", "TEntity").</param>
/// <param name="Constraints">Constraints for the type parameter (e.g., "class", "new()", "IEntity").</param>
public sealed record TypeParameterInfo(string Name, IReadOnlyList<string>? Constraints = null);

/// <summary>
/// Information about an attribute to apply to a member.
/// </summary>
/// <param name="Name">The attribute name (e.g., "JsonPropertyName", "Required", "HttpGet").</param>
/// <param name="Arguments">Optional: attribute arguments as strings (e.g., "\"user_name\"", "typeof(User)").</param>
public sealed record AttributeInfo(string Name, IReadOnlyList<string>? Arguments = null);

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

    /// <summary>Access modifier (e.g., "public", "private", "internal", "protected").</summary>
    public string AccessModifier { get; init; } = "public";

    /// <summary>Whether to include a getter.</summary>
    public bool HasGetter { get; init; } = true;

    /// <summary>Whether to include a setter.</summary>
    public bool HasSetter { get; init; } = true;

    /// <summary>Optional initial value.</summary>
    public string? InitialValue { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }

    /// <summary>If true, generate a field instead of a property.</summary>
    public bool IsField { get; init; }

    /// <summary>If true, add readonly modifier (for fields).</summary>
    public bool IsReadonly { get; init; }

    /// <summary>If true, add static modifier.</summary>
    public bool IsStatic { get; init; }

    /// <summary>If true, add required modifier (C# 11+).</summary>
    public bool IsRequired { get; init; }

    /// <summary>If true, use init accessor instead of set (C# 9+).</summary>
    public bool HasInit { get; init; }

    /// <summary>Attributes to apply to the property.</summary>
    public IReadOnlyList<AttributeInfo>? Attributes { get; init; }

    /// <summary>XML documentation summary for the property or field.</summary>
    public string? Documentation { get; init; }
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

    /// <summary>Test attribute to add (Fact, Test, TestMethod). If null, auto-detects for test classes.</summary>
    public string? TestAttribute { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }

    /// <summary>If true, add static modifier.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Method modifier: virtual, override, abstract, sealed, or new.</summary>
    public string? MethodModifier { get; init; }

    /// <summary>
    /// Generic type parameters with optional constraints.
    /// Example: T Create&lt;T&gt;() where T : new()
    /// </summary>
    public IReadOnlyList<TypeParameterInfo>? TypeParameters { get; init; }

    /// <summary>Attributes to apply to the method.</summary>
    public IReadOnlyList<AttributeInfo>? Attributes { get; init; }

    /// <summary>If true, generate as extension method (first parameter gets 'this' modifier).</summary>
    public bool IsExtension { get; init; }

    /// <summary>XML documentation summary for the method.</summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// Request to move a type to its own file.
/// </summary>
public sealed record MoveTypeToFileRequest
{
    /// <summary>Name of the type to move.</summary>
    public required string TypeName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Optional: target directory. If null, uses the same directory as source.</summary>
    public string? TargetDirectory { get; init; }

    /// <summary>Optional: target filename. If null, uses {TypeName}.cs.</summary>
    public string? TargetFileName { get; init; }

    /// <summary>If true, use git mv when possible to preserve history.</summary>
    public bool UseGitMove { get; init; } = true;

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }
}

/// <summary>
/// Request to create a new type file.
/// </summary>
public sealed record CreateTypeRequest
{
    /// <summary>Name of the type to create.</summary>
    public required string TypeName { get; init; }

    /// <summary>Kind of type: class, interface, record, struct, enum.</summary>
    public required string TypeKind { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Target directory for the new file.</summary>
    public required string TargetDirectory { get; init; }

    /// <summary>Optional: explicit namespace. If null, inferred from project + directory.</summary>
    public string? Namespace { get; init; }

    /// <summary>Optional: base class to inherit from.</summary>
    public string? BaseClass { get; init; }

    /// <summary>Optional: interfaces to implement.</summary>
    public IReadOnlyList<string>? Interfaces { get; init; }

    /// <summary>Optional: access modifier (public, internal). Default: public.</summary>
    public string AccessModifier { get; init; } = "public";

    /// <summary>Optional: whether class is sealed. Default: false.</summary>
    public bool IsSealed { get; init; }

    /// <summary>Optional: whether class is abstract. Default: false.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>Optional: whether class is static. Default: false.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Optional: whether record is a struct. Default: false.</summary>
    public bool IsRecordStruct { get; init; }

    /// <summary>Optional: additional using directives to add.</summary>
    public IReadOnlyList<string>? AdditionalUsings { get; init; }

    /// <summary>Optional: XML documentation summary.</summary>
    public string? DocumentationSummary { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }

    /// <summary>
    /// Primary constructor parameters (C# 12 for classes, C# 9 for records).
    /// For records, these become positional parameters: record Person(string Name, int Age).
    /// For classes, these become primary constructor: class Service(ILogger logger).
    /// </summary>
    public IReadOnlyList<ParameterInfo>? PrimaryConstructorParameters { get; init; }

    /// <summary>
    /// Generic type parameters with optional constraints.
    /// Example: Repository&lt;TEntity&gt; where TEntity : class, IEntity
    /// </summary>
    public IReadOnlyList<TypeParameterInfo>? TypeParameters { get; init; }

    /// <summary>
    /// Optional: attributes to apply to the type (e.g., [ApiController]).
    /// </summary>
    public IReadOnlyList<AttributeInfo>? Attributes { get; init; }

    /// <summary>
    /// Optional: enum member names (for typeKind=enum).
    /// Example: ["None", "Success", "Error", "Warning"]
    /// </summary>
    public IReadOnlyList<string>? EnumMembers { get; init; }
}


/// <summary>
/// Request to move members from a class to a new partial class file.
/// </summary>
public sealed record MoveMembersToPartialRequest
{
    /// <summary>Name of the class containing the members.</summary>
    public required string ClassName { get; init; }

    /// <summary>Path to the solution file.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Names of members (methods, properties, fields) to move.</summary>
    public required IReadOnlyList<string> MemberNames { get; init; }

    /// <summary>Target filename for the partial class (e.g., "McpHandler.Edit.cs").</summary>
    public required string TargetFileName { get; init; }

    /// <summary>Optional: target directory. If null, uses the same directory as source.</summary>
    public string? TargetDirectory { get; init; }

    /// <summary>If true, return preview without applying changes.</summary>
    public bool Preview { get; init; }

    /// <summary>If true, make source class partial if not already.</summary>
    public bool EnsureSourceIsPartial { get; init; } = true;
}
