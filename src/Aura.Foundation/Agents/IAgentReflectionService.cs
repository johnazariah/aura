// <copyright file="IAgentReflectionService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Service for applying self-reflection/self-critique to agent responses.
/// </summary>
public interface IAgentReflectionService
{
    /// <summary>
    /// Applies reflection to an agent response, potentially correcting it.
    /// </summary>
    /// <param name="task">The original task/prompt given to the agent.</param>
    /// <param name="response">The agent's response to review.</param>
    /// <param name="agentMetadata">The agent's metadata (for reflection settings).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reviewed (and potentially corrected) response.</returns>
    Task<ReflectionResult> ReflectAsync(
        string task,
        string response,
        AgentMetadata agentMetadata,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a reflection operation.
/// </summary>
/// <param name="Content">The final content (original if approved, corrected otherwise).</param>
/// <param name="WasModified">Whether the response was modified by reflection.</param>
/// <param name="TokensUsed">Tokens used for the reflection call.</param>
public sealed record ReflectionResult(
    string Content,
    bool WasModified,
    int TokensUsed);
