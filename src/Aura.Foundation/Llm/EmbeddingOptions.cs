// <copyright file="EmbeddingOptions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

/// <summary>
/// Top-level configuration for embedding provider selection.
/// Controls which provider is used for embedding generation.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Embedding";

    /// <summary>
    /// Gets or sets the embedding provider to use.
    /// Supported values: "ollama" (default, local), "openai" (hosted).
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// Gets or sets the batch size for embedding generation.
    /// Controls how many texts are sent per API request.
    /// Applies to providers that support batching (OpenAI).
    /// For Ollama, this controls how many texts are sent in one /api/embed call.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
