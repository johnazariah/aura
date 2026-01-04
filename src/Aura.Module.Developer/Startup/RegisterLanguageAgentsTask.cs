// <copyright file="RegisterLanguageAgentsTask.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Startup;

using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Shell;
using Aura.Foundation.Startup;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Startup task that loads language specialist agents from YAML configuration files.
/// </summary>
public sealed class RegisterLanguageAgentsTask : IStartupTask
{
    /// <inheritdoc/>
    public int Order => 150; // After code ingestors (100), before agents are used

    /// <inheritdoc/>
    public string Name => "Register Language Specialist Agents";

    /// <inheritdoc/>
    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var fileSystem = serviceProvider.GetRequiredService<IFileSystem>();
        var agentRegistry = serviceProvider.GetRequiredService<IAgentRegistry>();
        var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
        var processRunner = serviceProvider.GetRequiredService<IProcessRunner>();
        var reactExecutor = serviceProvider.GetRequiredService<IReActExecutor>();
        var llmRegistry = serviceProvider.GetRequiredService<ILlmProviderRegistry>();

        var logger = loggerFactory.CreateLogger<RegisterLanguageAgentsTask>();
        var configLoader = new LanguageConfigLoader(
            fileSystem,
            loggerFactory.CreateLogger<LanguageConfigLoader>());

        // Get the languages directory path
        var languagesPath = config["Aura:Modules:Developer:LanguagesPath"]
            ?? GetDefaultLanguagesPath(fileSystem);

        if (!fileSystem.Directory.Exists(languagesPath))
        {
            logger.LogWarning(
                "Languages directory not found at {Path}, skipping language agent registration",
                languagesPath);
            return;
        }

        // Load all language configurations
        var languageConfigs = await configLoader.LoadAllAsync(languagesPath).ConfigureAwait(false);

        if (languageConfigs.Count == 0)
        {
            logger.LogInformation("No language configurations found in {Path}", languagesPath);
            return;
        }

        var registeredAgents = new List<string>();
        var registeredTools = new List<string>();

        foreach (var langConfig in languageConfigs)
        {
            // Skip C# - it uses the hardcoded RoslynCodingAgent
            if (langConfig.Language.Id.Equals("csharp", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug(
                    "Skipping {Language} - uses hardcoded RoslynCodingAgent",
                    langConfig.Language.Name);
                continue;
            }

            try
            {
                // Register tools from config
                var tools = LanguageToolFactory.RegisterToolsFromConfig(
                    toolRegistry,
                    processRunner,
                    langConfig,
                    loggerFactory.CreateLogger("LanguageToolFactory"));

                registeredTools.AddRange(tools);

                // Create and register the agent
                var agent = new LanguageSpecialistAgent(
                    langConfig,
                    reactExecutor,
                    toolRegistry,
                    llmRegistry,
                    loggerFactory.CreateLogger<LanguageSpecialistAgent>());

                agentRegistry.Register(agent);
                registeredAgents.Add(agent.AgentId);

                logger.LogDebug(
                    "Registered {AgentId} with capabilities: {Capabilities}",
                    agent.AgentId,
                    string.Join(", ", agent.Metadata.Capabilities));
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to register agent for {Language}",
                    langConfig.Language.Name);
            }
        }

        logger.LogInformation(
            "Language agents registered: {Agents} ({ToolCount} tools)",
            string.Join(", ", registeredAgents),
            registeredTools.Count);
    }

    /// <summary>
    /// Gets the default languages directory path.
    /// </summary>
    private static string GetDefaultLanguagesPath(IFileSystem fileSystem)
    {
        // Try relative to current directory first
        var relativePath = fileSystem.Path.Combine("agents", "languages");
        if (fileSystem.Directory.Exists(relativePath))
        {
            return relativePath;
        }

        // Try relative to the base directory of the executing assembly
        var basePath = AppContext.BaseDirectory;
        var absolutePath = fileSystem.Path.Combine(basePath, "agents", "languages");
        if (fileSystem.Directory.Exists(absolutePath))
        {
            return absolutePath;
        }

        // Default to relative path (may not exist)
        return relativePath;
    }
}
