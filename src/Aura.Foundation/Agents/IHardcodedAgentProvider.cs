// <copyright file="IHardcodedAgentProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Provider for hardcoded (C#-based) agents.
/// Modules implement this interface to register agents that need direct .NET implementation
/// rather than markdown-based configuration.
/// </summary>
public interface IHardcodedAgentProvider
{
    /// <summary>
    /// Gets the hardcoded agents provided by this provider.
    /// Called during registry initialization to register agents.
    /// </summary>
    /// <returns>Collection of hardcoded agents.</returns>
    IEnumerable<IAgent> GetAgents();
}
