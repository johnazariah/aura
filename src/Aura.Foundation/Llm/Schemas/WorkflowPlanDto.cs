// <copyright file="WorkflowPlanDto.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm.Schemas;

using System.Text.Json.Serialization;

/// <summary>
/// DTO for workflow plan responses.
/// Defines the structure for multi-step workflow planning.
/// </summary>
/// <remarks>
/// This DTO is the source of truth for the workflow plan JSON schema.
/// The schema in <see cref="WellKnownSchemas.WorkflowPlan"/> is generated from this type.
/// </remarks>
public sealed record WorkflowPlanDto
{
    /// <summary>
    /// List of workflow steps to execute in order.
    /// </summary>
    [JsonPropertyName("steps")]
    public required IReadOnlyList<WorkflowStepDto> Steps { get; init; }
}

/// <summary>
/// DTO for a single workflow step.
/// </summary>
public sealed record WorkflowStepDto
{
    /// <summary>
    /// Short name for the step (e.g. 'Implement UserService').
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The capability needed: coding, review, documentation, analysis, fixing, or enrichment.
    /// </summary>
    [JsonPropertyName("capability")]
    public required string Capability { get; init; }

    /// <summary>
    /// Programming language if applicable (e.g. 'csharp', 'python', 'typescript').
    /// </summary>
    [JsonPropertyName("language")]
    public required string Language { get; init; }

    /// <summary>
    /// Detailed description of what this step should accomplish.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
