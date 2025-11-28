// <copyright file="IEmbeddingProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

/// <summary>
/// Interface for embedding generation providers.
/// Implemented by LLM providers that support embeddings (like Ollama).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates embeddings for the given text.
    /// </summary>
    /// <param name="model">The embedding model to use (e.g., "nomic-embed-text").</param>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding vector.</returns>
    /// <exception cref="LlmException">Thrown when embedding generation fails.</exception>
    Task<float[]> GenerateEmbeddingAsync(
        string model,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple texts in a batch.
    /// </summary>
    /// <param name="model">The embedding model to use.</param>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding vectors, one per input text.</returns>
    /// <exception cref="LlmException">Thrown when embedding generation fails.</exception>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        string model,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from embedding generation.
/// </summary>
/// <param name="Embedding">The embedding vector.</param>
/// <param name="Model">The model used.</param>
public sealed record EmbeddingResponse(float[] Embedding, string? Model = null);
