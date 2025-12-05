// <copyright file="PromptRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Prompts;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Registry that loads prompt templates from .prompt files.
/// Uses Handlebars.Net for full Handlebars template support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PromptRegistry"/> class.
/// </remarks>
public sealed class PromptRegistry : IPromptRegistry
{
    private readonly IFileSystem _fileSystem;
    private readonly PromptOptions _options;
    private readonly ILogger<PromptRegistry> _logger;
    private readonly IHandlebars _handlebars;
    private readonly ConcurrentDictionary<string, PromptTemplate> _prompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> _compiledTemplates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRegistry"/> class.
    /// </summary>
    public PromptRegistry(
        IFileSystem fileSystem,
        IOptions<PromptOptions> options,
        IHandlebars handlebars,
        ILogger<PromptRegistry> logger)
    {
        _fileSystem = fileSystem;
        _options = options.Value;
        _handlebars = handlebars;
        _logger = logger;

        // Register custom helpers
        RegisterCustomHelpers();
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
        if (!_compiledTemplates.TryGetValue(name, out var template))
        {
            var prompt = GetPrompt(name)
                ?? throw new InvalidOperationException($"Prompt '{name}' not found");

            template = _handlebars.Compile(prompt.Template);
            _compiledTemplates[name] = template;
        }

        return template(context);
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public IReadOnlyList<string> GetPromptNames()
    {
        return _prompts.Keys.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRagQueries(string name)
    {
        if (_prompts.TryGetValue(name, out var prompt))
        {
            return prompt.RagQueries;
        }

        return [];
    }

    /// <inheritdoc/>
    public void Reload()
    {
        _prompts.Clear();
        _compiledTemplates.Clear();

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
        var ragQueries = new List<string>();
        var template = content;
        var inRagQueries = false;

        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var frontmatter = content[3..endIndex].Trim();
                template = content[(endIndex + 3)..].Trim();

                // Simple parsing for frontmatter fields
                foreach (var line in frontmatter.Split('\n'))
                {
                    var trimmed = line.Trim();

                    // Check for ragQueries list items (lines starting with -)
                    if (inRagQueries && trimmed.StartsWith("-"))
                    {
                        var query = trimmed[1..].Trim().Trim('"', '\'');
                        if (!string.IsNullOrEmpty(query))
                        {
                            ragQueries.Add(query);
                        }

                        continue;
                    }

                    // End of ragQueries section when we hit another key
                    if (inRagQueries && !trimmed.StartsWith("-") && trimmed.Contains(':'))
                    {
                        inRagQueries = false;
                    }

                    if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    {
                        description = trimmed["description:".Length..].Trim();
                    }
                    else if (trimmed.StartsWith("ragQueries:", StringComparison.OrdinalIgnoreCase))
                    {
                        inRagQueries = true;
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
            RagQueries = ragQueries,
        };

        _prompts[fileName] = prompt;

        // Pre-compile the template
        try
        {
            _compiledTemplates[fileName] = _handlebars.Compile(template);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compile prompt template: {Name}", fileName);
        }

        _logger.LogDebug("Loaded prompt: {Name} from {Path}", fileName, filePath);
    }

    private void RegisterCustomHelpers()
    {
        // Helper: {{truncate text maxLength}}
        _handlebars.RegisterHelper("truncate", (output, context, arguments) =>
        {
            if (arguments.Length < 2)
            {
                return;
            }

            var text = arguments[0]?.ToString() ?? string.Empty;
            if (arguments[1] is int maxLength || int.TryParse(arguments[1]?.ToString(), out maxLength))
            {
                if (text.Length > maxLength)
                {
                    output.Write(text[..maxLength] + "...");
                }
                else
                {
                    output.Write(text);
                }
            }
            else
            {
                output.Write(text);
            }
        });

        // Helper: {{json object}} - Serialize object to JSON
        _handlebars.RegisterHelper("json", (output, context, arguments) =>
        {
            if (arguments.Length < 1)
            {
                return;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(
                arguments[0],
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            output.Write(json);
        });

        // Helper: {{lowercase text}}
        _handlebars.RegisterHelper("lowercase", (output, context, arguments) =>
        {
            if (arguments.Length < 1)
            {
                return;
            }

            output.Write(arguments[0]?.ToString()?.ToLowerInvariant() ?? string.Empty);
        });

        // Helper: {{uppercase text}}
        _handlebars.RegisterHelper("uppercase", (output, context, arguments) =>
        {
            if (arguments.Length < 1)
            {
                return;
            }

            output.Write(arguments[0]?.ToString()?.ToUpperInvariant() ?? string.Empty);
        });

        // Helper: {{join array separator}}
        _handlebars.RegisterHelper("join", (output, context, arguments) =>
        {
            if (arguments.Length < 2)
            {
                return;
            }

            if (arguments[0] is System.Collections.IEnumerable enumerable and not string)
            {
                var separator = arguments[1]?.ToString() ?? ", ";
                var items = enumerable.Cast<object>().Select(x => x?.ToString() ?? string.Empty);
                output.Write(string.Join(separator, items));
            }
        });

        // Block helper: {{#ifEquals a b}}...{{else}}...{{/ifEquals}}
        _handlebars.RegisterHelper("ifEquals", (output, options, context, arguments) =>
        {
            if (arguments.Length < 2)
            {
                options.Inverse(output, context);
                return;
            }

            var a = arguments[0]?.ToString();
            var b = arguments[1]?.ToString();

            if (string.Equals(a, b, StringComparison.Ordinal))
            {
                options.Template(output, context);
            }
            else
            {
                options.Inverse(output, context);
            }
        });

        // Block helper: {{#ifContains text substring}}...{{else}}...{{/ifContains}}
        _handlebars.RegisterHelper("ifContains", (output, options, context, arguments) =>
        {
            if (arguments.Length < 2)
            {
                options.Inverse(output, context);
                return;
            }

            var text = arguments[0]?.ToString() ?? string.Empty;
            var substring = arguments[1]?.ToString() ?? string.Empty;

            if (text.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                options.Template(output, context);
            }
            else
            {
                options.Inverse(output, context);
            }
        });
    }
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
