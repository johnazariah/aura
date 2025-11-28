// <copyright file="ServiceCollectionExtensions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation;

using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Aura Foundation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Aura Foundation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuraFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // File system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();

        // LLM services
        services.AddLlmServices(configuration);

        // RAG services
        services.AddRagServices(configuration);

        // Agent services
        services.AddAgentServices(configuration);

        return services;
    }

    /// <summary>
    /// Adds LLM provider services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLlmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        // Registry
        services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();

        // Ollama provider with typed HttpClient
        services.AddHttpClient<OllamaProvider>(client =>
        {
            var ollamaSection = configuration.GetSection(OllamaOptions.SectionName);
            var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(300);
        });

        // Stub provider for testing/fallback
        services.AddSingleton<StubLlmProvider>();

        // Register providers on startup
        services.AddHostedService<LlmProviderInitializer>();

        return services;
    }

    /// <summary>
    /// Adds RAG (Retrieval-Augmented Generation) services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRagServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure RAG options
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        // Register embedding provider (OllamaProvider implements IEmbeddingProvider)
        services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        // Text chunker
        services.AddSingleton<TextChunker>();

        // RAG service
        services.AddScoped<IRagService, RagService>();

        return services;
    }

    /// <summary>
    /// Adds agent services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));

        // Agent factory
        services.AddSingleton<IAgentFactory, ConfigurableAgentFactory>();

        // Agent loader
        services.AddSingleton<IAgentLoader, MarkdownAgentLoader>();

        // Agent registry
        services.AddSingleton<IAgentRegistry, AgentRegistry>();

        // Initialize registry on startup
        services.AddHostedService<AgentRegistryInitializer>();

        return services;
    }
}

/// <summary>
/// Configuration options for agents.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Aura:Agents";

    /// <summary>
    /// Gets or sets directories to watch for agent files.
    /// </summary>
    public List<string> Directories { get; set; } = ["agents"];

    /// <summary>
    /// Gets or sets whether hot-reload is enabled.
    /// </summary>
    public bool EnableHotReload { get; set; } = true;
}
