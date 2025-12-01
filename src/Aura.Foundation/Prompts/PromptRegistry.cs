// <copyright file="PromptRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Registry that loads prompt templates from .prompt files.
/// </summary>
public sealed partial class PromptRegistry : IPromptRegistry
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PromptRegistry> _logger;
    private readonly PromptOptions _options;
    private readonly ConcurrentDictionary<string, PromptTemplate> _prompts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRegistry"/> class.
    /// </summary>
    public PromptRegistry(
        IFileSystem fileSystem,
        IOptions<PromptOptions> options,
        ILogger<PromptRegistry> logger)
    {
        _fileSystem = fileSystem;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public PromptTemplate? GetPrompt(string name)
    {
        _prompts.TryGetValue(name, out var prompt);
        return prompt;
    }

    /// <inheritdoc/>
    public string Render(string name, object context)
    {
        var prompt = GetPrompt(name)
            ?? throw new InvalidOperationException($"Prompt '{name}' not found");

        return RenderTemplate(prompt.Template, context);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetPromptNames()
    {
        return _prompts.Keys.ToList();
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _prompts.Clear();

        foreach (var directory in _options.Directories)
        {
            LoadFromDirectory(directory);
        }

        _logger.LogInformation("Loaded {Count} prompts", _prompts.Count);
    }

    /// <summary>
    /// Loads prompts from a directory.
    /// </summary>
    public void LoadFromDirectory(string directory)
    {
        if (!_fileSystem.Directory.Exists(directory))
        {
            _logger.LogDebug("Prompt directory not found: {Directory}", directory);
            return;
        }

        var files = _fileSystem.Directory.GetFiles(directory, "*.prompt", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                LoadPromptFile(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load prompt: {File}", file);
            }
        }
    }

    private void LoadPromptFile(string filePath)
    {
        var content = _fileSystem.File.ReadAllText(filePath);
        var fileName = _fileSystem.Path.GetFileNameWithoutExtension(filePath);

        // Parse frontmatter if present (YAML-style)
        string? description = null;
        var template = content;

        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var frontmatter = content[3..endIndex].Trim();
                template = content[(endIndex + 3)..].Trim();

                // Simple parsing for description
                foreach (var line in frontmatter.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    {
                        description = trimmed["description:".Length..].Trim();
                    }
                }
            }
        }

        var prompt = new PromptTemplate
        {
            Name = fileName,
            Description = description,
            Template = template,
            SourcePath = filePath,
            LoadedAt = DateTimeOffset.UtcNow,
        };

        _prompts[fileName] = prompt;

        _logger.LogDebug("Loaded prompt: {Name} from {Path}", fileName, filePath);
    }

    /// <summary>
    /// Renders a template with the given context using simple variable substitution.
    /// Supports {{propertyName}} and {{nested.property}} syntax.
    /// </summary>
    private static string RenderTemplate(string template, object context)
    {
        var result = new StringBuilder(template);

        // Convert context to dictionary for easy lookup
        var properties = GetPropertiesAsDictionary(context);

        // Replace {{property}} patterns
        var matches = TemplateVariableRegex().Matches(template);
        foreach (Match match in matches.Cast<Match>().Reverse()) // Reverse to maintain positions
        {
            var variableName = match.Groups[1].Value.Trim();
            var value = GetPropertyValue(properties, variableName);

            result.Remove(match.Index, match.Length);
            result.Insert(match.Index, value ?? string.Empty);
        }

        return result.ToString();
    }

    private static Dictionary<string, object?> GetPropertiesAsDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (obj is IDictionary<string, object?> existingDict)
        {
            foreach (var kvp in existingDict)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            foreach (var prop in obj.GetType().GetProperties())
            {
                try
                {
                    dict[prop.Name] = prop.GetValue(obj);
                }
                catch
                {
                    // Skip properties that throw
                }
            }
        }

        return dict;
    }

    private static string? GetPropertyValue(Dictionary<string, object?> properties, string path)
    {
        var parts = path.Split('.');
        object? current = properties;

        foreach (var part in parts)
        {
            if (current is null)
            {
                return null;
            }

            if (current is IDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(part, out current))
                {
                    return null;
                }
            }
            else
            {
                var prop = current.GetType().GetProperty(part);
                if (prop is null)
                {
                    return null;
                }

                current = prop.GetValue(current);
            }
        }

        return current?.ToString();
    }

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    private static partial Regex TemplateVariableRegex();
}

/// <summary>
/// Configuration options for prompts.
/// </summary>
public sealed class PromptOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Prompts";

    /// <summary>Gets or sets directories to load prompts from.</summary>
    public List<string> Directories { get; set; } = ["prompts"];
}
