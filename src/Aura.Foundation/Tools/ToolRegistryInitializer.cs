using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Git;
using Aura.Foundation.Llm;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools.BuiltIn;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Initializes the tool registry with built-in tools.
/// </summary>
public class ToolRegistryInitializer(
    IToolRegistry registry,
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    IGitService gitService,
    IAgentRegistry agentRegistry,
    IReActExecutor reactExecutor,
    ILlmProviderRegistry llmProviderRegistry,
    ILoggerFactory loggerFactory,
    ILogger<ToolRegistryInitializer> logger) : IHostedService
{
    private readonly IToolRegistry _registry = registry;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IGitService _gitService = gitService;
    private readonly IAgentRegistry _agentRegistry = agentRegistry;
    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly ILlmProviderRegistry _llmProviderRegistry = llmProviderRegistry;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILogger<ToolRegistryInitializer> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering built-in tools");
        BuiltInTools.RegisterBuiltInTools(_registry, _fileSystem, _processRunner, _logger);

        _logger.LogInformation("Registering git tools");
        GitTools.RegisterGitTools(_registry, _gitService, _logger);

        _logger.LogInformation("Registering code validation tool");
        var codeValidateTool = new CodeValidateTool(
            _processRunner,
            _loggerFactory.CreateLogger<CodeValidateTool>());
        _registry.RegisterTool(codeValidateTool);

        _logger.LogInformation("Registering sub-agent tool");
        var subAgentTool = new SpawnSubAgentTool(
            _agentRegistry,
            _reactExecutor,
            _registry,
            _llmProviderRegistry,
            _loggerFactory.CreateLogger<SpawnSubAgentTool>());
        _registry.RegisterTool(subAgentTool);

        _logger.LogInformation("Registering token budget tool");
        _registry.RegisterTool(CheckTokenBudgetTool.GetDefinition());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
