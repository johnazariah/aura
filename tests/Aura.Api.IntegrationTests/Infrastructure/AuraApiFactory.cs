// <copyright file="AuraApiFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Infrastructure;

using Aura.Foundation;
using Aura.Foundation.Data;
using Aura.Foundation.Llm;
using Aura.Module.Developer.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
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
    private readonly PostgreSqlContainer _postgresContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuraApiFactory"/> class.
    /// </summary>
    public AuraApiFactory()
    {
        // Configure Testcontainers to use Podman on Windows if Docker is not available
        ConfigureContainerRuntime();

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("aura_test")
            .WithUsername("aura_test")
            .WithPassword("aura_test")
            .Build();
    }

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
                options.DefaultProvider = LlmProviders.Stub;
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

    /// <summary>
    /// Configures Testcontainers to use the appropriate container runtime.
    /// On Windows, prefers Podman if Docker is not available.
    /// </summary>
    private static void ConfigureContainerRuntime()
    {
        // Check if DOCKER_HOST is already set (user override)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return;
        }

        // On Windows, check for Podman pipe if Docker pipe doesn't exist
        if (OperatingSystem.IsWindows())
        {
            // Check if Docker pipe exists
            if (IsPipeAvailable("docker_engine"))
            {
                return; // Docker is available, use it
            }

            // Try Podman pipe (standard machine name)
            if (IsPipeAvailable("podman-machine-default"))
            {
                // Configure Testcontainers to use Podman via named pipe
                Environment.SetEnvironmentVariable("DOCKER_HOST", "npipe:////./pipe/podman-machine-default");
                Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
                return;
            }

            // Last resort: check for any podman pipe
            var podmanPipe = FindPodmanPipe();
            if (podmanPipe is not null)
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", $"npipe:////./pipe/{podmanPipe}");
                Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
            }
        }
    }

    /// <summary>
    /// Checks if a Windows named pipe is available.
    /// </summary>
    private static bool IsPipeAvailable(string pipeName)
    {
        try
        {
            var pipePath = $@"\\.\pipe\{pipeName}";
            using var fs = new FileStream(pipePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds any available Podman pipe on Windows.
    /// </summary>
    private static string? FindPodmanPipe()
    {
        try
        {
            // List all pipes and find podman ones
            var pipes = Directory.GetFiles(@"\\.\pipe\", "podman*");
            return pipes.Length > 0 ? Path.GetFileName(pipes[0]) : null;
        }
        catch
        {
            return null;
        }
    }
}
