// <copyright file="IAgentFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Factory for creating agents from definitions.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates an agent from a definition.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <returns>The created agent.</returns>
    IAgent CreateAgent(AgentDefinition definition);
}
