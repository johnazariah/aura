// <copyright file="ILlmProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Aura.Foundation.Agents;

/// <summary>
/// Interface for LLM providers (Ollama, MAF, etc.).
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the unique identifier for this provider.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Generates a completion from a prompt.
    /// </summary>
    /// <param name="model">The model to use. If null, uses provider's default.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="temperature">Temperature for sampling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response.</returns>
    /// <exception cref="LlmException">Thrown when generation fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
    Task<LlmResponse> GenerateAsync(
        string? model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a completion from a chat conversation.
    /// </summary>
    /// <param name="model">The model to use. If null, uses provider's default.</param>
    /// <param name="messages">The conversation messages.</param>
    /// <param name="temperature">Temperature for sampling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response.</returns>
    /// <exception cref="LlmException">Thrown when generation fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
    Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a model is available on this provider.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the model is available.</returns>
    Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available models on this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available models.</returns>
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from an LLM provider.
/// </summary>
/// <param name="Content">The generated text content.</param>
/// <param name="TokensUsed">Number of tokens consumed.</param>
/// <param name="Model">The model that generated the response.</param>
/// <param name="FinishReason">Why generation stopped.</param>
public sealed record LlmResponse(
    string Content,
    int TokensUsed = 0,
    string? Model = null,
    string? FinishReason = null);

/// <summary>
/// Exception thrown by LLM providers.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LlmException"/> class.
/// </remarks>
/// <param name="code">Error code.</param>
/// <param name="message">Error message.</param>
/// <param name="innerException">Optional inner exception.</param>
public sealed class LlmException(LlmErrorCode code, string message, Exception? innerException = null) : Exception(message, innerException)
{

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public LlmErrorCode Code { get; } = code;

    /// <summary>Provider is not available.</summary>
    public static LlmException Unavailable(string provider) =>
        new(LlmErrorCode.Unavailable, $"LLM provider '{provider}' is not available");

    /// <summary>Model not found.</summary>
    public static LlmException ModelNotFound(string model) =>
        new(LlmErrorCode.ModelNotFound, $"Model '{model}' not found");

    /// <summary>Generation failed.</summary>
    public static LlmException GenerationFailed(string message, Exception? inner = null) =>
        new(LlmErrorCode.GenerationFailed, message, inner);

    /// <summary>Request timed out.</summary>
    public static LlmException Timeout() =>
        new(LlmErrorCode.Timeout, "Request timed out");
}

/// <summary>
/// Error codes for LLM operations.
/// </summary>
public enum LlmErrorCode
{
    /// <summary>Unknown error.</summary>
    Unknown = 0,

    /// <summary>Provider unavailable.</summary>
    Unavailable,

    /// <summary>Model not found.</summary>
    ModelNotFound,

    /// <summary>Generation failed.</summary>
    GenerationFailed,

    /// <summary>Request timed out.</summary>
    Timeout,
}

/// <summary>
/// Information about an available model.
/// </summary>
/// <param name="Name">Model name/identifier.</param>
/// <param name="Size">Model size in bytes.</param>
/// <param name="ModifiedAt">When the model was last modified.</param>
public sealed record ModelInfo(string Name, long? Size = null, DateTimeOffset? ModifiedAt = null);
