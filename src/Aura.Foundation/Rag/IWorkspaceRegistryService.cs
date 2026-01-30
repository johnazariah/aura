// <copyright file="IWorkspaceRegistryService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Service for managing the workspace registry - a list of indexed workspaces
/// that can be queried together for multi-workspace search.
/// </summary>
public interface IWorkspaceRegistryService
{
    /// <summary>
    /// Lists all registered workspaces.
    /// </summary>
    /// <returns>All registered workspaces.</returns>
    IReadOnlyList<RegisteredWorkspace> ListWorkspaces();

    /// <summary>
    /// Gets a workspace by ID or alias.
    /// </summary>
    /// <param name="idOrAlias">The workspace ID or alias.</param>
    /// <returns>The workspace, or null if not found.</returns>
    RegisteredWorkspace? GetWorkspace(string idOrAlias);

    /// <summary>
    /// Gets the default workspace.
    /// </summary>
    /// <returns>The default workspace, or null if none is set.</returns>
    RegisteredWorkspace? GetDefaultWorkspace();

    /// <summary>
    /// Adds a workspace to the registry.
    /// </summary>
    /// <param name="path">The workspace path.</param>
    /// <param name="alias">Optional short alias.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>The registered workspace.</returns>
    RegisteredWorkspace AddWorkspace(string path, string? alias = null, IReadOnlyList<string>? tags = null);

    /// <summary>
    /// Removes a workspace from the registry.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool RemoveWorkspace(string id);

    /// <summary>
    /// Sets the default workspace.
    /// </summary>
    /// <param name="id">The workspace ID to set as default.</param>
    /// <returns>True if set, false if workspace not found.</returns>
    bool SetDefault(string id);

    /// <summary>
    /// Resolves workspace IDs from a list that may contain aliases or wildcards.
    /// </summary>
    /// <param name="workspaceRefs">Workspace references (IDs, aliases, or "*" for all).</param>
    /// <returns>Resolved workspace IDs.</returns>
    IReadOnlyList<string> ResolveWorkspaceIds(IReadOnlyList<string> workspaceRefs);
}

/// <summary>
/// A workspace registered for multi-workspace queries.
/// </summary>
/// <param name="Id">The workspace ID (16-char hex hash).</param>
/// <param name="Path">The canonical path.</param>
/// <param name="Alias">Optional short alias.</param>
/// <param name="Tags">Tags for categorization.</param>
public sealed record RegisteredWorkspace(
    string Id,
    string Path,
    string? Alias,
    IReadOnlyList<string> Tags)
{
    /// <summary>Gets or sets whether this workspace is indexed.</summary>
    public bool Indexed { get; init; }

    /// <summary>Gets or sets the chunk count.</summary>
    public int ChunkCount { get; init; }

    /// <summary>Gets or sets when the workspace was last indexed.</summary>
    public DateTimeOffset? LastIndexed { get; init; }
}
