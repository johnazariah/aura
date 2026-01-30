// <copyright file="ChunkMetadataKeys.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Constants for chunk metadata keys.
/// These correspond to the JSON property names in <see cref="SemanticChunk"/>.
/// </summary>
public static class ChunkMetadataKeys
{
    /// <summary>Chunk type (class, method, function, etc.).</summary>
    public const string ChunkType = "chunkType";

    /// <summary>Symbol name (class name, method name, etc.).</summary>
    public const string SymbolName = "symbolName";

    /// <summary>Fully qualified name.</summary>
    public const string FullyQualifiedName = "fullyQualifiedName";

    /// <summary>Method/function signature.</summary>
    public const string Signature = "signature";

    /// <summary>Parent symbol (containing class, etc.).</summary>
    public const string ParentSymbol = "parentSymbol";

    /// <summary>Programming language.</summary>
    public const string Language = "language";

    /// <summary>Start line number.</summary>
    public const string StartLine = "startLine";

    /// <summary>End line number.</summary>
    public const string EndLine = "endLine";

    /// <summary>Title (for documentation chunks).</summary>
    public const string Title = "title";
}
