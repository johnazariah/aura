// <copyright file="IIssueService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Service for managing local issues.
/// </summary>
public interface IIssueService
{
    /// <summary>
    /// Creates a new issue.
    /// </summary>
    /// <param name="title">The issue title.</param>
    /// <param name="description">The issue description (optional).</param>
    /// <param name="repositoryPath">The repository path (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created issue.</returns>
    Task<Issue> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets an issue by ID.
    /// </summary>
    /// <param name="id">The issue ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The issue if found, null otherwise.</returns>
    Task<Issue?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets an issue by ID with its workflow.
    /// </summary>
    /// <param name="id">The issue ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The issue with workflow if found, null otherwise.</returns>
    Task<Issue?> GetByIdWithWorkflowAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all issues, optionally filtered by status.
    /// </summary>
    /// <param name="status">Filter by status (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of issues.</returns>
    Task<IReadOnlyList<Issue>> ListAsync(IssueStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// Updates an issue.
    /// </summary>
    /// <param name="id">The issue ID.</param>
    /// <param name="title">New title (null to keep existing).</param>
    /// <param name="description">New description (null to keep existing).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated issue.</returns>
    Task<Issue> UpdateAsync(
        Guid id,
        string? title = null,
        string? description = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an issue.
    /// </summary>
    /// <param name="id">The issue ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Closes an issue without completing it.
    /// </summary>
    /// <param name="id">The issue ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The closed issue.</returns>
    Task<Issue> CloseAsync(Guid id, CancellationToken ct = default);
}
