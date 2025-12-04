// <copyright file="ServiceCollectionExtensions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation;

using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Conversations;
using Aura.Foundation.Git;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
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

        // Shell/Process services
        services.AddShellServices(configuration);

        // Git services
        services.AddGitServices(configuration);

        // Tool registry
        services.AddToolServices(configuration);

        // LLM services
        services.AddLlmServices(configuration);

        // RAG services
        services.AddRagServices(configuration);

        // Conversation services
        services.AddConversationServices(configuration);

        // Agent services
        services.AddAgentServices(configuration);

        // Prompt services
        services.AddPromptServices(configuration);

        return services;
    }

    /// <summary>
    /// Adds shell/process execution services.
    /// </summary>
    public static IServiceCollection AddShellServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        return services;
    }

    /// <summary>
    /// Adds Git services.
    /// </summary>
    public static IServiceCollection AddGitServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        return services;
    }

    /// <summary>
    /// Adds tool registry services.
    /// </summary>
    public static IServiceCollection AddToolServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // ReAct executor for tool-using agents
        services.AddSingleton<IReActExecutor, ReActExecutor>();

        // Register built-in tools after all services are built
        services.AddHostedService<ToolRegistryInitializer>();

        return services;
    }    /// <summary>
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
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));

        // Registry
        services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();

        // Ollama provider with typed HttpClient
        services.AddHttpClient<OllamaProvider>(client =>
        {
            var ollamaSection = configuration.GetSection(OllamaOptions.SectionName);
            var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan; // No timeout on HttpClient, let resilience handle it
        });

        // OpenAI provider (direct API, optional, only if configured)
        var openAiSection = configuration.GetSection(OpenAiOptions.SectionName);
        if (!string.IsNullOrEmpty(openAiSection["ApiKey"]))
        {
            services.AddSingleton<OpenAiProvider>();
        }

        // Azure OpenAI provider (optional, only if configured)
        var azureSection = configuration.GetSection(AzureOpenAiOptions.SectionName);
        if (!string.IsNullOrEmpty(azureSection["Endpoint"]) && !string.IsNullOrEmpty(azureSection["ApiKey"]))
        {
            services.AddSingleton<AzureOpenAiProvider>();
        }

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
        services.Configure<RagExecutionOptions>(configuration.GetSection(RagExecutionOptions.SectionName));
        services.Configure<RagWatcherOptions>(configuration.GetSection(RagWatcherOptions.SectionName));
        services.Configure<BackgroundIndexerOptions>(configuration.GetSection(BackgroundIndexerOptions.SectionName));

        // Register embedding provider (OllamaProvider implements IEmbeddingProvider)
        services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        // Text chunker
        services.AddSingleton<TextChunker>();

        // Content ingestors for smart file processing
        services.AddSingleton<Rag.Ingestors.IIngestorRegistry, Rag.Ingestors.IngestorRegistry>();

        // RAG service (vector-based)
        services.AddScoped<IRagService, RagService>();

        // Code Graph service (graph-based RAG for structural queries)
        services.AddScoped<ICodeGraphService, CodeGraphService>();

        // RAG-enriched executor
        services.AddScoped<IRagEnrichedExecutor, RagEnrichedExecutor>();

        // Background indexer (queue-based async indexing)
        services.AddSingleton<BackgroundIndexer>();
        services.AddSingleton<IBackgroundIndexer>(sp => sp.GetRequiredService<BackgroundIndexer>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundIndexer>());

        // Incremental indexer (background service for file watching)
        services.AddSingleton<IncrementalIndexer>();

        return services;
    }

    /// <summary>
    /// Adds conversation services with RAG context persistence.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConversationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IConversationService, ConversationService>();
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

        // Foundation-level hardcoded agents (fallback ingester, etc.)
        services.AddSingleton<IHardcodedAgentProvider, FoundationAgentProvider>();

        // Initialize registry on startup
        services.AddHostedService<AgentRegistryInitializer>();

        return services;
    }

    /// <summary>
    /// Adds prompt registry services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPromptServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<Prompts.PromptOptions>(configuration.GetSection(Prompts.PromptOptions.SectionName));

        // Register shared Handlebars instance for template processing
        services.AddSingleton<HandlebarsDotNet.IHandlebars>(_ =>
            HandlebarsDotNet.Handlebars.Create(new HandlebarsDotNet.HandlebarsConfiguration
            {
                ThrowOnUnresolvedBindingExpression = false, // Don't throw on missing properties
                NoEscape = true, // Don't HTML-escape output (we're generating prompts, not HTML)
            }));

        // Prompt registry
        services.AddSingleton<Prompts.IPromptRegistry, Prompts.PromptRegistry>();

        // Initialize registry on startup
        services.AddHostedService<Prompts.PromptRegistryInitializer>();

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
