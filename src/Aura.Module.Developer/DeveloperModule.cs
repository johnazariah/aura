// <copyright file="DeveloperModule.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer;

using Aura.Foundation.Agents;
using Aura.Foundation.Modules;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Agents;
using Aura.Module.Developer.Data;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        // Register module options
        services.Configure<DeveloperModuleOptions>(config.GetSection(DeveloperModuleOptions.SectionName));

        // Get connection string from configuration (shared with Foundation)
        var connectionString = config.GetConnectionString("auradb");

        // Register DeveloperDbContext (uses same database as Foundation)
        services.AddDbContext<DeveloperDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        // Register Developer Module services
        services.AddScoped<IWorkflowService, WorkflowService>();

        // Register GitHub service with typed HttpClient
        services.Configure<GitHubOptions>(config.GetSection(GitHubOptions.SectionName));
        services.AddHttpClient<IGitHubService, GitHubService>((sp, client) =>
        {
            var options = config.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>() ?? new();
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Aura/1.2.0");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(options.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);
            }
        });

        // Register Roslyn workspace service (singleton for caching)
        services.AddSingleton<IRoslynWorkspaceService, RoslynWorkspaceService>();

        // Register Roslyn refactoring service
        services.AddSingleton<IRoslynRefactoringService, RoslynRefactoringService>();

        // Register Python refactoring service
        services.AddSingleton<IPythonRefactoringService, PythonRefactoringService>();

        // Register Code Graph indexer (for Graph RAG)
        services.AddScoped<ICodeGraphIndexer, CodeGraphIndexer>();

        // Register codebase context service (combines graph + RAG for agent context)
        services.AddScoped<ICodebaseContextService, CodebaseContextService>();

        // Register hardcoded agents provider (C# ingester, etc.)
        services.AddSingleton<IHardcodedAgentProvider, DeveloperAgentProvider>();

        // Register startup task for code ingestors
        services.AddSingleton<Foundation.Startup.IStartupTask, Startup.RegisterCodeIngestorsTask>();

        // Register startup task for language specialist agents (from YAML configs)
        services.AddSingleton<Foundation.Startup.IStartupTask, Startup.RegisterLanguageAgentsTask>();

        // Register language config loader
        services.AddSingleton<Agents.ILanguageConfigLoader, Agents.LanguageConfigLoader>();

        // Register Roslyn tools
        services.AddSingleton<ListProjectsTool>();
        services.AddSingleton<ListClassesTool>();
        services.AddSingleton<GetClassInfoTool>();
        services.AddSingleton<ValidateCompilationTool>();
        services.AddSingleton<FindUsagesTool>();
        services.AddSingleton<GetProjectReferencesTool>();

        // Note: File tools (file.read, file.write, file.modify) are provided by Foundation
        // via BuiltInTools - do not duplicate registration here

        // Register dotnet tools
        services.AddSingleton<RunTestsTool>();

        // Register Graph RAG tools (need scoped due to DbContext dependency)
        services.AddScoped<IndexCodeGraphTool>();
        services.AddScoped<FindImplementationsTool>();
        services.AddScoped<FindCallersTool>();
        services.AddScoped<GetTypeMembersTool>();
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

    /// <summary>
    /// Registers Developer Module tools with the tool registry.
    /// Called by the API when the module is loaded.
    /// </summary>
    public void RegisterTools(IToolRegistry toolRegistry, IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var processRunner = services.GetRequiredService<Foundation.Shell.IProcessRunner>();

        // Roslyn tools (code analysis)
        var listProjects = services.GetRequiredService<ListProjectsTool>();
        toolRegistry.RegisterTool<ListProjectsInput, ListProjectsOutput>(listProjects);

        var listClasses = services.GetRequiredService<ListClassesTool>();
        toolRegistry.RegisterTool<ListClassesInput, ListClassesOutput>(listClasses);

        var getClassInfo = services.GetRequiredService<GetClassInfoTool>();
        toolRegistry.RegisterTool<GetClassInfoInput, GetClassInfoOutput>(getClassInfo);

        var validateCompilation = services.GetRequiredService<ValidateCompilationTool>();
        toolRegistry.RegisterTool<ValidateCompilationInput, ValidateCompilationOutput>(validateCompilation);

        var findUsages = services.GetRequiredService<FindUsagesTool>();
        toolRegistry.RegisterTool<FindUsagesInput, FindUsagesOutput>(findUsages);

        var getProjectReferences = services.GetRequiredService<GetProjectReferencesTool>();
        toolRegistry.RegisterTool<GetProjectReferencesInput, GetProjectReferencesOutput>(getProjectReferences);

        // Note: File tools (file.read, file.write, file.modify) are provided by Foundation
        // via BuiltInTools - do not duplicate registration here

        // Dotnet tools
        var runTests = services.GetRequiredService<RunTestsTool>();
        toolRegistry.RegisterTool<RunTestsInput, RunTestsOutput>(runTests);

        // Graph RAG tools (code graph queries)
        var indexCodeGraph = services.GetRequiredService<IndexCodeGraphTool>();
        toolRegistry.RegisterTool<IndexCodeGraphInput, IndexCodeGraphOutput>(indexCodeGraph);

        var findImplementations = services.GetRequiredService<FindImplementationsTool>();
        toolRegistry.RegisterTool<FindImplementationsInput, FindImplementationsOutput>(findImplementations);

        var findCallers = services.GetRequiredService<FindCallersTool>();
        toolRegistry.RegisterTool<FindCallersInput, FindCallersOutput>(findCallers);

        var getTypeMembers = services.GetRequiredService<GetTypeMembersTool>();
        toolRegistry.RegisterTool<GetTypeMembersInput, GetTypeMembersOutput>(getTypeMembers);

        // Language-specific tools (use static registration)
        FSharpTools.RegisterFSharpTools(
            toolRegistry,
            processRunner,
            loggerFactory.CreateLogger("FSharpTools"));

        PythonTools.RegisterPythonTools(
            toolRegistry,
            processRunner,
            loggerFactory.CreateLogger("PythonTools"));

        TypeScriptTools.RegisterTypeScriptTools(
            toolRegistry,
            processRunner,
            loggerFactory.CreateLogger("TypeScriptTools"));

        GoTools.RegisterGoTools(
            toolRegistry,
            processRunner,
            loggerFactory.CreateLogger("GoTools"));

        RustTools.RegisterRustTools(
            toolRegistry,
            processRunner,
            loggerFactory.CreateLogger("RustTools"));

        // Build-fix loop tools (autonomous build → fix → rebuild cycles)
        var agentRegistry = services.GetRequiredService<Foundation.Agents.IAgentRegistry>();
        BuildFixLoopTools.RegisterBuildFixLoopTools(
            toolRegistry,
            processRunner,
            agentRegistry,
            loggerFactory.CreateLogger("BuildFixLoopTools"));

        // Architecture visualization tools (Mermaid/ASCII diagrams)
        var codeGraphService = services.GetRequiredService<Foundation.Rag.ICodeGraphService>();
        VisualizationTools.RegisterVisualizationTools(
            toolRegistry,
            codeGraphService,
            loggerFactory.CreateLogger("VisualizationTools"));

        // GitHub Actions tools (CI/CD monitoring and triggering)
        var gitHubService = services.GetRequiredService<GitHub.IGitHubService>();
        GitHubActionsTools.Register(
            toolRegistry,
            gitHubService,
            loggerFactory);
    }
}
