// <copyright file="StartupTaskRunner.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Startup;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs all registered <see cref="IStartupTask"/> implementations in order.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StartupTaskRunner"/> class.
/// </remarks>
public sealed class StartupTaskRunner(IServiceProvider serviceProvider, ILogger<StartupTaskRunner> logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<StartupTaskRunner> _logger = logger;

    /// <summary>
    /// Runs all startup tasks in order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _serviceProvider.GetServices<IStartupTask>()
            .OrderBy(t => t.Order)
            .ToList();

        if (tasks.Count == 0)
        {
            _logger.LogDebug("No startup tasks registered");
            return;
        }

        _logger.LogInformation("Running {Count} startup tasks", tasks.Count);

        foreach (var task in tasks)
        {
            _logger.LogDebug("Running startup task: {TaskName} (Order={Order})", task.Name, task.Order);

            try
            {
                await task.ExecuteAsync(_serviceProvider, cancellationToken);
                _logger.LogDebug("Completed startup task: {TaskName}", task.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup task failed: {TaskName}", task.Name);
                throw; // Fail fast - startup tasks are critical
            }
        }

        _logger.LogInformation("All startup tasks completed");
    }
}
