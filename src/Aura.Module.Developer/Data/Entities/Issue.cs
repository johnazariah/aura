// <copyright file="Issue.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Data.Entities;

/// <summary>
/// A local issue - the starting point for a development workflow.
/// This is the entry point for the Developer Module's workflow automation.
/// </summary>
public sealed class Issue
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the issue title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the issue description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the issue status.</summary>
    public IssueStatus Status { get; set; } = IssueStatus.Open;

    /// <summary>Gets or sets the repository path this issue relates to.</summary>
    public string? RepositoryPath { get; set; }

    /// <summary>Gets or sets when the issue was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the issue was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the associated workflow (one issue → one workflow).</summary>
    public Workflow? Workflow { get; set; }
}

/// <summary>
/// The status of an issue.
/// </summary>
public enum IssueStatus
{
    /// <summary>Issue is open and available for workflow.</summary>
    Open,

    /// <summary>Workflow has been created for this issue.</summary>
    InProgress,

    /// <summary>Workflow completed successfully.</summary>
    Completed,

    /// <summary>Manually closed without completion.</summary>
    Closed,
}
