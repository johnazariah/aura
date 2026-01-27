// <copyright file="IDocsService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Services;

/// <summary>
/// Service for managing and retrieving Aura documentation.
/// </summary>
public interface IDocsService
{
    /// <summary>
    /// Lists documents, optionally filtered by category and tags.
    /// </summary>
    /// <param name="category">Optional category to filter by.</param>
    /// <param name="tags">Optional list of tags to filter by.</param>
    /// <returns>A read-only list of document entries matching the filters.</returns>
    IReadOnlyList<DocumentEntry> ListDocuments(string? category = null, IReadOnlyList<string>? tags = null);

    /// <summary>
    /// Retrieves the full content of a document by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the document.</param>
    /// <returns>The document content if found; otherwise, null.</returns>
    DocumentContent? GetDocument(string id);
}

/// <summary>
/// Represents a document entry with metadata.
/// </summary>
/// <param name="Id">The unique identifier of the document.</param>
/// <param name="Title">The title of the document.</param>
/// <param name="Summary">A brief summary of the document content.</param>
/// <param name="Category">The category the document belongs to.</param>
/// <param name="Tags">A read-only list of tags associated with the document.</param>
public record DocumentEntry(
    string Id,
    string Title,
    string Summary,
    string Category,
    IReadOnlyList<string> Tags);

/// <summary>
/// Represents the full content of a document.
/// </summary>
/// <param name="Id">The unique identifier of the document.</param>
/// <param name="Title">The title of the document.</param>
/// <param name="Category">The category the document belongs to.</param>
/// <param name="Tags">A read-only list of tags associated with the document.</param>
/// <param name="Content">The full content of the document.</param>
/// <param name="LastUpdated">The timestamp when the document was last updated.</param>
public record DocumentContent(
    string Id,
    string Title,
    string Category,
    IReadOnlyList<string> Tags,
    string Content,
    string LastUpdated);
