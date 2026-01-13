// <copyright file="JsonSchema.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.Text.Json;

/// <summary>
/// Represents a JSON schema for structured LLM output.
/// When provided, the LLM will be instructed to return JSON matching this schema.
/// </summary>
/// <param name="Name">A name for the schema (used by some providers).</param>
/// <param name="Schema">The JSON Schema object.</param>
/// <param name="Description">Optional description of the schema.</param>
/// <param name="Strict">Whether to enforce strict schema adherence (OpenAI feature).</param>
public sealed record JsonSchema(
    string Name,
    JsonElement Schema,
    string? Description = null,
    bool Strict = true);

/// <summary>
/// Options for chat completion requests.
/// </summary>
public sealed record ChatOptions
{
    /// <summary>
    /// When set, request structured JSON output matching this schema.
    /// Provider will use native JSON mode if available, otherwise falls back to prompt-based.
    /// </summary>
    public JsonSchema? ResponseSchema { get; init; }

    /// <summary>
    /// Temperature for sampling (0.0 to 2.0). Lower = more deterministic.
    /// </summary>
    public double Temperature { get; init; } = 0.7;
}
