namespace Aura.Foundation;

using System.IO.Abstractions;
using Aura.Foundation.Git;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring core Aura services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core Foundation services to the container.
    /// </summary>
    public static IServiceCollection AddAuraFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddShellServices();
        services.AddGitServices();
        services.AddRagServices(configuration);
        return services;
    }

    /// <summary>
    /// Adds shell/process execution services.
    /// </summary>
    public static IServiceCollection AddShellServices(this IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        return services;
    }

    /// <summary>
    /// Adds git services.
    /// </summary>
    public static IServiceCollection AddGitServices(this IServiceCollection services)
    {
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        return services;
    }

    /// <summary>
    /// Adds RAG/indexing services and embedding provider.
    /// </summary>
    public static IServiceCollection AddRagServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
        services.Configure<RagWatcherOptions>(configuration.GetSection(RagWatcherOptions.SectionName));
        services.Configure<BackgroundIndexerOptions>(configuration.GetSection(BackgroundIndexerOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        services.AddHttpClient<OllamaProvider>(client =>
        {
            var section = configuration.GetSection(OllamaOptions.SectionName);
            var baseUrl = section["BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OllamaProvider>());

        services.AddSingleton<TextChunker>();
        services.AddSingleton<Rag.Ingestors.IIngestorRegistry, Rag.Ingestors.IngestorRegistry>();

        services.AddScoped<IRagService, RagService>();
        services.AddScoped<ICodeGraphService, CodeGraphService>();
        services.AddScoped<ICodeGraphEnricher, CodeGraphEnricher>();

        services.AddSingleton<BackgroundIndexer>();
        services.AddSingleton<IBackgroundIndexer>(sp => sp.GetRequiredService<BackgroundIndexer>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundIndexer>());

        services.AddSingleton<IncrementalIndexer>();
        services.AddSingleton<IWorkspaceRegistryService, WorkspaceRegistryService>();

        return services;
    }
}
