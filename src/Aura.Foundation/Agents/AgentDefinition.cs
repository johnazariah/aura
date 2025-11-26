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
/// <param name="Capabilities">List of capabilities/tags.</param>
/// <param name="Tools">List of tools available to the agent.</param>
public sealed record AgentDefinition(
    string AgentId,
    string Name,
    string Description,
    string Provider,
    string Model,
    double Temperature,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Tools)
{
    /// <summary>
    /// Gets the default provider.
    /// </summary>
    public const string DefaultProvider = "ollama";

    /// <summary>
    /// Gets the default model.
    /// </summary>
    public const string DefaultModel = "qwen2.5-coder:7b";

    /// <summary>
    /// Gets the default temperature.
    /// </summary>
    public const double DefaultTemperature = 0.7;

    /// <summary>
    /// Creates an AgentMetadata instance from this definition.
    /// </summary>
    /// <returns>Agent metadata.</returns>
    public AgentMetadata ToMetadata() => new(
        Name: Name,
        Description: Description,
        Provider: Provider,
        Model: Model,
        Temperature: Temperature,
        Tools: Tools,
        Tags: Capabilities);
}
