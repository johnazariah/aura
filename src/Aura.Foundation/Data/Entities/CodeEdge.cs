// <copyright file="CodeEdge.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents an edge (relationship) between two code nodes in the graph.
/// </summary>
public class CodeEdge
{
    /// <summary>Gets the unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the type of relationship.</summary>
    public required CodeEdgeType EdgeType { get; init; }

    /// <summary>Gets the source node ID.</summary>
    public Guid SourceId { get; init; }

    /// <summary>Gets the source node.</summary>
    public CodeNode? Source { get; init; }

    /// <summary>Gets the target node ID.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Gets the target node.</summary>
    public CodeNode? Target { get; init; }

    /// <summary>Gets additional properties as JSON.</summary>
    public string? PropertiesJson { get; init; }
}

/// <summary>
/// The type of relationship between code nodes.
/// </summary>
public enum CodeEdgeType
{
    /// <summary>Container contains child (Solution→Project, Project→File, File→Type, Type→Member).</summary>
    Contains,

    /// <summary>Namespace declares type.</summary>
    Declares,

    /// <summary>Class inherits from base class.</summary>
    Inherits,

    /// <summary>Type implements interface.</summary>
    Implements,

    /// <summary>Project references another project.</summary>
    References,

    /// <summary>Method calls another method.</summary>
    Calls,

    /// <summary>Member uses/depends on a type.</summary>
    Uses,

    /// <summary>Method overrides base method.</summary>
    Overrides,
}
