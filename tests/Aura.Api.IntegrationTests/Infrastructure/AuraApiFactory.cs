// <copyright file="AuraApiFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Infrastructure;

using Aura.Foundation;
using Aura.Foundation.Data;
using Aura.Foundation.Llm;
using Aura.Module.Developer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

/// <summary>
/// Custom WebApplicationFactory for Aura API integration tests.
/// Uses Testcontainers to spin up a real PostgreSQL with pgvector for true integration testing.
/// </summary>
public class AuraApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("aura_test")
        .WithUsername("aura_test")
        .WithPassword("aura_test")
        .Build();

    /// <summary>
    /// Gets the PostgreSQL connection string for the test container.
    /// </summary>
    public string ConnectionString => _postgresContainer.GetConnectionString();

    /// <summary>
    /// Gets the absolute path to the agents directory.
    /// Walks up from the test output directory to find the repository root.
    /// </summary>
    private static string AgentsDirectory
    {
        get
        {
            // Start from the test assembly location
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            // Walk up until we find the repository root (contains Aura.sln)
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aura.sln")))
            {
                directory = directory.Parent;
            }

            if (directory is null)
            {
                throw new InvalidOperationException(
                    "Could not find repository root. Expected Aura.sln in a parent directory.");
            }

            return Path.Combine(directory.FullName, "agents");
        }
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    /// <inheritdoc/>
    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use IntegrationTesting environment so migrations run
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registrations
            RemoveService<DbContextOptions<AuraDbContext>>(services);
            RemoveService<DbContextOptions<DeveloperDbContext>>(services);
            RemoveService<AuraDbContext>(services);
            RemoveService<DeveloperDbContext>(services);

            // Add DbContexts with the test container connection string
            services.AddDbContext<AuraDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString, o => o.UseVector());
            });

            services.AddDbContext<DeveloperDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
            });

            // Configure agents to load from the actual agents directory
            // Use PostConfigure to override the default configuration
            services.PostConfigure<AgentOptions>(options =>
            {
                options.Directories = [AgentsDirectory];
            });

            // Configure LLM to use stub provider as default
            services.Configure<LlmOptions>(options =>
            {
                options.DefaultProvider = "stub";
            });
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
    }
}
