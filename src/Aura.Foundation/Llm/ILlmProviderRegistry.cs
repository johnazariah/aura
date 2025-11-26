// <copyright file="ILlmProviderRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

/// <summary>
/// Registry for LLM providers.
/// </summary>
public interface ILlmProviderRegistry
{
    /// <summary>
    /// Gets a provider by ID.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The provider if found, null otherwise.</returns>
    ILlmProvider? GetProvider(string providerId);

    /// <summary>
    /// Tries to get a provider by ID.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="provider">The provider if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetProvider(string providerId, out ILlmProvider? provider);

    /// <summary>
    /// Gets the default provider.
    /// </summary>
    /// <returns>The default provider.</returns>
    ILlmProvider? GetDefaultProvider();

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IReadOnlyList<ILlmProvider> Providers { get; }

    /// <summary>
    /// Registers a provider.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    void Register(ILlmProvider provider);
}
