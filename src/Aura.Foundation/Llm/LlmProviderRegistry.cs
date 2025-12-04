// <copyright file="LlmProviderRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default implementation of LLM provider registry.
/// </summary>
public sealed class LlmProviderRegistry : ILlmProviderRegistry
{
    private readonly ConcurrentDictionary<string, ILlmProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<LlmProviderRegistry> _logger;
    private readonly LlmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderRegistry"/> class.
    /// </summary>
    /// <param name="options">LLM options.</param>
    /// <param name="logger">Logger instance.</param>
    public LlmProviderRegistry(
        IOptions<LlmOptions> options,
        ILogger<LlmProviderRegistry> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ILlmProvider> Providers => _providers.Values.ToList();

    /// <inheritdoc/>
    public ILlmProvider? GetProvider(string providerId)
    {
        _providers.TryGetValue(providerId, out var provider);
        return provider;
    }

    /// <inheritdoc/>
    public bool TryGetProvider(string providerId, out ILlmProvider? provider)
    {
        return _providers.TryGetValue(providerId, out provider);
    }

    /// <inheritdoc/>
    public ILlmProvider? GetDefaultProvider()
    {
        return GetProvider(_options.DefaultProvider);
    }

    /// <inheritdoc/>
    public void Register(ILlmProvider provider)
    {
        _providers[provider.ProviderId] = provider;
        _logger.LogInformation("Registered LLM provider: {ProviderId}", provider.ProviderId);
    }
}

/// <summary>
/// Configuration options for LLM providers.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "LlmProviders";

    /// <summary>
    /// Gets or sets the default provider ID.
    /// </summary>
    public string DefaultProvider { get; set; } = "ollama";

    /// <summary>
    /// Gets or sets the default model.
    /// </summary>
    public string DefaultModel { get; set; } = "qwen2.5-coder:7b";

    /// <summary>
    /// Gets or sets the default temperature.
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;
}
