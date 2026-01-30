// <copyright file="IngesterContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Typed context for file ingester agents.
/// Contains the file path, content, and language information needed for parsing.
/// </summary>
/// <param name="FilePath">The path to the file being ingested.</param>
/// <param name="Content">The content of the file.</param>
/// <param name="Language">The language or file extension (without dot).</param>
public sealed record IngesterContext(
    string FilePath,
    string Content,
    string? Language = null)
{
    /// <summary>
    /// Gets the file extension (without dot) from the file path.
    /// </summary>
    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();
}

/// <summary>
/// Extension methods for working with <see cref="IngesterContext"/> in <see cref="AgentContext"/>.
/// </summary>
public static class IngesterContextExtensions
{
    /// <summary>
    /// Property key for storing ingester context.
    /// </summary>
    internal const string IngesterContextKey = "__ingesterContext";

    /// <summary>
    /// Property key for file path (legacy, for backwards compatibility).
    /// </summary>
    internal const string FilePathKey = "filePath";

    /// <summary>
    /// Property key for content (legacy, for backwards compatibility).
    /// </summary>
    internal const string ContentKey = "content";

    /// <summary>
    /// Property key for language (legacy, for backwards compatibility).
    /// </summary>
    internal const string LanguageKey = "language";

    /// <summary>
    /// Property key for extension (legacy, for backwards compatibility).
    /// </summary>
    internal const string ExtensionKey = "extension";

    /// <summary>
    /// Creates an <see cref="AgentContext"/> with ingester context.
    /// </summary>
    /// <param name="ingesterContext">The ingester context.</param>
    /// <param name="prompt">Optional prompt text.</param>
    /// <returns>A new agent context with the ingester context attached.</returns>
    public static AgentContext ToAgentContext(this IngesterContext ingesterContext, string? prompt = null)
    {
        return new AgentContext(
            Prompt: prompt ?? "Parse this file and extract semantic chunks",
            Properties: new Dictionary<string, object>
            {
                [IngesterContextKey] = ingesterContext,
                // Also set legacy keys for backwards compatibility
                [FilePathKey] = ingesterContext.FilePath,
                [ContentKey] = ingesterContext.Content,
                [LanguageKey] = ingesterContext.Language ?? ingesterContext.Extension,
                [ExtensionKey] = ingesterContext.Extension,
            });
    }

    /// <summary>
    /// Gets the ingester context from an agent context.
    /// Supports both new typed context and legacy dictionary-based context.
    /// </summary>
    /// <param name="context">The agent context.</param>
    /// <returns>The ingester context, or null if not available.</returns>
    public static IngesterContext? GetIngesterContext(this AgentContext context)
    {
        // Try new typed context first
        if (context.Properties.TryGetValue(IngesterContextKey, out var obj) && obj is IngesterContext typed)
        {
            return typed;
        }

        // Fall back to legacy dictionary-based context
        var filePath = context.Properties.GetValueOrDefault(FilePathKey) as string;
        var content = context.Properties.GetValueOrDefault(ContentKey) as string
            ?? context.Prompt;
        var language = context.Properties.GetValueOrDefault(LanguageKey) as string;

        if (string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(content))
        {
            return null;
        }

        return new IngesterContext(
            FilePath: filePath ?? "unknown",
            Content: content ?? string.Empty,
            Language: language);
    }

    /// <summary>
    /// Gets the ingester context from an agent context, throwing if filePath or content is missing.
    /// This provides strict validation for agents that require both fields.
    /// </summary>
    /// <param name="context">The agent context.</param>
    /// <returns>The ingester context.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath or content is not available.</exception>
    public static IngesterContext GetRequiredIngesterContext(this AgentContext context)
    {
        // Try new typed context first
        if (context.Properties.TryGetValue(IngesterContextKey, out var obj) && obj is IngesterContext typed)
        {
            return typed;
        }

        // Fall back to legacy dictionary-based context with strict validation
        var filePath = context.Properties.GetValueOrDefault(FilePathKey) as string;
        var content = context.Properties.GetValueOrDefault(ContentKey) as string;

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("filePath is required");
        }

        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("content is required");
        }

        var language = context.Properties.GetValueOrDefault(LanguageKey) as string;

        return new IngesterContext(
            FilePath: filePath,
            Content: content,
            Language: language);
    }
}
