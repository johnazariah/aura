// <copyright file="ArtifactKeys.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Constants for agent artifact keys.
/// Used in <see cref="AgentOutput.Artifacts"/> dictionary.
/// </summary>
public static class ArtifactKeys
{
    /// <summary>Serialized semantic chunks from ingester agents.</summary>
    public const string Chunks = "chunks";

    /// <summary>Programming language of the content.</summary>
    public const string Language = "language";

    /// <summary>Parser used to process the content (e.g., "roslyn", "treesitter", "text").</summary>
    public const string Parser = "parser";
}
