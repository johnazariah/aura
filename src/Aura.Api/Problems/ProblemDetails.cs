// <copyright file="ProblemDetails.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Problems;

using System.Text.Json.Serialization;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs.
/// </summary>
public record ProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>
    /// Optional trace ID for debugging.
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}

/// <summary>
/// Problem details with validation errors.
/// </summary>
public record ValidationProblemDetails : ProblemDetails
{
    /// <summary>
    /// Validation errors by field name.
    /// </summary>
    [JsonPropertyName("errors")]
    public IDictionary<string, string[]>? Errors { get; init; }
}
