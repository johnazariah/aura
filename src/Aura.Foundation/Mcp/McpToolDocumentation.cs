// <copyright file="McpToolDocumentation.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Mcp;

/// <summary>
/// Single source of truth for MCP tool documentation.
/// Used by both the MCP server and copilot-instructions generation.
/// </summary>
public static class McpToolDocumentation
{
    private const string InstructionsFileName = "mcp-tools-instructions.md";
    private static string? _cachedInstructions;

    /// <summary>
    /// Gets the markdown documentation for Aura MCP tools, suitable for copilot-instructions.md.
    /// Loaded from prompts/mcp-tools-instructions.md at runtime.
    /// </summary>
    /// <param name="promptsDirectory">Path to the prompts directory.</param>
    /// <returns>The markdown content, or a fallback message if the file is not found.</returns>
    public static string GetCopilotInstructionsMarkdown(string promptsDirectory)
    {
        if (_cachedInstructions is not null)
            return _cachedInstructions;

        var filePath = Path.Combine(promptsDirectory, InstructionsFileName);
        if (File.Exists(filePath))
        {
            _cachedInstructions = File.ReadAllText(filePath);
            return _cachedInstructions;
        }

        return $"<!-- MCP tools documentation not found at {filePath} -->";
    }

    /// <summary>
    /// Clears the cached instructions (useful for hot-reload scenarios).
    /// </summary>
    public static void ClearCache() => _cachedInstructions = null;

    /// <summary>
    /// Tool names for validation and discoverability.
    /// </summary>
    public static readonly IReadOnlyList<string> ToolNames = new[]
    {
        "aura_search",
        "aura_navigate",
        "aura_inspect",
        "aura_refactor",
        "aura_generate",
        "aura_validate",
        "aura_workflow",
        "aura_architect",
        "aura_workspace",
        "aura_pattern",
    };
}
