// <copyright file="Workspace.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data.Entities;

/// <summary>
/// Represents a workspace (directory/repository) that has been onboarded to Aura.
/// A workspace is the top-level container for all indexed content.
/// </summary>
public sealed class Workspace
{
    /// <summary>
    /// Gets the unique identifier - SHA256 hash (first 16 chars) of the normalized path.
    /// Using a deterministic ID means the same path always maps to the same workspace.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the canonical (normalized) path of the workspace.
    /// Always lowercase with forward slashes via <see cref="Rag.PathNormalizer"/>.
    /// </summary>
    public required string CanonicalPath { get; init; }

    /// <summary>
    /// Gets or sets the display name (usually the directory name).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets when the workspace was first onboarded.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the workspace was last accessed or indexed.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the current onboarding/indexing status.
    /// </summary>
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Pending;

    /// <summary>
    /// Gets or sets the error message if <see cref="Status"/> is <see cref="WorkspaceStatus.Error"/>.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the Git remote URL if this is a git repository.
    /// </summary>
    public string? GitRemoteUrl { get; set; }

    /// <summary>
    /// Gets or sets the default branch name.
    /// </summary>
    public string? DefaultBranch { get; set; }
}

/// <summary>
/// Workspace lifecycle status.
/// </summary>
public enum WorkspaceStatus
{
    /// <summary>Registered but not yet indexed.</summary>
    Pending = 0,

    /// <summary>Currently being indexed.</summary>
    Indexing = 1,

    /// <summary>Successfully indexed and ready.</summary>
    Ready = 2,

    /// <summary>Indexing failed.</summary>
    Error = 3,

    /// <summary>Index is stale (commits since last index).</summary>
    Stale = 4,
}
