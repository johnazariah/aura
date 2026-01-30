// <copyright file="CodeModificationDto.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm.Schemas;

using System.Text.Json.Serialization;

/// <summary>
/// DTO for code modification responses.
/// Defines the structure for file edit operations.
/// </summary>
/// <remarks>
/// This DTO is the source of truth for the code modification JSON schema.
/// The schema in <see cref="WellKnownSchemas.CodeModification"/> is generated from this type.
/// </remarks>
public sealed record CodeModificationDto
{
    /// <summary>
    /// List of file operations to perform.
    /// </summary>
    [JsonPropertyName("files")]
    public required IReadOnlyList<FileOperationDto> Files { get; init; }

    /// <summary>
    /// Explanation of the changes being made.
    /// </summary>
    [JsonPropertyName("explanation")]
    public required string Explanation { get; init; }
}

/// <summary>
/// DTO for a single file operation.
/// </summary>
public sealed record FileOperationDto
{
    /// <summary>
    /// Path to the file.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Operation to perform: create, modify, or delete.
    /// </summary>
    [JsonPropertyName("operation")]
    public required FileOperationType Operation { get; init; }

    /// <summary>
    /// Full file content for create operations.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// Search and replace operations for modify operations.
    /// </summary>
    [JsonPropertyName("searchReplace")]
    public IReadOnlyList<SearchReplaceDto>? SearchReplace { get; init; }
}

/// <summary>
/// File operation types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FileOperationType>))]
public enum FileOperationType
{
    /// <summary>Create a new file.</summary>
    [JsonPropertyName("create")]
    Create,

    /// <summary>Modify an existing file.</summary>
    [JsonPropertyName("modify")]
    Modify,

    /// <summary>Delete an existing file.</summary>
    [JsonPropertyName("delete")]
    Delete,
}

/// <summary>
/// DTO for a search and replace operation.
/// </summary>
public sealed record SearchReplaceDto
{
    /// <summary>
    /// Text to search for.
    /// </summary>
    [JsonPropertyName("search")]
    public required string Search { get; init; }

    /// <summary>
    /// Text to replace with.
    /// </summary>
    [JsonPropertyName("replace")]
    public required string Replace { get; init; }
}
