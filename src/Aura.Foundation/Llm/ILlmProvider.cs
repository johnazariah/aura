// <copyright file="ILlmProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Aura.Foundation.Agents;
using CSharpFunctionalExtensions;

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
    /// <param name="model">The model to use.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="temperature">Temperature for sampling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated text or an error.</returns>
    Task<Result<LlmResponse, LlmError>> GenerateAsync(
        string model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a completion from a chat conversation.
    /// </summary>
    /// <param name="model">The model to use.</param>
    /// <param name="messages">The conversation messages.</param>
    /// <param name="temperature">Temperature for sampling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response or an error.</returns>
    Task<Result<LlmResponse, LlmError>> ChatAsync(
        string model,
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
/// Error from an LLM provider.
/// </summary>
/// <param name="Code">Error code.</param>
/// <param name="Message">Error message.</param>
/// <param name="Details">Optional details.</param>
public sealed record LlmError(LlmErrorCode Code, string Message, string? Details = null)
{
    /// <summary>Provider is not available.</summary>
    public static LlmError Unavailable(string provider) =>
        new(LlmErrorCode.Unavailable, $"LLM provider '{provider}' is not available");

    /// <summary>Model not found.</summary>
    public static LlmError ModelNotFound(string model) =>
        new(LlmErrorCode.ModelNotFound, $"Model '{model}' not found");

    /// <summary>Generation failed.</summary>
    public static LlmError GenerationFailed(string message, string? details = null) =>
        new(LlmErrorCode.GenerationFailed, message, details);

    /// <summary>Request timed out.</summary>
    public static LlmError Timeout() =>
        new(LlmErrorCode.Timeout, "Request timed out");

    /// <summary>Request was cancelled.</summary>
    public static LlmError Cancelled() =>
        new(LlmErrorCode.Cancelled, "Request was cancelled");
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

    /// <summary>Request cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Information about an available model.
/// </summary>
/// <param name="Name">Model name/identifier.</param>
/// <param name="Size">Model size in bytes.</param>
/// <param name="ModifiedAt">When the model was last modified.</param>
public sealed record ModelInfo(string Name, long? Size = null, DateTimeOffset? ModifiedAt = null);
