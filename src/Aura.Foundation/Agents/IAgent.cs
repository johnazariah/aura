// <copyright file="IAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Core contract for all agents in the Aura system.
/// Agents process work items using LLM providers.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the unique identifier for this agent.
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Gets the metadata describing this agent's capabilities.
    /// </summary>
    AgentMetadata Metadata { get; }

    /// <summary>
    /// Executes the agent with the provided context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent output.</returns>
    /// <exception cref="AgentException">Thrown when execution fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
    Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}
