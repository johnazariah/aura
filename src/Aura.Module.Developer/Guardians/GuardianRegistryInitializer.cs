// <copyright file="GuardianRegistryInitializer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Guardians;

using Aura.Foundation.Guardians;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that initializes the guardian registry on startup.
/// </summary>
public sealed class GuardianRegistryInitializer : IHostedService
{
    private readonly IGuardianRegistry _registry;
    private readonly GuardianOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GuardianRegistryInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardianRegistryInitializer"/> class.
    /// </summary>
    /// <param name="registry">Guardian registry.</param>
    /// <param name="options">Guardian options.</param>
    /// <param name="environment">Host environment for resolving paths.</param>
    /// <param name="logger">Logger instance.</param>
    public GuardianRegistryInitializer(
        IGuardianRegistry registry,
        IOptions<GuardianOptions> options,
        IHostEnvironment environment,
        ILogger<GuardianRegistryInitializer> logger)
    {
        _registry = registry;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing guardian registry");

        foreach (var directory in _options.Directories)
        {
            // Resolve relative paths against content root
            var resolvedPath = Path.IsPathRooted(directory)
                ? directory
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, directory));

            _logger.LogDebug("Adding guardian directory: {Original} -> {Resolved}", directory, resolvedPath);
            _registry.AddWatchDirectory(resolvedPath);
        }

        await _registry.ReloadAsync().ConfigureAwait(false);
        _logger.LogInformation("Guardian registry initialized with {Count} guardians", _registry.Guardians.Count);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Options for guardian configuration.
/// </summary>
public sealed class GuardianOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string Section = "Aura:Guardians";

    /// <summary>
    /// Gets or sets the directories to load guardians from.
    /// </summary>
    public IReadOnlyList<string> Directories { get; set; } = ["./guardians"];

    /// <summary>
    /// Gets or sets whether guardian scheduling is enabled.
    /// </summary>
    public bool EnableScheduling { get; set; } = true;
}
