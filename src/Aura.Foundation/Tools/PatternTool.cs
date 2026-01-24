// <copyright file="PatternTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the pattern.load tool.
/// </summary>
public record PatternLoadInput
{
    /// <summary>Pattern name (e.g., "generate-tests", "comprehensive-rename").</summary>
    public required string Name { get; init; }

    /// <summary>Optional language for overlay (e.g., "csharp", "python").</summary>
    public string? Language { get; init; }
}

/// <summary>
/// Input for the pattern.list tool.
/// </summary>
public record PatternListInput
{
    /// <summary>Optional language filter.</summary>
    public string? Language { get; init; }
}

/// <summary>
/// A pattern definition with metadata.
/// </summary>
public record PatternInfo
{
    /// <summary>Pattern name.</summary>
    public required string Name { get; init; }

    /// <summary>Pattern description (from first line).</summary>
    public required string Description { get; init; }

    /// <summary>Languages with overlays for this pattern.</summary>
    public string[] Overlays { get; init; } = [];

    /// <summary>Language (for language-specific patterns).</summary>
    public string? Language { get; init; }
}

/// <summary>
/// Output from the pattern.list tool.
/// </summary>
public record PatternListOutput
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Base patterns (polyglot).</summary>
    public required IReadOnlyList<PatternInfo> Patterns { get; init; }

    /// <summary>Language-specific patterns.</summary>
    public required IReadOnlyList<PatternInfo> LanguagePatterns { get; init; }

    /// <summary>Available languages.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>Message.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Output from the pattern.load tool.
/// </summary>
public record PatternLoadOutput
{
    /// <summary>Whether the pattern was found.</summary>
    public required bool Success { get; init; }

    /// <summary>Pattern name.</summary>
    public required string Name { get; init; }

    /// <summary>Language used.</summary>
    public string? Language { get; init; }

    /// <summary>Pattern content (markdown with instructions).</summary>
    public string? Content { get; init; }

    /// <summary>Error message if not found.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Tool for loading operational patterns (recipes for complex tasks).
/// Patterns provide step-by-step guidance for multi-step operations like
/// comprehensive renames, test generation, and feature implementation.
/// </summary>
public sealed class PatternLoadTool : TypedToolBase<PatternLoadInput, PatternLoadOutput>
{
    private readonly ILogger<PatternLoadTool> _logger;
    private string? _patternsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternLoadTool"/> class.
    /// </summary>
    public PatternLoadTool(ILogger<PatternLoadTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "pattern.load";

    /// <inheritdoc/>
    public override string Name => "Load Pattern";

    /// <inheritdoc/>
    public override string Description =>
        "Load an operational pattern (recipe) for complex multi-step tasks. " +
        "Patterns provide step-by-step instructions for operations like generating tests, " +
        "comprehensive renames, or feature implementation. Use pattern.list to see available patterns.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["patterns", "guidance"];

    /// <inheritdoc/>
    public override async Task<ToolResult<PatternLoadOutput>> ExecuteAsync(
        PatternLoadInput input,
        CancellationToken ct = default)
    {
        var patternsDir = GetPatternsDirectory();
        if (!Directory.Exists(patternsDir))
        {
            var output = new PatternLoadOutput
            {
                Success = false,
                Name = input.Name,
                Error = $"Patterns directory not found: {patternsDir}"
            };
            return ToolResult<PatternLoadOutput>.Ok(output);
        }

        var content = LoadPatternContent(input.Name, input.Language, patternsDir);
        if (content is null)
        {
            var output = new PatternLoadOutput
            {
                Success = false,
                Name = input.Name,
                Language = input.Language,
                Error = $"Pattern '{input.Name}' not found. Use pattern.list to see available patterns."
            };
            return ToolResult<PatternLoadOutput>.Ok(output);
        }

        _logger.LogInformation("Loaded pattern '{Name}' with language={Language}", input.Name, input.Language ?? "none");

        return ToolResult<PatternLoadOutput>.Ok(new PatternLoadOutput
        {
            Success = true,
            Name = input.Name,
            Language = input.Language,
            Content = content
        });
    }

    private string GetPatternsDirectory()
    {
        if (_patternsDirectory is not null)
        {
            return _patternsDirectory;
        }

        // Check common locations
        var candidates = new[]
        {
            // Production: relative to assembly
            Path.Combine(AppContext.BaseDirectory, "patterns"),
            // Development: from repository root
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "patterns"),
            // Windows service location
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Aura", "patterns"),
        };

        foreach (var candidate in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (Directory.Exists(normalized))
            {
                _patternsDirectory = normalized;
                return normalized;
            }
        }

        // Default to production location
        _patternsDirectory = candidates[0];
        return _patternsDirectory;
    }

    private static string? LoadPatternContent(string name, string? language, string patternsDir)
    {
        var basePatternPath = Path.Combine(patternsDir, $"{name}.md");
        var hasBasePattern = File.Exists(basePatternPath);

        // Language overlay path
        string? languageOverlayPath = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            languageOverlayPath = Path.Combine(patternsDir, language, $"{name}.md");
        }

        // Case 1: Language-specific pattern (no base, only in language folder)
        if (!hasBasePattern && languageOverlayPath != null && File.Exists(languageOverlayPath))
        {
            return File.ReadAllText(languageOverlayPath);
        }

        // Case 2: No base pattern and no language pattern
        if (!hasBasePattern)
        {
            return null;
        }

        // Case 3: Base pattern only (no language or no overlay)
        var baseContent = File.ReadAllText(basePatternPath);
        if (string.IsNullOrWhiteSpace(language) || languageOverlayPath == null || !File.Exists(languageOverlayPath))
        {
            return baseContent;
        }

        // Case 4: Merge base + language overlay
        var overlayContent = File.ReadAllText(languageOverlayPath);
        var merged = new StringBuilder();
        merged.AppendLine(baseContent);
        merged.AppendLine();
        merged.AppendLine("---");
        merged.AppendLine($"## Language-Specific Guidance: {language}");
        merged.AppendLine();
        merged.AppendLine(overlayContent);

        return merged.ToString();
    }
}

/// <summary>
/// Tool for listing available patterns.
/// </summary>
public sealed class PatternListTool : TypedToolBase<PatternListInput, PatternListOutput>
{
    private readonly ILogger<PatternListTool> _logger;
    private string? _patternsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternListTool"/> class.
    /// </summary>
    public PatternListTool(ILogger<PatternListTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "pattern.list";

    /// <inheritdoc/>
    public override string Name => "List Patterns";

    /// <inheritdoc/>
    public override string Description =>
        "List available operational patterns (recipes) for complex tasks. " +
        "Patterns provide step-by-step instructions for operations like test generation, renames, etc.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["patterns", "guidance"];

    /// <inheritdoc/>
    public override async Task<ToolResult<PatternListOutput>> ExecuteAsync(
        PatternListInput input,
        CancellationToken ct = default)
    {
        var patternsDir = GetPatternsDirectory();
        if (!Directory.Exists(patternsDir))
        {
            return ToolResult<PatternListOutput>.Ok(new PatternListOutput
            {
                Success = false,
                Patterns = [],
                LanguagePatterns = [],
                Languages = [],
                Message = $"Patterns directory not found: {patternsDir}"
            });
        }

        // Get available languages (subdirectories)
        var languages = Directory.GetDirectories(patternsDir)
            .Select(d => Path.GetFileName(d))
            .Where(n => !n.StartsWith('.'))
            .ToList();

        // Base patterns (polyglot)
        var patterns = Directory.GetFiles(patternsDir, "*.md")
            .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var content = File.ReadAllText(f);
                var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
                var description = firstLine.StartsWith("#")
                    ? firstLine.TrimStart('#', ' ')
                    : name;

                // Check which languages have overlays
                var overlays = languages
                    .Where(lang => File.Exists(Path.Combine(patternsDir, lang, $"{name}.md")))
                    .ToArray();

                return new PatternInfo { Name = name, Description = description, Overlays = overlays };
            })
            .ToList();

        // Language-specific patterns
        var languagePatterns = languages
            .SelectMany(lang =>
            {
                var langDir = Path.Combine(patternsDir, lang);
                return Directory.GetFiles(langDir, "*.md")
                    .Where(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        // Exclude overlays of base patterns
                        return !File.Exists(Path.Combine(patternsDir, $"{name}.md"));
                    })
                    .Select(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var content = File.ReadAllText(f);
                        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
                        var description = firstLine.StartsWith("#")
                            ? firstLine.TrimStart('#', ' ')
                            : name;

                        return new PatternInfo { Name = name, Description = description, Language = lang };
                    });
            })
            .ToList();

        _logger.LogInformation("Listed {BaseCount} base patterns, {LangCount} language patterns", patterns.Count, languagePatterns.Count);

        return ToolResult<PatternListOutput>.Ok(new PatternListOutput
        {
            Success = true,
            Patterns = patterns,
            LanguagePatterns = languagePatterns,
            Languages = languages,
            Message = $"Found {patterns.Count} base patterns, {languagePatterns.Count} language-specific patterns."
        });
    }

    private string GetPatternsDirectory()
    {
        if (_patternsDirectory is not null)
        {
            return _patternsDirectory;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "patterns"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "patterns"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Aura", "patterns"),
        };

        foreach (var candidate in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (Directory.Exists(normalized))
            {
                _patternsDirectory = normalized;
                return normalized;
            }
        }

        _patternsDirectory = candidates[0];
        return _patternsDirectory;
    }
}
