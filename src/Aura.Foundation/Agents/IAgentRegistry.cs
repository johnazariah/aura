// <copyright file="IAgentRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Registry for discovering and managing agents.
/// Supports hot-reload of agent definitions.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Gets all registered agents.
    /// </summary>
    IReadOnlyList<IAgent> Agents { get; }

    /// <summary>
    /// Gets an agent by ID.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns>The agent if found, null otherwise.</returns>
    IAgent? GetAgent(string agentId);

    /// <summary>
    /// Tries to get an agent by ID.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agent">The agent if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetAgent(string agentId, out IAgent? agent);

    /// <summary>
    /// Gets agents with the specified capability, sorted by priority (lowest first).
    /// If language is specified, filters to agents that support that language (or are polyglot).
    /// </summary>
    /// <param name="capability">The capability to filter by (from <see cref="Capabilities"/>).</param>
    /// <param name="language">Optional language to filter by (null = any language).</param>
    /// <returns>Matching agents sorted by priority (lowest = most specialized first).</returns>
    IReadOnlyList<IAgent> GetByCapability(string capability, string? language = null);

    /// <summary>
    /// Gets the best agent for a capability (lowest priority = most specialized).
    /// </summary>
    /// <param name="capability">The capability to find an agent for.</param>
    /// <param name="language">Optional language to filter by (null = any language).</param>
    /// <returns>The best matching agent, or null if none found.</returns>
    IAgent? GetBestForCapability(string capability, string? language = null);

    /// <summary>
    /// Gets agents matching the specified tags.
    /// </summary>
    /// <param name="tags">Tags to match (any match).</param>
    /// <returns>Matching agents.</returns>
    IReadOnlyList<IAgent> GetAgentsByTags(params string[] tags);

    /// <summary>
    /// Registers an agent.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    void Register(IAgent agent);

    /// <summary>
    /// Registers an agent.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    /// <param name="isHardcoded">Whether this is a hardcoded (non-markdown) agent that should not be removed during reloads.</param>
    void Register(IAgent agent, bool isHardcoded);

    /// <summary>
    /// Unregisters an agent.
    /// </summary>
    /// <param name="agentId">The agent ID to unregister.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool Unregister(string agentId);

    /// <summary>
    /// Reloads agents from the configured source directories.
    /// </summary>
    /// <returns>Task representing the reload operation.</returns>
    Task ReloadAsync();

    /// <summary>
    /// Event raised when agents are added or removed.
    /// </summary>
    event EventHandler<AgentRegistryChangedEventArgs>? AgentsChanged;
}

/// <summary>
/// Event args for agent registry changes.
/// </summary>
public sealed class AgentRegistryChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of change.
    /// </summary>
    public required AgentChangeType ChangeType { get; init; }

    /// <summary>
    /// Gets the affected agent ID.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the affected agent (null for removal).
    /// </summary>
    public IAgent? Agent { get; init; }
}

/// <summary>
/// Type of agent registry change.
/// </summary>
public enum AgentChangeType
{
    /// <summary>Agent was added.</summary>
    Added,

    /// <summary>Agent was updated.</summary>
    Updated,

    /// <summary>Agent was removed.</summary>
    Removed,
}
