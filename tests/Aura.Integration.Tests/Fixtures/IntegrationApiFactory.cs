// <copyright file="IntegrationApiFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Integration.Tests.Fixtures;

using Aura.Foundation;
using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// WebApplicationFactory configured for integration tests with real Ollama.
/// Uses in-memory database but real LLM provider.
/// </summary>
public class IntegrationApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the path to test agents directory.
    /// </summary>
    public string TestAgentsPath { get; private set; } = null!;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set up test agents directory
        TestAgentsPath = Path.Combine(Path.GetTempPath(), "aura-integration-agents", Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestAgentsPath);
        SetupTestAgents();

        builder.UseEnvironment("Integration");

        builder.ConfigureServices(services =>
        {
            // Remove ALL database-related services - use in-memory
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuraDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AuraDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions));
            if (optionsDescriptor != null)
            {
                services.Remove(optionsDescriptor);
            }

            // Add in-memory database
            var dbName = $"AuraIntegrationDb_{Guid.NewGuid()}";
            services.AddDbContext<AuraDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(dbName);
                options.UseInternalServiceProvider(
                    new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider());
            });

            // Keep real LLM provider (Ollama) - don't replace it

            // Configure agents path
            var testPath = TestAgentsPath;
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
        // Chat agent using real Ollama
        var chatAgent = """
            # Integration Chat Agent

            A chat agent for integration testing with real LLM.

            ## Metadata

            - **Priority**: 80
            - **Provider**: ollama
            - **Model**: llama3:latest

            ## Capabilities

            - chat

            ## Tags

            - integration
            - chat

            ## System Prompt

            You are a helpful assistant. Keep your responses concise and direct.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "integration-chat-agent.md"), chatAgent);

        // Coding agent using qwen2.5-coder
        var codingAgent = """
            # Integration Coding Agent

            A coding agent for integration testing with real LLM.

            ## Metadata

            - **Priority**: 60
            - **Provider**: ollama
            - **Model**: qwen2.5-coder:7b

            ## Capabilities

            - coding

            ## Languages

            - csharp
            - python
            - typescript

            ## Tags

            - integration
            - coding

            ## System Prompt

            You are an expert programmer. Write clean, idiomatic code with proper error handling.
            When asked to write code, provide only the code without extensive explanations unless asked.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "integration-coding-agent.md"), codingAgent);

        // Analysis agent
        var analysisAgent = """
            # Integration Analysis Agent

            An analysis agent for integration testing.

            ## Metadata

            - **Priority**: 50
            - **Provider**: ollama
            - **Model**: llama3:latest

            ## Capabilities

            - analysis
            - digestion

            ## Tags

            - integration
            - analysis

            ## System Prompt

            You are an expert analyst. Summarize information clearly and identify key points.
            Be concise but thorough.
            """;
        File.WriteAllText(Path.Combine(TestAgentsPath, "integration-analysis-agent.md"), analysisAgent);
    }
}
