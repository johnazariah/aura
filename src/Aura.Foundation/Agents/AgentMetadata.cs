// <copyright file="AgentMetadata.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Metadata describing an agent's capabilities and configuration.
/// </summary>
/// <param name="Name">Human-readable name for the agent.</param>
/// <param name="Description">Description of what the agent does.</param>
/// <param name="Provider">LLM provider to use (e.g., "ollama", "maf").</param>
/// <param name="Model">Model to use with the provider.</param>
/// <param name="Temperature">Temperature for LLM sampling (0.0-1.0).</param>
/// <param name="Tools">List of tool names this agent can use.</param>
/// <param name="Tags">Tags for categorization and filtering.</param>
public sealed record AgentMetadata(
    string Name,
    string Description,
    string Provider = "ollama",
    string Model = "qwen2.5-coder:7b",
    double Temperature = 0.7,
    IReadOnlyList<string>? Tools = null,
    IReadOnlyList<string>? Tags = null)
{
    /// <summary>
    /// Gets the tools available to this agent.
    /// </summary>
    public IReadOnlyList<string> Tools { get; } = Tools ?? [];

    /// <summary>
    /// Gets the tags for this agent.
    /// </summary>
    public IReadOnlyList<string> Tags { get; } = Tags ?? [];
}
