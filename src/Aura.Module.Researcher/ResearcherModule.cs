// <copyright file="ResearcherModule.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher;

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
public sealed class ResearcherModule
{
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

}
