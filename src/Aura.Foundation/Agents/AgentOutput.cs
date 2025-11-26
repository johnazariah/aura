// <copyright file="AgentOutput.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Output from an agent execution.
/// </summary>
/// <param name="Content">The text output from the agent.</param>
/// <param name="TokensUsed">Number of tokens consumed.</param>
/// <param name="ToolCalls">Any tool calls made during execution.</param>
/// <param name="Artifacts">Named artifacts produced (code, files, etc.).</param>
public sealed record AgentOutput(
    string Content,
    int TokensUsed = 0,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    IReadOnlyDictionary<string, string>? Artifacts = null)
{
    /// <summary>
    /// Gets tool calls made during execution.
    /// </summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; } = ToolCalls ?? [];

    /// <summary>
    /// Gets artifacts produced during execution.
    /// </summary>
    public IReadOnlyDictionary<string, string> Artifacts { get; } = Artifacts ?? new Dictionary<string, string>();

    /// <summary>
    /// Creates a simple text output.
    /// </summary>
    /// <param name="content">The output content.</param>
    /// <returns>A new output instance.</returns>
    public static AgentOutput FromText(string content) => new(content);

    /// <summary>
    /// Creates output with artifacts.
    /// </summary>
    /// <param name="content">The output content.</param>
    /// <param name="artifacts">The artifacts dictionary.</param>
    /// <returns>A new output instance.</returns>
    public static AgentOutput WithArtifacts(string content, IReadOnlyDictionary<string, string> artifacts) =>
        new(content, Artifacts: artifacts);
}

/// <summary>
/// A tool call made by an agent.
/// </summary>
/// <param name="ToolName">The name of the tool called.</param>
/// <param name="Arguments">Arguments passed to the tool (JSON).</param>
/// <param name="Result">Result returned by the tool.</param>
public sealed record ToolCall(string ToolName, string Arguments, string? Result = null);
