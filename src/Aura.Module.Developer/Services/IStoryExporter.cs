// <copyright file="IStoryExporter.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for exporting story artifacts as markdown files.
/// </summary>
public interface IStoryExporter
{
    /// <summary>
    /// Exports a story's artifacts to the specified output path.
    /// </summary>
    /// <param name="storyId">The story ID.</param>
    /// <param name="request">Export configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The export result containing paths of exported files.</returns>
    Task<StoryExportResult> ExportAsync(Guid storyId, StoryExportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for story export.
/// </summary>
public record StoryExportRequest
{
    /// <summary>
    /// Gets or sets the output path. Defaults to ".project" in worktree.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Gets or sets the export format. Defaults to "sdd".
    /// </summary>
    public string Format { get; init; } = "sdd";

    /// <summary>
    /// Gets or sets which artifacts to include. Null means all.
    /// Valid values: "research", "plan", "changes", "review".
    /// </summary>
    public List<string>? Include { get; init; }
}

/// <summary>
/// Result of story export operation.
/// </summary>
public record StoryExportResult
{
    /// <summary>
    /// Gets the list of exported files.
    /// </summary>
    public required List<ExportedFile> Exported { get; init; }

    /// <summary>
    /// Gets any warnings encountered during export.
    /// </summary>
    public List<string>? Warnings { get; init; }
}

/// <summary>
/// Information about a single exported file.
/// </summary>
public record ExportedFile
{
    /// <summary>
    /// Gets the artifact type (e.g., "research", "plan", "changes").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the file path relative to the output root.
    /// </summary>
    public required string Path { get; init; }
}
