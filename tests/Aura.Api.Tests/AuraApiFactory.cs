// <copyright file="AuraApiFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests;

using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Custom WebApplicationFactory for API integration tests.
/// Configures in-memory database and stub LLM provider.
/// </summary>
public class AuraApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the path to test agents directory.
    /// </summary>
    public string TestAgentsPath { get; private set; } = null!;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set up test agents directory
        TestAgentsPath = Path.Combine(Path.GetTempPath(), "aura-test-agents", Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestAgentsPath);
        SetupTestAgents();

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL database-related services to avoid provider conflicts
            // This is more aggressive but necessary when switching from Npgsql to InMemory
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuraDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Remove the DbContext itself
            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AuraDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            // Remove DbContextOptions (non-generic)
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions));
            if (optionsDescriptor != null)
            {
                services.Remove(optionsDescriptor);
            }

            // Add in-memory database for testing with fresh service provider
            var dbName = $"AuraTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AuraDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(dbName);
                // Force using internal service provider to avoid Npgsql conflict
                options.UseInternalServiceProvider(
                    new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider());
            });

            // Ensure we use the stub LLM provider
            services.RemoveAll<ILlmProvider>();
            services.AddSingleton<ILlmProvider, StubLlmProvider>();

            // Remove all existing AgentOptions configuration and replace with test config
            // This must be done by removing and re-adding the IConfigureOptions
            var testPath = TestAgentsPath; // Capture for closure
            services.RemoveAll<IConfigureOptions<AgentOptions>>();
            services.RemoveAll<IPostConfigureOptions<AgentOptions>>();
            services.Configure<AgentOptions>(options =>
            {
                options.Directories = [testPath];
                options.EnableHotReload = false;
            });
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(TestAgentsPath))
        {
            try
            {
                Directory.Delete(TestAgentsPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        base.Dispose(disposing);
    }

    private void SetupTestAgents()
    {
        // Create test chat agent
        var chatAgent = """
            # Test Chat Agent

            A test chat agent for integration tests.

            ## Metadata

            - **Priority**: 80
            - **Provider**: stub
            - **Model**: test-model

            ## Capabilities

            - chat

            ## Tags

            - test
            - fallback

            ## System Prompt

            You are a test chat agent.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "chat-agent.md"), chatAgent);

        // Create test coding agent
        var codingAgent = """
            # Test Coding Agent

            A test coding agent for integration tests.

            ## Metadata

            - **Priority**: 70
            - **Provider**: stub
            - **Model**: test-model

            ## Capabilities

            - coding

            ## Tags

            - test
            - polyglot

            ## System Prompt

            You are a test coding agent.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "coding-agent.md"), codingAgent);

        // Create test C# specialist agent
        var csharpAgent = """
            # Test C# Agent

            A test C# specialist agent for integration tests.

            ## Metadata

            - **Priority**: 30
            - **Provider**: stub
            - **Model**: test-model

            ## Capabilities

            - coding

            ## Languages

            - csharp

            ## Tags

            - test
            - specialist

            ## System Prompt

            You are a test C# specialist agent.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "csharp-agent.md"), csharpAgent);

        // Create test analysis agent
        var analysisAgent = """
            # Test Analysis Agent

            A test analysis agent for integration tests.

            ## Metadata

            - **Priority**: 50
            - **Provider**: stub
            - **Model**: test-model

            ## Capabilities

            - analysis

            ## Tags

            - test
            - planning

            ## System Prompt

            You are a test analysis agent.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "analysis-agent.md"), analysisAgent);
    }
}
