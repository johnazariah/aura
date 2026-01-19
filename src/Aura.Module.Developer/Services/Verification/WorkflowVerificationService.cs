// <copyright file="WorkflowVerificationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Service for running verification checks on workflow changes.
/// </summary>
public sealed class WorkflowVerificationService : IWorkflowVerificationService
{
    private readonly IProjectVerificationDetector _detector;
    private readonly ILogger<WorkflowVerificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowVerificationService"/> class.
    /// </summary>
    public WorkflowVerificationService(
        IProjectVerificationDetector detector,
        ILogger<WorkflowVerificationService> logger)
    {
        _detector = detector;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<VerificationResult> VerifyAsync(
        string workingDirectory,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Detect projects and their verification requirements
        var projects = await _detector.DetectProjectsAsync(workingDirectory, ct);

        if (projects.Count == 0)
        {
            _logger.LogInformation("No projects detected for verification in {Path}", workingDirectory);
            return new VerificationResult
            {
                Success = true,
                Projects = projects,
                StepResults = [],
                DurationMs = stopwatch.ElapsedMilliseconds,
            };
        }

        _logger.LogInformation(
            "Starting verification for {ProjectCount} projects: {Projects}",
            projects.Count,
            string.Join(", ", projects.Select(p => p.ProjectName)));

        var stepResults = new List<VerificationStepResult>();

        // Run verification steps for each project
        foreach (var project in projects)
        {
            foreach (var step in project.VerificationSteps)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var result = await RunStepAsync(step, ct);
                stepResults.Add(result);

                // If a required step fails, we still continue to get full picture
                if (!result.Success && result.Required)
                {
                    _logger.LogWarning(
                        "Required verification step failed: {StepType} for {Project} - {Error}",
                        step.StepType,
                        project.ProjectName,
                        result.ErrorMessage);
                }
                else if (!result.Success)
                {
                    _logger.LogInformation(
                        "Optional verification step failed: {StepType} for {Project} - {Error}",
                        step.StepType,
                        project.ProjectName,
                        result.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation(
                        "Verification step passed: {StepType} for {Project} ({Duration}ms)",
                        step.StepType,
                        project.ProjectName,
                        result.DurationMs);
                }
            }
        }

        stopwatch.Stop();

        // Success if no required steps failed
        var success = stepResults.All(r => r.Success || !r.Required);

        _logger.LogInformation(
            "Verification completed: {Success}, {PassedCount}/{TotalCount} steps passed in {Duration}ms",
            success ? "PASSED" : "FAILED",
            stepResults.Count(r => r.Success),
            stepResults.Count,
            stopwatch.ElapsedMilliseconds);

        return new VerificationResult
        {
            Success = success,
            Projects = projects,
            StepResults = stepResults,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };
    }

    /// <inheritdoc/>
    public async Task<VerificationStepResult> RunStepAsync(
        VerificationStep step,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug(
                "Running verification step: {Command} {Args} in {WorkingDir}",
                step.Command,
                string.Join(" ", step.Arguments),
                step.WorkingDirectory);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(step.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var psi = new ProcessStartInfo
            {
                FileName = step.Command,
                WorkingDirectory = step.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in step.Arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };

            var outputTask = new TaskCompletionSource<string>();
            var errorTask = new TaskCompletionSource<string>();
            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout - try to kill the process
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore errors killing the process
                }

                stopwatch.Stop();
                return new VerificationStepResult
                {
                    Step = step,
                    Success = false,
                    Required = step.Required,
                    ExitCode = -1,
                    StandardOutput = TruncateOutput(output.ToString()),
                    StandardError = TruncateOutput(error.ToString()),
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    TimedOut = true,
                };
            }

            stopwatch.Stop();
            return new VerificationStepResult
            {
                Step = step,
                Success = process.ExitCode == 0,
                Required = step.Required,
                ExitCode = process.ExitCode,
                StandardOutput = TruncateOutput(output.ToString()),
                StandardError = TruncateOutput(error.ToString()),
                DurationMs = stopwatch.ElapsedMilliseconds,
                TimedOut = false,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error running verification step: {Step}", step.Description);
            return new VerificationStepResult
            {
                Step = step,
                Success = false,
                Required = step.Required,
                ExitCode = -1,
                StandardError = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                TimedOut = false,
            };
        }
    }

    private static string TruncateOutput(string output, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        if (output.Length <= maxLength)
        {
            return output;
        }

        return output[..maxLength] + "\n... (truncated)";
    }
}
