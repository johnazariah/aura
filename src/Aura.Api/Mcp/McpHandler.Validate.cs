using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    /// <summary>
    /// aura_validate - Check code correctness.
    /// Routes to: compilation, tests.
    /// Auto-detects language from solutionPath (C#) vs projectPath (TypeScript/Python).
    /// </summary>
    private async Task<object> ValidateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString() ?? throw new ArgumentException("operation is required");
        var language = DetectLanguageFromArgs(args);

        return (operation, language) switch
        {
            ("compilation", "typescript") => new { error = "TypeScript compilation validation is not yet implemented. Use run_in_terminal with 'npx tsc --noEmit' for TypeScript type checking." },
            ("tests", "typescript") => new { error = "TypeScript test execution is not yet implemented. Use run_in_terminal with 'npx jest' or 'npx vitest' for TypeScript tests." },
            ("compilation", _) => await ValidateCompilationAsync(args, ct),
            ("tests", _) => await RunTestsAsync(args, ct),
            _ => throw new ArgumentException($"Unknown validate operation: {operation}")
        };
    }

    private async Task<object> ValidateCompilationAsync(JsonElement? args, CancellationToken ct)
    {
        // Get solutionPath - required
        string? solutionPath = null;
        if (args.HasValue && args.Value.TryGetProperty("solutionPath", out var solEl))
        {
            solutionPath = solEl.GetString();
        }

        // Get projectName - optional (if omitted, validate all projects)
        string? projectName = null;
        if (args.HasValue && args.Value.TryGetProperty("projectName", out var projEl))
        {
            projectName = projEl.GetString();
        }

        var includeWarnings = false;
        if (args.HasValue && args.Value.TryGetProperty("includeWarnings", out var warnEl))
        {
            includeWarnings = warnEl.GetBoolean();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new
            {
                error = $"Solution file not found: {solutionPath}"
            };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            // If projectName is specified, validate just that project
            if (!string.IsNullOrEmpty(projectName))
            {
                var project = solution.Projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
                if (project is null)
                {
                    var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                    return new
                    {
                        error = $"Project '{projectName}' not found. Available: {available}"
                    };
                }

                return await ValidateProjectAsync(project, includeWarnings, ct);
            }

            // No project specified - validate all projects in solution
            var results = new List<object>();
            var totalErrors = 0;
            var totalWarnings = 0;
            foreach (var project in solution.Projects.Where(p => !p.Name.Contains(".Tests")))
            {
                var result = await ValidateProjectAsync(project, includeWarnings, ct);
                results.Add(new { project = project.Name, result });
                // Extract counts from dynamic result
                if (result is { } r)
                {
                    var props = r.GetType().GetProperties();
                    var errorProp = props.FirstOrDefault(p => p.Name == "errorCount");
                    var warnProp = props.FirstOrDefault(p => p.Name == "warningCount");
                    if (errorProp?.GetValue(r) is int errors)
                        totalErrors += errors;
                    if (warnProp?.GetValue(r) is int warnings)
                        totalWarnings += warnings;
                }
            }

            return new
            {
                solutionPath,
                success = totalErrors == 0,
                totalErrors,
                totalWarnings,
                projectCount = results.Count,
                projects = results,
                summary = totalErrors == 0 ? $"Solution compiles successfully ({results.Count} projects)" : $"Solution has {totalErrors} error(s) across {results.Count} projects"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate compilation for {SolutionPath}", solutionPath);
            return new
            {
                error = $"Failed to validate compilation: {ex.Message}"
            };
        }
    }

    private async Task<object> ValidateProjectAsync(Project project, bool includeWarnings, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null)
        {
            return new
            {
                error = "Failed to get compilation"
            };
        }

        var diagnostics = compilation.GetDiagnostics(ct).Where(d => d.Severity == DiagnosticSeverity.Error || (includeWarnings && d.Severity == DiagnosticSeverity.Warning)).Take(50).Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new
            {
                id = d.Id,
                severity = d.Severity.ToString(),
                message = d.GetMessage(),
                filePath = lineSpan.Path,
                line = lineSpan.StartLinePosition.Line + 1,
                column = lineSpan.StartLinePosition.Character + 1
            };
        }).ToList();
        var errorCount = diagnostics.Count(d => d.severity == "Error");
        var warningCount = diagnostics.Count(d => d.severity == "Warning");
        return new
        {
            projectName = project.Name,
            success = errorCount == 0,
            errorCount,
            warningCount,
            diagnostics,
            summary = errorCount == 0 ? $"Project {project.Name} compiles successfully" : $"Project {project.Name} has {errorCount} error(s)"
        };
    }

    private async Task<object> RunTestsAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        string? filter = null;
        var timeoutSeconds = 120;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("filter", out var filterEl))
                filter = filterEl.GetString();
            if (args.Value.TryGetProperty("timeoutSeconds", out var timeoutEl))
                timeoutSeconds = timeoutEl.GetInt32();
        }

        if (string.IsNullOrEmpty(projectPath))
        {
            return new
            {
                error = "projectPath is required"
            };
        }

        try
        {
            var arguments = $"test \"{projectPath}\" --no-restore --verbosity normal";
            if (!string.IsNullOrEmpty(filter))
            {
                arguments += $" --filter \"{filter}\"";
            }

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();
            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000), ct);
            if (!completed)
            {
                process.Kill();
                return new
                {
                    error = $"Test run timed out after {timeoutSeconds} seconds"
                };
            }

            var outputText = output.ToString();
            var success = process.ExitCode == 0;
            // Parse test results from output - handle multiple output formats
            // Format 1: "Passed: 10" (normal)
            // Format 2: "Passed:    10, Failed:     0" (summary line)
            // Format 3: "Total tests: 10"
            var passedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Passed[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var failedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Failed[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var skippedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Skipped[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var totalMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Total[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Fallback: look for "Total tests: X" format
            if (!totalMatch.Success)
            {
                totalMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Total tests:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Calculate total from passed + failed + skipped if total not found
            var passed = passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value) : 0;
            var failed = failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value) : 0;
            var skipped = skippedMatch.Success ? int.Parse(skippedMatch.Groups[1].Value) : 0;
            var total = totalMatch.Success ? int.Parse(totalMatch.Groups[1].Value) : (passed + failed + skipped);
            return new
            {
                projectPath,
                success,
                exitCode = process.ExitCode,
                passed,
                failed,
                skipped,
                total,
                output = outputText.Length > 10000 ? outputText[..10000] + "\n... (truncated)" : outputText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run tests for {Project}", projectPath);
            return new
            {
                error = $"Failed to run tests: {ex.Message}"
            };
        }
    }
}
