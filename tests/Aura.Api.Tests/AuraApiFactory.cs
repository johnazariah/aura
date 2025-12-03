// <copyright file="AuraApiFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests;

using Aura.Foundation.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Custom WebApplicationFactory for Aura API unit tests.
/// Uses stub services for fast, isolated testing without external dependencies.
/// For true integration tests with real PostgreSQL, see Aura.Api.IntegrationTests.
/// </summary>
public class AuraApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Configure LLM to use stub provider as default
            services.Configure<LlmOptions>(options =>
            {
                options.DefaultProvider = "stub";
            });
        });
    }
}
