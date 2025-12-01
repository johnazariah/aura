// <copyright file="IngestorRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

using Microsoft.Extensions.Logging;

/// <summary>
/// Registry for content ingestors. Routes files to appropriate ingestors based on extension.
/// </summary>
public interface IIngestorRegistry
{
    /// <summary>
    /// Gets all registered ingestors.
    /// </summary>
    IReadOnlyList<IContentIngestor> Ingestors { get; }

    /// <summary>
    /// Gets the best ingestor for a file path.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The best ingestor, or null if none found.</returns>
    IContentIngestor? GetIngestor(string filePath);

    /// <summary>
    /// Registers a new ingestor.
    /// </summary>
    /// <param name="ingestor">The ingestor to register.</param>
    void Register(IContentIngestor ingestor);
}

/// <summary>
/// Default implementation of <see cref="IIngestorRegistry"/>.
/// </summary>
public sealed class IngestorRegistry : IIngestorRegistry
{
    private readonly List<IContentIngestor> _ingestors = [];
    private readonly IContentIngestor _fallback;
    private readonly ILogger<IngestorRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestorRegistry"/> class.
    /// </summary>
    public IngestorRegistry(ILogger<IngestorRegistry> logger)
    {
        _logger = logger;
        _fallback = new PlainTextIngestor();

        // Register default ingestors
        Register(new MarkdownIngestor());
        Register(new CodeIngestor());
        Register(_fallback);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IContentIngestor> Ingestors => _ingestors.AsReadOnly();

    /// <inheritdoc/>
    public IContentIngestor? GetIngestor(string filePath)
    {
        // Try specific ingestors first (in registration order)
        foreach (var ingestor in _ingestors)
        {
            if (ingestor.CanIngest(filePath))
            {
                _logger.LogDebug(
                    "Using {Ingestor} for {FilePath}",
                    ingestor.IngestorId,
                    filePath);
                return ingestor;
            }
        }

        // Fall back to plain text
        _logger.LogDebug("No specific ingestor for {FilePath}, using plaintext fallback", filePath);
        return _fallback;
    }

    /// <inheritdoc/>
    public void Register(IContentIngestor ingestor)
    {
        // Add at the beginning so newer registrations take priority
        // (except for plaintext which should always be last)
        if (ingestor is PlainTextIngestor)
        {
            _ingestors.Add(ingestor);
        }
        else
        {
            _ingestors.Insert(0, ingestor);
        }

        _logger.LogDebug(
            "Registered ingestor {Ingestor} for extensions: {Extensions}",
            ingestor.IngestorId,
            string.Join(", ", ingestor.SupportedExtensions));
    }
}
