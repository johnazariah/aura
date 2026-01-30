// <copyright file="ReActResponseDto.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm.Schemas;

using System.Text.Json.Serialization;

/// <summary>
/// DTO for ReAct agent responses.
/// Defines the thought-action-input structure used by the ReAct executor.
/// </summary>
/// <remarks>
/// This DTO is the source of truth for the ReAct response JSON schema.
/// The schema in <see cref="WellKnownSchemas.ReActResponse"/> is generated from this type.
/// </remarks>
public sealed record ReActResponseDto
{
    /// <summary>
    /// The reasoning step explaining the agent's thinking.
    /// </summary>
    [JsonPropertyName("thought")]
    public required string Thought { get; init; }

    /// <summary>
    /// The action to take (tool name or 'finish').
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>
    /// Parameters for the action (tool arguments or final answer).
    /// </summary>
    [JsonPropertyName("action_input")]
    public object? ActionInput { get; init; }
}
