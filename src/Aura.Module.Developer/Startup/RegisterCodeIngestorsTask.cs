// <copyright file="RegisterCodeIngestorsTask.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Startup;

using Aura.Foundation.Rag.Ingestors;
using Aura.Foundation.Startup;
using Aura.Module.Developer.Ingestors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Startup task that registers Developer module's code ingestors with the ingestor registry.
/// </summary>
public sealed class RegisterCodeIngestorsTask : IStartupTask
{
    /// <inheritdoc/>
    public int Order => 100; // Module-level priority

    /// <inheritdoc/>
    public string Name => "Register Developer Module Code Ingestors";

    /// <inheritdoc/>
    public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var registry = serviceProvider.GetRequiredService<IIngestorRegistry>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Register RoslynCodeIngestor for C# files
        // Priority: This will be inserted at the front of the registry, taking precedence
        // over the generic CodeIngestor for .cs files
        var roslynIngestor = new RoslynCodeIngestor(loggerFactory.CreateLogger<RoslynCodeIngestor>());
        registry.Register(roslynIngestor);

        var logger = loggerFactory.CreateLogger<RegisterCodeIngestorsTask>();
        logger.LogInformation(
            "Registered {Ingestor} for extensions: {Extensions}",
            roslynIngestor.IngestorId,
            string.Join(", ", roslynIngestor.SupportedExtensions));

        return Task.CompletedTask;
    }
}
