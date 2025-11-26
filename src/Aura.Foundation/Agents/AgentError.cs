// <copyright file="AgentError.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Error from an agent execution.
/// </summary>
/// <param name="Code">Error code for programmatic handling.</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Details">Additional error details.</param>
public sealed record AgentError(AgentErrorCode Code, string Message, string? Details = null)
{
    /// <summary>
    /// Creates an error for agent not found.
    /// </summary>
    /// <param name="agentId">The agent ID that was not found.</param>
    /// <returns>An agent error.</returns>
    public static AgentError NotFound(string agentId) =>
        new(AgentErrorCode.NotFound, $"Agent '{agentId}' not found");

    /// <summary>
    /// Creates an error for execution failure.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Optional details.</param>
    /// <returns>An agent error.</returns>
    public static AgentError ExecutionFailed(string message, string? details = null) =>
        new(AgentErrorCode.ExecutionFailed, message, details);

    /// <summary>
    /// Creates an error for provider unavailable.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>An agent error.</returns>
    public static AgentError ProviderUnavailable(string provider) =>
        new(AgentErrorCode.ProviderUnavailable, $"LLM provider '{provider}' is not available");

    /// <summary>
    /// Creates an error for model not found.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <param name="provider">The provider name.</param>
    /// <returns>An agent error.</returns>
    public static AgentError ModelNotFound(string model, string provider) =>
        new(AgentErrorCode.ModelNotFound, $"Model '{model}' not found on provider '{provider}'");

    /// <summary>
    /// Creates an error for timeout.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>An agent error.</returns>
    public static AgentError Timeout(int timeoutSeconds) =>
        new(AgentErrorCode.Timeout, $"Agent execution timed out after {timeoutSeconds} seconds");

    /// <summary>
    /// Creates an error for cancellation.
    /// </summary>
    /// <returns>An agent error.</returns>
    public static AgentError Cancelled() =>
        new(AgentErrorCode.Cancelled, "Agent execution was cancelled");

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="message">The validation message.</param>
    /// <returns>An agent error.</returns>
    public static AgentError ValidationFailed(string message) =>
        new(AgentErrorCode.ValidationFailed, message);
}

/// <summary>
/// Error codes for agent operations.
/// </summary>
public enum AgentErrorCode
{
    /// <summary>Unknown error.</summary>
    Unknown = 0,

    /// <summary>Agent not found.</summary>
    NotFound,

    /// <summary>Execution failed.</summary>
    ExecutionFailed,

    /// <summary>LLM provider not available.</summary>
    ProviderUnavailable,

    /// <summary>Model not found on provider.</summary>
    ModelNotFound,

    /// <summary>Execution timed out.</summary>
    Timeout,

    /// <summary>Execution cancelled.</summary>
    Cancelled,

    /// <summary>Validation failed.</summary>
    ValidationFailed,
}
