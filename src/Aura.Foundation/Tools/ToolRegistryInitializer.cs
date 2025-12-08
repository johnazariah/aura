using System.IO.Abstractions;
using Aura.Foundation.Git;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Initializes the tool registry with built-in tools.
/// </summary>
public class ToolRegistryInitializer : IHostedService
{
    private readonly IToolRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IGitService _gitService;
    private readonly ILogger<ToolRegistryInitializer> _logger;

    public ToolRegistryInitializer(
        IToolRegistry registry,
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        IGitService gitService,
        ILogger<ToolRegistryInitializer> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _gitService = gitService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering built-in tools");
        BuiltInTools.RegisterBuiltInTools(_registry, _fileSystem, _processRunner, _logger);

        _logger.LogInformation("Registering git tools");
        GitTools.RegisterGitTools(_registry, _gitService, _logger);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
