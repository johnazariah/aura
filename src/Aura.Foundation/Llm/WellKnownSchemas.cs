using System.Text.Json;
using Aura.Foundation.Llm.Schemas;

namespace Aura.Foundation.Llm;

/// <summary>
/// Common JSON schemas for structured LLM output.
/// These schemas can be used with providers that support schema enforcement
/// (OpenAI, Azure OpenAI) to guarantee valid structured output.
/// </summary>
/// <remarks>
/// Schemas are generated from DTO types in the <see cref="Schemas"/> namespace.
/// This ensures the schemas stay in sync with the actual types used for parsing responses.
/// </remarks>
public static class WellKnownSchemas
{
    /// <summary>
    /// Schema for ReAct agent responses.
    /// Defines the thought-action-input structure used by the ReAct executor.
    /// </summary>
    /// <remarks>
    /// Generated from <see cref="ReActResponseDto"/>.
    /// </remarks>
    public static JsonSchema ReActResponse { get; } = JsonSchemaGenerator.CreateSchema<ReActResponseDto>(
        name: "react_response",
        description: "ReAct agent response with thought, action, and parameters",
        strict: true);

    /// <summary>
    /// Schema for workflow plan responses.
    /// Defines the structure for multi-step workflow planning.
    /// </summary>
    /// <remarks>
    /// Generated from <see cref="WorkflowPlanDto"/>.
    /// </remarks>
    public static JsonSchema WorkflowPlan { get; } = JsonSchemaGenerator.CreateSchema<WorkflowPlanDto>(
        name: "workflow_plan",
        description: "Multi-step workflow plan with capability-based steps",
        strict: true);

    /// <summary>
    /// Schema for code modification responses.
    /// Defines the structure for file edit operations.
    /// </summary>
    /// <remarks>
    /// Generated from <see cref="CodeModificationDto"/>.
    /// </remarks>
    public static JsonSchema CodeModification { get; } = JsonSchemaGenerator.CreateSchema<CodeModificationDto>(
        name: "code_modification",
        description: "Code modification with file operations",
        strict: true);
}
