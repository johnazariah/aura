// <copyright file="CodeNode.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents a node in the code graph (solution, project, type, member, etc.).
/// </summary>
public class CodeNode
{
    /// <summary>Gets the unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the type of node.</summary>
    public required CodeNodeType NodeType { get; init; }

    /// <summary>Gets the simple name (e.g., "WorkflowService").</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fully qualified name (e.g., "Aura.Foundation.Services.WorkflowService").</summary>
    public string? FullName { get; init; }

    /// <summary>Gets the file path where this node is defined.</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets the line number where this node starts.</summary>
    public int? LineNumber { get; init; }

    /// <summary>Gets the signature (for methods/properties).</summary>
    public string? Signature { get; init; }

    /// <summary>Gets the modifiers (public, abstract, static, etc.).</summary>
    public string? Modifiers { get; init; }

    /// <summary>Gets the workspace path this node belongs to (for multi-worktree isolation).</summary>
    public string? WorkspacePath { get; init; }

    /// <summary>Gets additional properties as JSON.</summary>
    public string? PropertiesJson { get; init; }

    /// <summary>Gets the embedding vector for semantic search.</summary>
    public float[]? Embedding { get; init; }

    /// <summary>Gets when this node was indexed.</summary>
    public DateTimeOffset IndexedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the outgoing edges from this node.</summary>
    public ICollection<CodeEdge> OutgoingEdges { get; init; } = [];

    /// <summary>Gets the incoming edges to this node.</summary>
    public ICollection<CodeEdge> IncomingEdges { get; init; } = [];
}

/// <summary>
/// The type of a code node in the graph.
/// </summary>
public enum CodeNodeType
{
    /// <summary>A solution (.sln) file.</summary>
    Solution,

    /// <summary>A project (.csproj) file.</summary>
    Project,

    /// <summary>A source file.</summary>
    File,

    /// <summary>A namespace.</summary>
    Namespace,

    /// <summary>A class.</summary>
    Class,

    /// <summary>An interface.</summary>
    Interface,

    /// <summary>A record type.</summary>
    Record,

    /// <summary>A struct.</summary>
    Struct,

    /// <summary>An enum.</summary>
    Enum,

    /// <summary>A method.</summary>
    Method,

    /// <summary>A property.</summary>
    Property,

    /// <summary>A field.</summary>
    Field,

    /// <summary>An event.</summary>
    Event,

    /// <summary>A constructor.</summary>
    Constructor,
}
