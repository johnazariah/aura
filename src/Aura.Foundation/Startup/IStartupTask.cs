// <copyright file="IStartupTask.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Startup;

/// <summary>
/// Represents a task that runs during application startup, after DI container is built.
/// Use this for runtime initialization that requires resolved services (e.g., registering ingestors).
/// </summary>
/// <remarks>
/// Ordering convention:
/// - 0-99: Foundation tasks (base infrastructure)
/// - 100-199: Module tasks (module-specific initialization)
/// - 200+: Application tasks (host-specific setup).
/// </remarks>
public interface IStartupTask
{
    /// <summary>
    /// Gets the execution order. Lower values run first.
    /// </summary>
    int Order => 100;

    /// <summary>
    /// Gets a descriptive name for logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the startup task.
    /// </summary>
    /// <param name="serviceProvider">The application's service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
