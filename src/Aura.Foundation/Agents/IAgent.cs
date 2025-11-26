// <copyright file="IAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using CSharpFunctionalExtensions;

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
    /// <returns>Result containing output or error.</returns>
    Task<Result<AgentOutput, AgentError>> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}
