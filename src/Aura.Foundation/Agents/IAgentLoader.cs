// <copyright file="IAgentLoader.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Interface for loading agents from files.
/// </summary>
public interface IAgentLoader
{
    /// <summary>
    /// Loads an agent from a file.
    /// </summary>
    /// <param name="filePath">Path to the agent definition file.</param>
    /// <returns>The loaded agent, or null if the file is not a valid agent.</returns>
    Task<IAgent?> LoadAsync(string filePath);
}
