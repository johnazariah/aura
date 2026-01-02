// <copyright file="RunTestsTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the run_tests tool.
/// </summary>
public record RunTestsInput
{
    /// <summary>Project path or name to test</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Optional filter for specific tests (dotnet test --filter)</summary>
    public string? Filter { get; init; }

    /// <summary>Timeout in seconds (default: 120)</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Build before testing (default: true)</summary>
    public bool Build { get; init; } = true;
}

/// <summary>
/// Information about a test result.
/// </summary>
public record TestResult
{
    /// <summary>Test name</summary>
    public required string Name { get; init; }

    /// <summary>Test outcome (Passed, Failed, Skipped)</summary>
    public required string Outcome { get; init; }

    /// <summary>Duration in milliseconds</summary>
    public double DurationMs { get; init; }

    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Output from the run_tests tool.
/// </summary>
public record RunTestsOutput
{
    /// <summary>Project that was tested</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Whether all tests passed</summary>
    public bool Success { get; init; }

    /// <summary>Total tests run</summary>
    public int TotalTests { get; init; }

    /// <summary>Passed tests</summary>
    public int PassedTests { get; init; }

    /// <summary>Failed tests</summary>
    public int FailedTests { get; init; }

    /// <summary>Skipped tests</summary>
    public int SkippedTests { get; init; }

    /// <summary>Total duration</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Individual test results (especially failures)</summary>
    public IReadOnlyList<TestResult> Results { get; init; } = [];

    /// <summary>Raw output from dotnet test</summary>
    public string? RawOutput { get; init; }

    /// <summary>Summary message</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// Runs unit tests for a project using dotnet test.
/// Use to verify that code changes don't break existing tests.
/// </summary>
public partial class RunTestsTool(ILogger<RunTestsTool> logger) : TypedToolBase<RunTestsInput, RunTestsOutput>
{
    private readonly ILogger<RunTestsTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "dotnet.run_tests";

    /// <inheritdoc/>
    public override string Name => "Run Tests";

    /// <inheritdoc/>
    public override string Description =>
        "Runs unit tests for a project using dotnet test. Returns test results " +
        "including pass/fail counts and error messages for failures. " +
        "Use after writing tests or modifying code to verify correctness.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["dotnet", "testing"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<RunTestsOutput>> ExecuteAsync(
        RunTestsInput input,
        CancellationToken ct = default)
    {
        var projectPath = Path.GetFullPath(input.ProjectPath);
        _logger.LogInformation("Running tests for: {ProjectPath}", projectPath);

        // Build the dotnet test command
        var args = new StringBuilder();
        args.Append($"test \"{projectPath}\" --verbosity normal");

        if (!input.Build)
        {
            args.Append(" --no-build");
        }

        if (!string.IsNullOrEmpty(input.Filter))
        {
            args.Append($" --filter \"{input.Filter}\"");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var stopwatch = Stopwatch.StartNew();
            using var process = new Process { StartInfo = startInfo };

            var output = new StringBuilder();
            var errorOutput = new StringBuilder();

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
                    errorOutput.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(input.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                process.Kill();
                return ToolResult<RunTestsOutput>.Fail(
                    $"Test run timed out after {input.TimeoutSeconds} seconds");
            }

            stopwatch.Stop();

            var rawOutput = output.ToString();
            var parseResult = ParseTestOutput(rawOutput);

            var testOutput = new RunTestsOutput
            {
                ProjectPath = projectPath,
                Success = parseResult.Failed == 0,
                TotalTests = parseResult.Total,
                PassedTests = parseResult.Passed,
                FailedTests = parseResult.Failed,
                SkippedTests = parseResult.Skipped,
                Duration = stopwatch.Elapsed,
                Results = parseResult.Results,
                RawOutput = rawOutput.Length > 10000 ? rawOutput[..10000] + "\n... (truncated)" : rawOutput,
                Summary = parseResult.Failed == 0
                    ? $"All {parseResult.Passed} tests passed in {stopwatch.Elapsed.TotalSeconds:F1}s"
                    : $"{parseResult.Failed} of {parseResult.Total} tests failed",
            };

            _logger.LogInformation("Test run complete: {Summary}", testOutput.Summary);
            return ToolResult<RunTestsOutput>.Ok(testOutput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run tests for {ProjectPath}", projectPath);
            return ToolResult<RunTestsOutput>.Fail($"Failed to run tests: {ex.Message}");
        }
    }

    private static (int Total, int Passed, int Failed, int Skipped, List<TestResult> Results) ParseTestOutput(string output)
    {
        var results = new List<TestResult>();
        int total = 0, passed = 0, failed = 0, skipped = 0;

        // Parse summary line like "Passed!  - Failed:     0, Passed:   188, Skipped:     0, Total:   188"
        var summaryMatch = SummaryRegex().Match(output);
        if (summaryMatch.Success)
        {
            _ = int.TryParse(summaryMatch.Groups["failed"].Value, out failed);
            _ = int.TryParse(summaryMatch.Groups["passed"].Value, out passed);
            _ = int.TryParse(summaryMatch.Groups["skipped"].Value, out skipped);
            _ = int.TryParse(summaryMatch.Groups["total"].Value, out total);
        }

        // Parse individual failed tests
        var failedMatches = FailedTestRegex().Matches(output);
        foreach (Match match in failedMatches)
        {
            results.Add(new TestResult
            {
                Name = match.Groups["name"].Value,
                Outcome = "Failed",
                ErrorMessage = match.Groups["error"].Success ? match.Groups["error"].Value : null,
            });
        }

        return (total, passed, failed, skipped, results);
    }

    [GeneratedRegex(@"Failed:\s*(?<failed>\d+).*?Passed:\s*(?<passed>\d+).*?Skipped:\s*(?<skipped>\d+).*?Total:\s*(?<total>\d+)", RegexOptions.Singleline)]
    private static partial Regex SummaryRegex();

    [GeneratedRegex(@"Failed\s+(?<name>[\w.]+)\s*(?:\[.*?\])?\s*(?:Error Message:\s*(?<error>.+?)(?=Failed|\z))?", RegexOptions.Singleline)]
    private static partial Regex FailedTestRegex();
}
