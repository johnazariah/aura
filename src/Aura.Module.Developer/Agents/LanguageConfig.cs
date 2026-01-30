// <copyright file="LanguageConfig.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using Aura.Foundation.Llm;

/// <summary>
/// Configuration for a language specialist agent loaded from YAML.
/// </summary>
public sealed record LanguageConfig
{
    /// <summary>Gets the language metadata.</summary>
    public required LanguageMetadata Language { get; init; }

    /// <summary>Gets the capabilities this agent provides.</summary>
    public List<string> Capabilities { get; init; } = [];

    /// <summary>Gets the agent priority (lower = more specialized).</summary>
    public int Priority { get; init; } = 10;

    /// <summary>Gets the agent configuration.</summary>
    public AgentConfig Agent { get; init; } = new();

    /// <summary>Gets the tool definitions.</summary>
    public Dictionary<string, ToolConfig> Tools { get; init; } = [];

    /// <summary>Gets the prompt template sections.</summary>
    public PromptConfig Prompt { get; init; } = new();
}

/// <summary>
/// Language metadata.
/// </summary>
public sealed record LanguageMetadata
{
    /// <summary>Gets the language identifier (e.g., "python", "fsharp").</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name (e.g., "Python", "F#").</summary>
    public required string Name { get; init; }

    /// <summary>Gets the file extensions for this language.</summary>
    public List<string> Extensions { get; init; } = [];

    /// <summary>Gets the project file patterns.</summary>
    public List<string> ProjectFiles { get; init; } = [];
}

/// <summary>
/// Agent configuration from YAML.
/// </summary>
public sealed record AgentConfig
{
    /// <summary>Gets the LLM provider.</summary>
    public string Provider { get; init; } = LlmProviders.Ollama;

    /// <summary>Gets the model to use.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the temperature.</summary>
    public double Temperature { get; init; } = 0.1;

    /// <summary>Gets the max ReAct steps.</summary>
    public int MaxSteps { get; init; } = 15;
}

/// <summary>
/// Tool configuration from YAML.
/// </summary>
public sealed record ToolConfig
{
    /// <summary>Gets the tool identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the CLI command.</summary>
    public required string Command { get; init; }

    /// <summary>Gets the command arguments.</summary>
    public List<string> Args { get; init; } = [];

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the tool categories.</summary>
    public List<string> Categories { get; init; } = [];

    /// <summary>Gets whether confirmation is required.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Gets the position for path argument (-1 = last).</summary>
    public int? PathArg { get; init; }

    /// <summary>Gets the position for project argument.</summary>
    public int? ProjectArg { get; init; }

    /// <summary>Gets the position for script argument.</summary>
    public int? ScriptArg { get; init; }

    /// <summary>Gets the configuration argument prefix.</summary>
    public List<string>? ConfigArg { get; init; }

    /// <summary>Gets the fallback command if primary fails.</summary>
    public FallbackConfig? Fallback { get; init; }

    /// <summary>Gets the output parsers.</summary>
    public Dictionary<string, OutputParserConfig>? OutputParsers { get; init; }
}

/// <summary>
/// Fallback command configuration.
/// </summary>
public sealed record FallbackConfig
{
    /// <summary>Gets the fallback command.</summary>
    public required string Command { get; init; }

    /// <summary>Gets the fallback arguments.</summary>
    public List<string> Args { get; init; } = [];
}

/// <summary>
/// Output parser configuration.
/// </summary>
public sealed record OutputParserConfig
{
    /// <summary>Gets the parser type (lineMatch, regex, json, exitCode).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the pattern for lineMatch or regex parsers.</summary>
    public string? Pattern { get; init; }

    /// <summary>Gets whether to ignore case.</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>Gets the named groups for regex parsers.</summary>
    public List<string>? Groups { get; init; }

    /// <summary>Gets the JSONPath for json parsers.</summary>
    public string? Path { get; init; }

    /// <summary>Gets the success codes for exitCode parsers.</summary>
    public List<int>? SuccessCodes { get; init; }
}

/// <summary>
/// Prompt template configuration from YAML.
/// </summary>
public sealed record PromptConfig
{
    /// <summary>Gets the workflow instructions.</summary>
    public string Workflow { get; init; } = string.Empty;

    /// <summary>Gets the available tools description.</summary>
    public string? AvailableTools { get; init; }

    /// <summary>Gets the best practices.</summary>
    public string BestPractices { get; init; } = string.Empty;

    /// <summary>Gets the syntax reminders.</summary>
    public string SyntaxReminders { get; init; } = string.Empty;

    /// <summary>Gets the project structure guidance.</summary>
    public string? ProjectStructure { get; init; }
}
