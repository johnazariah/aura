// <copyright file="IToolConfirmationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

/// <summary>
/// Service for requesting user approval for tool executions.
/// </summary>
public interface IToolConfirmationService
{
    /// <summary>
    /// Request user approval for a tool execution.
    /// </summary>
    /// <param name="toolId">The tool being executed.</param>
    /// <param name="toolDescription">Human-readable description of the tool.</param>
    /// <param name="argumentsSummary">Summary of arguments being passed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if approved, false if rejected.</returns>
    Task<bool> RequestApprovalAsync(
        string toolId,
        string toolDescription,
        string argumentsSummary,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for tool confirmations.
/// </summary>
public sealed class ToolConfirmationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Aura:Agents:ToolExecution";

    /// <summary>Whether tool execution is enabled. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum iterations for the tool execution loop.</summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>Tools that are auto-approved (no user confirmation needed).</summary>
    public List<string> AutoApproveTools { get; set; } = ["file.read", "graph.query"];

    /// <summary>Tools that always require approval.</summary>
    public List<string> RequireApprovalTools { get; set; } = ["file.write", "file.delete", "git.commit"];
}

/// <summary>
/// Auto-approve tool confirmation service for non-interactive scenarios.
/// </summary>
public sealed class AutoApproveToolConfirmationService : IToolConfirmationService
{
    private readonly ToolConfirmationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoApproveToolConfirmationService"/> class.
    /// </summary>
    public AutoApproveToolConfirmationService(ToolConfirmationOptions? options = null)
    {
        _options = options ?? new ToolConfirmationOptions();
    }

    /// <inheritdoc/>
    public Task<bool> RequestApprovalAsync(
        string toolId,
        string toolDescription,
        string argumentsSummary,
        CancellationToken cancellationToken = default)
    {
        // Check if tool is in auto-approve list
        if (_options.AutoApproveTools.Contains(toolId, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(true);
        }

        // Check if tool requires approval
        if (_options.RequireApprovalTools.Contains(toolId, StringComparer.OrdinalIgnoreCase))
        {
            // In auto-approve mode, reject tools that require confirmation
            // In a real implementation, this would trigger UI for approval
            return Task.FromResult(false);
        }

        // Default: auto-approve
        return Task.FromResult(true);
    }
}
