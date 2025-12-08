// <copyright file="AgentDefinition.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Parsed agent definition from a markdown file.
/// </summary>
/// <param name="AgentId">Unique identifier (typically filename without extension).</param>
/// <param name="Name">Display name for the agent.</param>
/// <param name="Description">Description of the agent's purpose.</param>
/// <param name="Provider">LLM provider (e.g., "ollama", "maf").</param>
/// <param name="Model">Model to use with the provider.</param>
/// <param name="Temperature">Temperature for LLM sampling.</param>
/// <param name="SystemPrompt">The system prompt template.</param>
/// <param name="Capabilities">Fixed vocabulary capabilities for routing.</param>
/// <param name="Priority">Agent priority. Lower = more specialized.</param>
/// <param name="Languages">Languages this agent supports (empty = polyglot).</param>
/// <param name="Tags">User-defined tags for filtering.</param>
/// <param name="Tools">List of tools available to the agent.</param>
public sealed record AgentDefinition(
    string AgentId,
    string Name,
    string Description,
    string Provider,
    string? Model,
    double Temperature,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities,
    int Priority,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Tools)
{
    /// <summary>
    /// Gets the default provider.
    /// </summary>
    public const string DefaultProvider = "ollama";

    /// <summary>
    /// Gets the default model. Null means use provider's configured default.
    /// </summary>
    public const string? DefaultModel = null;

    /// <summary>
    /// Gets the default temperature.
    /// </summary>
    public const double DefaultTemperature = 0.7;

    /// <summary>
    /// Gets the default priority.
    /// </summary>
    public const int DefaultPriority = 50;

    /// <summary>
    /// Creates an AgentMetadata instance from this definition.
    /// </summary>
    /// <returns>Agent metadata.</returns>
    public AgentMetadata ToMetadata() => new(
        Name: Name,
        Description: Description,
        Capabilities: Capabilities,
        Priority: Priority,
        Languages: Languages,
        Provider: Provider,
        Model: Model,
        Temperature: Temperature,
        Tools: Tools,
        Tags: Tags);
}
