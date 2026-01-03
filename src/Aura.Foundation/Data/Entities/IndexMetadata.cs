// <copyright file="IndexMetadata.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Tracks metadata about indexing operations for freshness detection.
/// </summary>
public sealed class IndexMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the workspace/repository path that was indexed.
    /// </summary>
    public required string WorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets the type of index ('rag' or 'graph').
    /// </summary>
    public required string IndexType { get; set; }

    /// <summary>
    /// Gets or sets when the indexing occurred.
    /// </summary>
    public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the git commit SHA at time of indexing.
    /// </summary>
    public string? CommitSha { get; set; }

    /// <summary>
    /// Gets or sets the commit timestamp at time of indexing.
    /// </summary>
    public DateTimeOffset? CommitAt { get; set; }

    /// <summary>
    /// Gets or sets the number of files indexed.
    /// </summary>
    public int FilesIndexed { get; set; }

    /// <summary>
    /// Gets or sets the number of items created (chunks or nodes).
    /// </summary>
    public int ItemsCreated { get; set; }

    /// <summary>
    /// Gets or sets additional stats as JSON.
    /// </summary>
    public string? StatsJson { get; set; }
}

/// <summary>
/// Index types for IndexMetadata.
/// </summary>
public static class IndexTypes
{
    /// <summary>RAG text embedding index.</summary>
    public const string Rag = "rag";

    /// <summary>Code graph structural index.</summary>
    public const string Graph = "graph";
}
