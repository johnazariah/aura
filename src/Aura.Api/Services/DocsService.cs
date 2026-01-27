// <copyright file="DocsService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Services;

using System.Reflection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Service for managing and retrieving Aura documentation from embedded resources.
/// </summary>
public sealed class DocsService : IDocsService
{
    private readonly DocsRegistry _registry;
    private readonly Assembly _assembly;
    private readonly ILogger<DocsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DocsService(ILogger<DocsService> logger)
    {
        _logger = logger;
        _assembly = typeof(DocsService).Assembly;
        _registry = LoadRegistry();
    }

    /// <inheritdoc/>
    public IReadOnlyList<DocumentEntry> ListDocuments(string? category = null, IReadOnlyList<string>? tags = null)
    {
        var query = _registry.Documents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (tags is not null && tags.Count > 0)
        {
            // OR logic - matches ANY tag
            query = query.Where(d => d.Tags.Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        return query.Select(d => new DocumentEntry(
            d.Id,
            d.Title,
            d.Summary,
            d.Category,
            d.Tags)).ToList();
    }

    /// <inheritdoc/>
    public DocumentContent? GetDocument(string id)
    {
        var entry = _registry.Documents.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            _logger.LogWarning("Document with ID '{DocumentId}' not found in registry", id);
            return null;
        }

        var resourceName = $"Aura.Api.Docs.{entry.Path.Replace("/", ".")}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogError(
                "Embedded resource '{ResourceName}' not found for document '{DocumentId}'",
                resourceName,
                id);
            return null;
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        return new DocumentContent(
            entry.Id,
            entry.Title,
            entry.Category,
            entry.Tags,
            content,
            DateTime.UtcNow.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// Loads the document registry from the embedded YAML resource.
    /// </summary>
    /// <returns>The deserialized document registry.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the registry cannot be loaded.</exception>
    private DocsRegistry LoadRegistry()
    {
        const string RegistryResourceName = "Aura.Api.Docs.registry.yaml";

        using var stream = _assembly.GetManifestResourceStream(RegistryResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{RegistryResourceName}' not found. " +
                "Ensure Docs/registry.yaml is marked as EmbeddedResource in the project file.");
        }

        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        try
        {
            var registry = deserializer.Deserialize<DocsRegistry>(yaml);
            _logger.LogInformation(
                "Loaded document registry with {DocumentCount} documents",
                registry.Documents.Count);
            return registry;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize document registry from '{RegistryResourceName}'",
                ex);
        }
    }
}

/// <summary>
/// Internal model for deserializing the YAML document registry.
/// </summary>
internal sealed record DocsRegistry
{
    /// <summary>
    /// Gets or sets the list of document entries in the registry.
    /// </summary>
    public List<RegistryDocument> Documents { get; set; } = [];
}

/// <summary>
/// Internal model for a document entry in the registry.
/// </summary>
internal sealed record RegistryDocument
{
    /// <summary>
    /// Gets or sets the unique identifier of the document.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the document.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the summary of the document.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the document.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags associated with the document.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the relative path to the document file.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
