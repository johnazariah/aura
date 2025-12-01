// <copyright file="DeveloperModule.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer;

using Aura.Foundation.Agents;
using Aura.Foundation.Modules;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Developer workflow module for Aura.
/// Provides code automation, testing, git worktrees, and workflow management.
/// </summary>
public sealed class DeveloperModule : IAuraModule
{
    /// <inheritdoc/>
    public string ModuleId => "developer";

    /// <inheritdoc/>
    public string Name => "Developer Workflow";

    /// <inheritdoc/>
    public string Description => "Code automation, testing, git worktrees, and workflow management";

    /// <inheritdoc/>
    public IReadOnlyList<string> Dependencies => []; // Only depends on Foundation

    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Get connection string from configuration (shared with Foundation)
        var connectionString = config.GetConnectionString("auradb");

        // Register DeveloperDbContext (uses same database as Foundation)
        services.AddDbContext<DeveloperDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        // Register Developer Module services
        services.AddScoped<IWorkflowService, WorkflowService>();
    }

    /// <inheritdoc/>
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
    {
        // Developer-specific agents can be registered here
        // The markdown agents from agents/developer/ will be loaded separately
        var agentsPath = config["Aura:Modules:Developer:AgentsPath"] ?? "./agents/developer";

        // TODO: Load agents from directory when file-based agent loading is implemented
        // registry.LoadAgentsFromDirectory(agentsPath);
    }
}
