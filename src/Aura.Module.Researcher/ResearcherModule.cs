// <copyright file="ResearcherModule.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher;

using Aura.Foundation.Agents;
using Aura.Foundation.Modules;
using Aura.Module.Researcher.Data;
using Aura.Module.Researcher.Fetchers;
using Aura.Module.Researcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Research assistant module for Aura.
/// Provides paper management, PDF extraction, knowledge graphs, and synthesis.
/// </summary>
public sealed class ResearcherModule : IAuraModule
{
    /// <inheritdoc/>
    public string ModuleId => "researcher";

    /// <inheritdoc/>
    public string Name => "Research Assistant";

    /// <inheritdoc/>
    public string Description => "Paper management, knowledge graphs, and literature synthesis";

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies => []; // Only depends on Foundation

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Register module options
        services.Configure<ResearcherModuleOptions>(config.GetSection(ResearcherModuleOptions.SectionName));

        // Get connection string from configuration (shared with Foundation)
        var connectionString = config.GetConnectionString("auradb");

        // Register ResearcherDbContext (uses same database as Foundation)
        services.AddDbContext<ResearcherDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        // Register services
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddSingleton<IPdfExtractor, PdfExtractor>();
        services.AddSingleton<IPdfToMarkdownService, PdfToMarkdownService>();

        // Register fetchers with typed HttpClient
        services.AddHttpClient<ArxivFetcher>();
        services.AddHttpClient<SemanticScholarFetcher>();
        services.AddHttpClient<WebPageFetcher>();

        // Register fetchers as ISourceFetcher (order matters - more specific first)
        services.AddSingleton<ISourceFetcher>(sp => sp.GetRequiredService<ArxivFetcher>());
        services.AddSingleton<ISourceFetcher>(sp => sp.GetRequiredService<SemanticScholarFetcher>());
        services.AddSingleton<ISourceFetcher>(sp => sp.GetRequiredService<WebPageFetcher>());

        // Register aggregator service
        services.AddSingleton<SourceFetcherService>();

        // Ensure storage directories exist
        var moduleOptions = config.GetSection(ResearcherModuleOptions.SectionName)
            .Get<ResearcherModuleOptions>() ?? new ResearcherModuleOptions();

        Directory.CreateDirectory(moduleOptions.StoragePath);
        Directory.CreateDirectory(moduleOptions.PapersPath);
    }

    /// <inheritdoc/>
    public void RegisterAgents(IAgentRegistry registry, IConfiguration configuration)
    {
        // Agents will be loaded from agents/ directory via markdown definitions
        // No programmatic agent registration needed
    }
}
