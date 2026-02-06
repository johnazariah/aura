// <copyright file="StoryVerificationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Service for running verification checks on workflow changes.
/// </summary>
public sealed class StoryVerificationService : IStoryVerificationService
{
    private readonly IProjectVerificationDetector _detector;
    private readonly ILogger<StoryVerificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoryVerificationService"/> class.
    /// </summary>
    public StoryVerificationService(
        IProjectVerificationDetector detector,
        ILogger<StoryVerificationService> logger)
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

    /// <inheritdoc/>
    public async Task<VerificationResult> VerifyAsync(
        string workingDirectory,
        VerifyOptions options,
        CancellationToken ct = default)
    {
        // For now, options don't change behavior - all verification steps run
        // Future: filter steps based on options.RunBuild, RunTests, RunLint
        // Future: if options.IncludeCodeReview, run agent-based review
        return await VerifyAsync(workingDirectory, ct);
    }

    /// <inheritdoc/>
    public VerificationChecklist ToChecklist(VerificationResult result)
    {
        var buildResults = result.StepResults
            .Where(r => r.Step.StepType == "build")
            .ToList();

        var testResults = result.StepResults
            .Where(r => r.Step.StepType == "test")
            .ToList();

        var lintResults = result.StepResults
            .Where(r => r.Step.StepType == "lint" || r.Step.StepType == "format")
            .ToList();

        // Determine decision based on results
        var decision = result.Success
            ? ReviewDecision.Approved
            : result.StepResults.Any(r => !r.Success && r.Required)
                ? ReviewDecision.ChangesRequested
                : ReviewDecision.Approved;

        // Build findings from failed steps
        var mustFix = result.StepResults
            .Where(r => !r.Success && r.Required)
            .Select(r => $"{r.Step.StepType}: {r.ErrorMessage}")
            .ToList();

        var shouldFix = result.StepResults
            .Where(r => !r.Success && !r.Required)
            .Select(r => $"{r.Step.StepType}: {r.ErrorMessage}")
            .ToList();

        return new VerificationChecklist
        {
            Summary = result.Summary,
            Decision = decision,
            Functional = null, // Requires code review agent
            CodeQuality = CreateCodeQualityChecklist(lintResults),
            Testing = CreateTestingChecklist(testResults),
            Architecture = null, // Requires code review agent
            Findings = new VerificationFindings
            {
                MustFix = mustFix.Count > 0 ? mustFix : null,
                ShouldFix = shouldFix.Count > 0 ? shouldFix : null,
                Suggestions = null, // Requires code review agent
            },
            Build = buildResults.Count > 0 ? CreateBuildResult(buildResults) : null,
            Tests = testResults.Count > 0 ? CreateTestResult(testResults) : null,
            Lint = lintResults.Count > 0 ? CreateLintResult(lintResults) : null,
        };
    }

    private static ChecklistCategory? CreateCodeQualityChecklist(List<VerificationStepResult> lintResults)
    {
        if (lintResults.Count == 0)
        {
            return null;
        }

        return new ChecklistCategory
        {
            Items = new Dictionary<string, ChecklistItem>
            {
                ["no_lint_errors"] = new ChecklistItem
                {
                    Passed = lintResults.All(r => r.Success),
                    Notes = lintResults.Any(r => !r.Success)
                        ? $"{lintResults.Count(r => !r.Success)} lint check(s) failed"
                        : null,
                },
            },
            Notes = null,
        };
    }

    private static ChecklistCategory? CreateTestingChecklist(List<VerificationStepResult> testResults)
    {
        if (testResults.Count == 0)
        {
            return null;
        }

        var allPass = testResults.All(r => r.Success);

        return new ChecklistCategory
        {
            Items = new Dictionary<string, ChecklistItem>
            {
                ["tests_pass"] = new ChecklistItem
                {
                    Passed = allPass,
                    Notes = !allPass
                        ? $"{testResults.Count(r => !r.Success)} test run(s) failed"
                        : null,
                },
            },
            Notes = null,
        };
    }

    private static BuildResult CreateBuildResult(List<VerificationStepResult> buildResults)
    {
        var allPass = buildResults.All(r => r.Success);
        var output = string.Join("\n---\n", buildResults
            .Where(r => !string.IsNullOrEmpty(r.StandardOutput) || !string.IsNullOrEmpty(r.StandardError))
            .Select(r => $"[{r.Step.Description}]\n{r.StandardOutput}{r.StandardError}"));

        // Parse warning/error counts from MSBuild output (simplified)
        var errorCount = 0;
        var warningCount = 0;
        foreach (var result in buildResults)
        {
            var text = (result.StandardOutput ?? string.Empty) + (result.StandardError ?? string.Empty);
            errorCount += System.Text.RegularExpressions.Regex.Matches(text, @": error ").Count;
            warningCount += System.Text.RegularExpressions.Regex.Matches(text, @": warning ").Count;
        }

        return new BuildResult
        {
            Passed = allPass,
            Output = string.IsNullOrEmpty(output) ? null : TruncateOutput(output, 2000),
            Errors = errorCount,
            Warnings = warningCount,
        };
    }

    private static TestResult CreateTestResult(List<VerificationStepResult> testResults)
    {
        var allPass = testResults.All(r => r.Success);
        var output = string.Join("\n---\n", testResults
            .Where(r => !string.IsNullOrEmpty(r.StandardOutput) || !string.IsNullOrEmpty(r.StandardError))
            .Select(r => $"[{r.Step.Description}]\n{r.StandardOutput}{r.StandardError}"));

        // Parse test counts from dotnet test output (simplified)
        var total = 0;
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var result in testResults)
        {
            var text = (result.StandardOutput ?? string.Empty) + (result.StandardError ?? string.Empty);

            // Look for patterns like "Passed: 10" "Failed: 2" etc
            var passedMatch = System.Text.RegularExpressions.Regex.Match(text, @"Passed:\s*(\d+)");
            var failedMatch = System.Text.RegularExpressions.Regex.Match(text, @"Failed:\s*(\d+)");
            var skippedMatch = System.Text.RegularExpressions.Regex.Match(text, @"Skipped:\s*(\d+)");

            if (passedMatch.Success)
            {
                passed += int.Parse(passedMatch.Groups[1].Value);
            }

            if (failedMatch.Success)
            {
                failed += int.Parse(failedMatch.Groups[1].Value);
            }

            if (skippedMatch.Success)
            {
                skipped += int.Parse(skippedMatch.Groups[1].Value);
            }
        }

        total = passed + failed + skipped;

        return new TestResult
        {
            Passed = allPass,
            Total = total,
            PassedCount = passed,
            Failed = failed,
            Skipped = skipped,
            Output = string.IsNullOrEmpty(output) ? null : TruncateOutput(output, 2000),
        };
    }

    private static LintResult CreateLintResult(List<VerificationStepResult> lintResults)
    {
        var allPass = lintResults.All(r => r.Success);
        var output = string.Join("\n---\n", lintResults
            .Where(r => !string.IsNullOrEmpty(r.StandardOutput) || !string.IsNullOrEmpty(r.StandardError))
            .Select(r => $"[{r.Step.Description}]\n{r.StandardOutput}{r.StandardError}"));

        // Parse error/warning counts (simplified)
        var errorCount = 0;
        var warningCount = 0;
        foreach (var result in lintResults)
        {
            var text = (result.StandardOutput ?? string.Empty) + (result.StandardError ?? string.Empty);
            errorCount += System.Text.RegularExpressions.Regex.Matches(text, @"error", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            warningCount += System.Text.RegularExpressions.Regex.Matches(text, @"warning", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        }

        return new LintResult
        {
            Passed = allPass,
            Errors = errorCount,
            Warnings = warningCount,
            Output = string.IsNullOrEmpty(output) ? null : TruncateOutput(output, 2000),
        };
    }
}
