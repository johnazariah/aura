// <copyright file="AgentError.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Exception thrown by agents.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AgentException"/> class.
/// </remarks>
/// <param name="code">Error code for programmatic handling.</param>
/// <param name="message">Human-readable error message.</param>
/// <param name="innerException">Optional inner exception.</param>
public sealed class AgentException(AgentErrorCode code, string message, Exception? innerException = null) : Exception(message, innerException)
{

    /// <summary>
    /// Gets the error code for programmatic handling.
    /// </summary>
    public AgentErrorCode Code { get; } = code;

    /// <summary>
    /// Creates an exception for agent not found.
    /// </summary>
    /// <param name="agentId">The agent ID that was not found.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException NotFound(string agentId) =>
        new(AgentErrorCode.NotFound, $"Agent '{agentId}' not found");

    /// <summary>
    /// Creates an exception for execution failure.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException ExecutionFailed(string message, Exception? innerException = null) =>
        new(AgentErrorCode.ExecutionFailed, message, innerException);

    /// <summary>
    /// Creates an exception for provider unavailable.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException ProviderUnavailable(string provider) =>
        new(AgentErrorCode.ProviderUnavailable, $"LLM provider '{provider}' is not available");

    /// <summary>
    /// Creates an exception for model not found.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <param name="provider">The provider name.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException ModelNotFound(string model, string provider) =>
        new(AgentErrorCode.ModelNotFound, $"Model '{model}' not found on provider '{provider}'");

    /// <summary>
    /// Creates an exception for timeout.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException Timeout(int timeoutSeconds) =>
        new(AgentErrorCode.Timeout, $"Agent execution timed out after {timeoutSeconds} seconds");

    /// <summary>
    /// Creates a validation exception.
    /// </summary>
    /// <param name="message">The validation message.</param>
    /// <returns>An agent exception.</returns>
    public static AgentException ValidationFailed(string message) =>
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

    /// <summary>Validation failed.</summary>
    ValidationFailed,
}
