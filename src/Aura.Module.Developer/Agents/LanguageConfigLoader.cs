// <copyright file="LanguageConfigLoader.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Loads language configuration from YAML files.
/// </summary>
public interface ILanguageConfigLoader
{
    /// <summary>
    /// Loads a language configuration from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>The parsed language config, or null if invalid.</returns>
    Task<LanguageConfig?> LoadAsync(string filePath);

    /// <summary>
    /// Loads all language configurations from a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing YAML files.</param>
    /// <returns>List of loaded configurations.</returns>
    Task<IReadOnlyList<LanguageConfig>> LoadAllAsync(string directoryPath);

    /// <summary>
    /// Validates a language configuration.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    LanguageConfigValidationResult Validate(LanguageConfig config);
}

/// <summary>
/// Result of language configuration validation.
/// </summary>
/// <param name="IsValid">Whether the configuration is valid.</param>
/// <param name="Errors">List of validation errors.</param>
public sealed record LanguageConfigValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

/// <summary>
/// Default implementation of language config loader using YamlDotNet.
/// </summary>
public sealed class LanguageConfigLoader : ILanguageConfigLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<LanguageConfigLoader> _logger;
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageConfigLoader"/> class.
    /// </summary>
    public LanguageConfigLoader(
        IFileSystem fileSystem,
        ILogger<LanguageConfigLoader> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc/>
    public async Task<LanguageConfig?> LoadAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("Language config file does not exist: {Path}", filePath);
            return null;
        }

        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var config = _deserializer.Deserialize<LanguageConfig>(content);

            if (config is null)
            {
                _logger.LogWarning("Failed to deserialize language config from {Path}", filePath);
                return null;
            }

            var validation = Validate(config);
            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Language config {Path} has validation errors: {Errors}",
                    filePath,
                    string.Join("; ", validation.Errors));
                return null;
            }

            _logger.LogDebug(
                "Loaded language config for {Language} from {Path}",
                config.Language.Name,
                filePath);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading language config from {Path}", filePath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LanguageConfig>> LoadAllAsync(string directoryPath)
    {
        if (!_fileSystem.Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Language config directory does not exist: {Path}", directoryPath);
            return [];
        }

        var configs = new List<LanguageConfig>();
        var yamlFiles = _fileSystem.Directory.GetFiles(directoryPath, "*.yaml");

        _logger.LogInformation(
            "Loading language configs from {Path}, found {Count} YAML files",
            directoryPath,
            yamlFiles.Length);

        foreach (var file in yamlFiles)
        {
            var config = await LoadAsync(file).ConfigureAwait(false);
            if (config is not null)
            {
                configs.Add(config);
            }
        }

        _logger.LogInformation(
            "Successfully loaded {Count} language configurations: {Languages}",
            configs.Count,
            string.Join(", ", configs.Select(c => c.Language.Name)));

        return configs;
    }

    /// <inheritdoc/>
    public LanguageConfigValidationResult Validate(LanguageConfig config)
    {
        var errors = new List<string>();

        // Validate required fields
        if (config.Language is null)
        {
            errors.Add("'language' section is required");
        }
        else
        {
            if (string.IsNullOrEmpty(config.Language.Id))
            {
                errors.Add("'language.id' is required");
            }

            if (string.IsNullOrEmpty(config.Language.Name))
            {
                errors.Add("'language.name' is required");
            }
        }

        // Validate capabilities
        if (config.Capabilities.Count == 0)
        {
            errors.Add("At least one capability must be defined");
        }

        // Validate tools
        foreach (var (name, tool) in config.Tools)
        {
            if (string.IsNullOrEmpty(tool.Command))
            {
                errors.Add($"Tool '{name}' must have a 'command'");
            }

            if (string.IsNullOrEmpty(tool.Id))
            {
                errors.Add($"Tool '{name}' must have an 'id'");
            }
        }

        return new LanguageConfigValidationResult(errors.Count == 0, errors);
    }
}
