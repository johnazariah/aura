// <copyright file="AgentMetadata.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Metadata describing an agent's capabilities and configuration.
/// </summary>
/// <param name="Name">Human-readable name for the agent.</param>
/// <param name="Description">Description of what the agent does.</param>
/// <param name="Capabilities">Fixed vocabulary capabilities for routing (see <see cref="Agents.Capabilities"/>).</param>
/// <param name="Priority">Agent priority. Lower = more specialized, selected first. Default 50.</param>
/// <param name="Languages">Languages this agent supports (null = polyglot, handles any language).</param>
/// <param name="Provider">LLM provider to use (e.g., "ollama", "azureopenai"). Null = use system default.</param>
/// <param name="Model">Model to use with the provider.</param>
/// <param name="Temperature">Temperature for LLM sampling (0.0-1.0).</param>
/// <param name="Tools">List of tool names this agent can use.</param>
/// <param name="Tags">User-defined tags for filtering (open vocabulary).</param>
/// <param name="Reflection">Whether to enable self-critique before returning responses.</param>
/// <param name="ReflectionPrompt">Custom reflection prompt template name (default: agent-reflection).</param>
/// <param name="ReflectionModel">Model to use for reflection (default: same as agent).</param>
public sealed record AgentMetadata(
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    int Priority = 50,
    IReadOnlyList<string>? Languages = null,
    string? Provider = null,  // null = use configured default provider
    string? Model = null,
    double Temperature = 0.7,
    IReadOnlyList<string>? Tools = null,
    IReadOnlyList<string>? Tags = null,
    bool Reflection = false,
    string? ReflectionPrompt = null,
    string? ReflectionModel = null)
{
    /// <summary>
    /// Gets the capabilities for this agent (fixed vocabulary for routing).
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; } = Capabilities ?? [];

    /// <summary>
    /// Gets the languages this agent supports (empty = polyglot).
    /// </summary>
    public IReadOnlyList<string> Languages { get; } = Languages ?? [];

    /// <summary>
    /// Gets the tools available to this agent.
    /// </summary>
    public IReadOnlyList<string> Tools { get; } = Tools ?? [];

    /// <summary>
    /// Gets the tags for this agent (open vocabulary for user filtering).
    /// </summary>
    public IReadOnlyList<string> Tags { get; } = Tags ?? [];
}
