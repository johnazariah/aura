using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;
public sealed partial class McpHandler
{
    // =========================================================================
    // aura_pattern - Load operational patterns for complex tasks
    // =========================================================================
    /// <summary>
        /// aura_pattern - Load operational patterns for complex multi-step tasks.
        /// Patterns are dynamically discovered from the patterns/ folder.
        /// </summary>
        private Task<object> PatternAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString() ?? throw new ArgumentException("operation is required");
        return operation switch
        {
            "list" => Task.FromResult(ListPatternsOperation()),
            "get" => Task.FromResult(GetPatternOperation(args)),
            _ => throw new ArgumentException($"Unknown pattern operation: {operation}")
        };
    }

    private object ListPatternsOperation()
    {
        var patternsDir = GetPatternsDirectory();
        if (!Directory.Exists(patternsDir))
        {
            return new
            {
                success = false,
                patterns = Array.Empty<object>(),
                languagePatterns = Array.Empty<object>(),
                languages = Array.Empty<string>(),
                message = $"Patterns directory not found: {patternsDir}"
            };
        }

        // Get available languages (subdirectories)
        var languages = Directory.GetDirectories(patternsDir).Select(d => Path.GetFileName(d)).Where(n => !n.StartsWith('.')).ToArray();
        // Base patterns (polyglot)
        var patterns = Directory.GetFiles(patternsDir, "*.md").Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase)).Select(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var content = File.ReadAllText(f);
            var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
            var description = firstLine.StartsWith("#") ? firstLine.TrimStart('#', ' ') : name;
            // Check which languages have overlays for this pattern
            var overlays = languages.Where(lang => File.Exists(Path.Combine(patternsDir, lang, $"{name}.md"))).ToArray();
            return new
            {
                name,
                description,
                overlays
            };
        }).ToArray();
        // Language-specific patterns (no base, only in language folder)
        var languagePatterns = languages.SelectMany(lang =>
        {
            var langDir = Path.Combine(patternsDir, lang);
            return Directory.GetFiles(langDir, "*.md").Where(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                // Exclude patterns that are overlays of base patterns
                return !File.Exists(Path.Combine(patternsDir, $"{name}.md"));
            }).Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var content = File.ReadAllText(f);
                var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
                var description = firstLine.StartsWith("#") ? firstLine.TrimStart('#', ' ') : name;
                return new
                {
                    name,
                    language = lang,
                    description
                };
            });
        }).ToArray();
        return new
        {
            success = true,
            patterns,
            languagePatterns,
            languages,
            message = $"Found {patterns.Length} base patterns, {languagePatterns.Length} language-specific patterns. Use aura_pattern(operation: 'get', name: '...', language: '...') to load."
        };
    }

    private object GetPatternOperation(JsonElement? args)
    {
        var name = args?.TryGetProperty("name", out var nameProp) == true ? nameProp.GetString() : null;
        var language = args?.TryGetProperty("language", out var langProp) == true ? langProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name is required for 'get' operation");
        }

        var patternsDir = GetPatternsDirectory();
        var basePatternPath = Path.Combine(patternsDir, $"{name}.md");
        var hasBasePattern = File.Exists(basePatternPath);
        // Check for language-specific pattern (no base)
        string? langOnlyPatternPath = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            langOnlyPatternPath = Path.Combine(patternsDir, language, $"{name}.md");
        }

        // Case 1: Base pattern exists
        if (hasBasePattern)
        {
            var baseContent = File.ReadAllText(basePatternPath);
            string? overlayContent = null;
            var hasOverlay = false;
            // Check for language overlay
            if (!string.IsNullOrWhiteSpace(language))
            {
                var overlayPath = Path.Combine(patternsDir, language, $"{name}.md");
                if (File.Exists(overlayPath))
                {
                    overlayContent = File.ReadAllText(overlayPath);
                    hasOverlay = true;
                }
            }

            // Merge base + overlay if overlay exists
            var finalContent = hasOverlay ? $"{baseContent}\n\n---\n\n# {language!.ToUpperInvariant()} Language Overlay\n\n{overlayContent}" : baseContent;
            var message = hasOverlay ? $"Loaded pattern '{name}' with {language} overlay. Follow the steps in this pattern." : !string.IsNullOrWhiteSpace(language) ? $"Pattern '{name}' loaded (no {language} overlay found). Follow the steps in this pattern." : "Follow the steps in this pattern. Do not deviate.";
            return new
            {
                success = true,
                name,
                language,
                hasOverlay,
                isLanguageSpecific = false,
                content = finalContent,
                message
            };
        }

        // Case 2: Language-specific pattern (no base)
        if (langOnlyPatternPath != null && File.Exists(langOnlyPatternPath))
        {
            var content = File.ReadAllText(langOnlyPatternPath);
            return new
            {
                success = true,
                name,
                language,
                hasOverlay = false,
                isLanguageSpecific = true,
                content,
                message = $"Loaded {language}-specific pattern '{name}'. Follow the steps in this pattern."
            };
        }

        // Case 3: Not found
        return new
        {
            success = false,
            name,
            language,
            hasOverlay = false,
            isLanguageSpecific = false,
            content = (string?)null,
            message = $"Pattern '{name}' not found. Use aura_pattern(operation: 'list') to see available patterns."
        };
    }

    private static string GetPatternsDirectory()
    {
        // Try relative to the base directory of the executing assembly
        var basePath = AppContext.BaseDirectory;
        var absolutePath = Path.Combine(basePath, "patterns");
        if (Directory.Exists(absolutePath))
        {
            return absolutePath;
        }

        // Try one level up from base directory (installed layout: api\ is sibling to patterns\)
        var parentPath = Path.GetDirectoryName(basePath.TrimEnd(Path.DirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parentPath))
        {
            var siblingPath = Path.Combine(parentPath, "patterns");
            if (Directory.Exists(siblingPath))
            {
                return siblingPath;
            }
        }

        // Default fallback - will fail gracefully with "not found" message
        return Path.Combine(basePath, "patterns");
    }

    /// <summary>
        /// Loads pattern content with optional language overlay.
        /// Returns merged base + overlay if both exist, or just the pattern if no overlay.
        /// </summary>
        private static string? LoadPatternContent(string patternName, string? language)
    {
        var patternsDir = GetPatternsDirectory();
        var basePatternPath = Path.Combine(patternsDir, $"{patternName}.md");
        var hasBasePattern = File.Exists(basePatternPath);
        // Check for language-specific pattern path
        string? langPatternPath = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            langPatternPath = Path.Combine(patternsDir, language, $"{patternName}.md");
        }

        // Case 1: Base pattern exists
        if (hasBasePattern)
        {
            var baseContent = File.ReadAllText(basePatternPath);
            // Check for language overlay
            if (langPatternPath != null && File.Exists(langPatternPath))
            {
                var overlayContent = File.ReadAllText(langPatternPath);
                return $"{baseContent}\n\n---\n\n# {language!.ToUpperInvariant()} Language Overlay\n\n{overlayContent}";
            }

            return baseContent;
        }

        // Case 2: Language-specific pattern only (no base)
        if (langPatternPath != null && File.Exists(langPatternPath))
        {
            return File.ReadAllText(langPatternPath);
        }

        // Pattern not found
        return null;
    }
}
