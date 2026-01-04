// <copyright file="FunctionCalling.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

/// <summary>
/// Definition of a function that an LLM can call.
/// </summary>
/// <param name="Name">Function name (e.g., "file.read", "shell.execute").</param>
/// <param name="Description">Description of what the function does.</param>
/// <param name="Parameters">JSON Schema defining the function parameters.</param>
public sealed record FunctionDefinition(
    string Name,
    string Description,
    string Parameters);

/// <summary>
/// A function call requested by the LLM.
/// </summary>
/// <param name="Id">Optional unique identifier for the call (used by some providers).</param>
/// <param name="Name">The function name being called.</param>
/// <param name="ArgumentsJson">The function arguments as a JSON string.</param>
public sealed record FunctionCall(
    string? Id,
    string Name,
    string ArgumentsJson);

/// <summary>
/// Response from an LLM provider with function calling support.
/// </summary>
/// <param name="Content">The generated text content (may be null if only function calls).</param>
/// <param name="TokensUsed">Number of tokens consumed.</param>
/// <param name="Model">The model that generated the response.</param>
/// <param name="FinishReason">Why generation stopped (e.g., "stop", "tool_calls").</param>
/// <param name="FunctionCalls">List of function calls requested by the model.</param>
public sealed record LlmFunctionResponse(
    string? Content,
    int TokensUsed = 0,
    string? Model = null,
    string? FinishReason = null,
    IReadOnlyList<FunctionCall>? FunctionCalls = null)
{
    /// <summary>
    /// Gets a value indicating whether the model requested function calls.
    /// </summary>
    public bool HasFunctionCalls => FunctionCalls is { Count: > 0 };
}

/// <summary>
/// A function result message to send back to the LLM.
/// </summary>
/// <param name="CallId">The function call ID this result corresponds to.</param>
/// <param name="Name">The function name.</param>
/// <param name="Result">The JSON result from executing the function.</param>
public sealed record FunctionResultMessage(
    string? CallId,
    string Name,
    string Result);
