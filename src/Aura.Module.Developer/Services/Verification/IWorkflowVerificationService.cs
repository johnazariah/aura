// <copyright file="IWorkflowVerificationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Service for running verification checks on workflow changes.
/// </summary>
public interface IWorkflowVerificationService
{
    /// <summary>
    /// Runs verification checks for a workflow's working directory.
    /// </summary>
    /// <param name="workingDirectory">The directory to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<VerificationResult> VerifyAsync(
        string workingDirectory,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a single verification step.
    /// </summary>
    /// <param name="step">The verification step to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The step result.</returns>
    Task<VerificationStepResult> RunStepAsync(
        VerificationStep step,
        CancellationToken ct = default);
}

/// <summary>
/// Result of running all verification steps.
/// </summary>
public sealed record VerificationResult
{
    /// <summary>Gets whether all required verification steps passed.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the list of projects that were verified.</summary>
    public required IReadOnlyList<DetectedProject> Projects { get; init; }

    /// <summary>Gets the results of all verification steps.</summary>
    public required IReadOnlyList<VerificationStepResult> StepResults { get; init; }

    /// <summary>Gets the total duration of verification in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Gets a summary of the verification result.</summary>
    public string Summary => StepResults.Count == 0
        ? "No verification steps detected"
        : $"{StepResults.Count(r => r.Success)}/{StepResults.Count} steps passed" +
          (Success ? "" : $" ({StepResults.Count(r => !r.Success && r.Required)} required failures)");
}

/// <summary>
/// Result of running a single verification step.
/// </summary>
public sealed record VerificationStepResult
{
    /// <summary>Gets the step that was run.</summary>
    public required VerificationStep Step { get; init; }

    /// <summary>Gets whether the step succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets whether this step was required.</summary>
    public required bool Required { get; init; }

    /// <summary>Gets the exit code of the command.</summary>
    public int ExitCode { get; init; }

    /// <summary>Gets the standard output.</summary>
    public string? StandardOutput { get; init; }

    /// <summary>Gets the standard error.</summary>
    public string? StandardError { get; init; }

    /// <summary>Gets the duration in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Gets whether the step timed out.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Gets the error message if the step failed.</summary>
    public string? ErrorMessage => TimedOut
        ? $"Step timed out after {Step.TimeoutSeconds}s"
        : !Success && !string.IsNullOrWhiteSpace(StandardError)
            ? StandardError.Trim()
            : !Success
                ? $"Exited with code {ExitCode}"
                : null;
}
