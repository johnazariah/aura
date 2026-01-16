// <copyright file="IGuardianExecutor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Guardians;

/// <summary>
/// Executes guardian checks and creates workflows from violations.
/// </summary>
public interface IGuardianExecutor
{
    /// <summary>
    /// Executes a guardian check and creates workflows for any violations found.
    /// </summary>
    /// <param name="guardian">The guardian definition to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the guardian execution.</returns>
    Task<GuardianExecutionResult> ExecuteAsync(GuardianDefinition guardian, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a guardian check with custom context (e.g., for event triggers).
    /// </summary>
    /// <param name="guardian">The guardian definition to execute.</param>
    /// <param name="context">Additional context for the execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the guardian execution.</returns>
    Task<GuardianExecutionResult> ExecuteAsync(
        GuardianDefinition guardian,
        GuardianExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for guardian execution.
/// </summary>
public record GuardianExecutionContext
{
    /// <summary>
    /// Gets or sets the workspace path to check.
    /// </summary>
    public string? WorkspacePath { get; init; }

    /// <summary>
    /// Gets or sets the trigger type that initiated this execution.
    /// </summary>
    public GuardianTriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets or sets the specific files that triggered the guardian (for file-change triggers).
    /// </summary>
    public IReadOnlyList<string>? ChangedFiles { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for the execution.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of a guardian execution.
/// </summary>
public record GuardianExecutionResult
{
    /// <summary>
    /// Gets or sets the guardian ID that was executed.
    /// </summary>
    public required string GuardianId { get; init; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public required GuardianExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the check result from the guardian.
    /// </summary>
    public GuardianCheckResult? CheckResult { get; init; }

    /// <summary>
    /// Gets or sets the IDs of workflows created from violations.
    /// </summary>
    public IReadOnlyList<Guid> CreatedWorkflowIds { get; init; } = [];

    /// <summary>
    /// Gets or sets any error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or sets when the execution completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// Status of a guardian execution.
/// </summary>
public enum GuardianExecutionStatus
{
    /// <summary>
    /// Guardian executed successfully with no violations found.
    /// </summary>
    Clean,

    /// <summary>
    /// Guardian executed successfully and found violations (workflows created).
    /// </summary>
    ViolationsFound,

    /// <summary>
    /// Guardian execution failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Guardian was skipped (e.g., no matching files, disabled, etc.).
    /// </summary>
    Skipped,
}
