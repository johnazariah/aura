using System.IO.Abstractions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anvil.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Anvil.Cli.Services;

/// <summary>
/// Validates story results against scenario expectations.
/// </summary>
public sealed class ExpectationValidator(
    IFileSystem fileSystem,
    IIndexEffectivenessAnalyzer indexAnalyzer,
    ILogger<ExpectationValidator> logger) : IExpectationValidator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ExpectationResult>> ValidateAsync(
        Scenario scenario,
        StoryResponse story,
        CancellationToken ct = default)
    {
        var results = new List<ExpectationResult>();

        foreach (var expectation in scenario.Expectations)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ValidateExpectationAsync(expectation, story, ct);
            results.Add(result);

            if (result.Passed)
            {
                logger.LogDebug("✓ {Type}: {Description}", expectation.Type, expectation.Description);
            }
            else
            {
                logger.LogWarning("✗ {Type}: {Description} - {Message}",
                    expectation.Type, expectation.Description, result.Message);
            }
        }

        return results;
    }

    private async Task<ExpectationResult> ValidateExpectationAsync(
        Expectation expectation,
        StoryResponse story,
        CancellationToken ct)
    {
        return expectation.Type.ToLowerInvariant() switch
        {
            "compiles" => ValidateCompiles(expectation, story),
            "tests_pass" => ValidateTestsPass(expectation, story),
            "file_exists" => ValidateFileExists(expectation, story),
            "file_contains" => await ValidateFileContainsAsync(expectation, story, ct),
            "file_not_contains" => await ValidateFileNotContainsAsync(expectation, story, ct),
            "index_usage" => ValidateIndexUsage(expectation, story),
            _ => new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = $"Unknown expectation type: {expectation.Type}"
            }
        };
    }

    private ExpectationResult ValidateCompiles(Expectation expectation, StoryResponse story)
    {
        // Try to actually compile the code in the worktree
        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            // Fall back to story status check
            var passed = story.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                      || story.Status.Equals("ReadyToComplete", StringComparison.OrdinalIgnoreCase);
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = passed,
                Message = passed
                    ? $"Story status: {story.Status}"
                    : $"Story failed: {story.Error ?? story.Status}"
            };
        }

        // Find solution or project file
        var slnFiles = fileSystem.Directory.GetFiles(story.WorktreePath, "*.sln", SearchOption.TopDirectoryOnly);
        var csprojFiles = fileSystem.Directory.GetFiles(story.WorktreePath, "*.csproj", SearchOption.AllDirectories);

        var projectPath = slnFiles.FirstOrDefault() ?? csprojFiles.FirstOrDefault();
        if (projectPath == null)
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "No .sln or .csproj file found"
            };
        }

        // Clean obj directory to avoid stale project.assets.json from different user context
        // (The worktree may have been built by AuraService with different NuGet paths)
        try
        {
            var objPath = Path.Combine(story.WorktreePath, "obj");
            if (fileSystem.Directory.Exists(objPath))
            {
                fileSystem.Directory.Delete(objPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean obj directory, continuing anyway");
        }

        // Run dotnet restore to ensure packages are available
        try
        {
            var restoreInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{projectPath}\" -v q",
                WorkingDirectory = story.WorktreePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var restoreProcess = System.Diagnostics.Process.Start(restoreInfo);
            if (restoreProcess != null)
            {
                restoreProcess.WaitForExit(120000); // 2 minute timeout for restore
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "dotnet restore failed, continuing with build anyway");
        }

        // Run dotnet build
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" -v q",
                WorkingDirectory = story.WorktreePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return new ExpectationResult
                {
                    Expectation = expectation,
                    Passed = false,
                    Message = "Failed to start dotnet build"
                };
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(60000); // 60 second timeout

            if (process.ExitCode == 0)
            {
                return new ExpectationResult
                {
                    Expectation = expectation,
                    Passed = true,
                    Message = "Code compiles successfully"
                };
            }
            else
            {
                // Extract just the error summary
                var errorLines = (output + "\n" + error)
                    .Split('\n')
                    .Where(l => l.Contains("error CS") || l.Contains("Build FAILED"))
                    .Take(3)
                    .ToList();
                var errorSummary = errorLines.Count > 0
                    ? string.Join("; ", errorLines)
                    : "Build failed";

                return new ExpectationResult
                {
                    Expectation = expectation,
                    Passed = false,
                    Message = errorSummary
                };
            }
        }
        catch (Exception ex)
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = $"Build error: {ex.Message}"
            };
        }
    }

    private static ExpectationResult ValidateTestsPass(Expectation expectation, StoryResponse story)
    {
        // Check if any steps failed
        var failedSteps = story.Steps?.Where(s =>
            s.Status != null && s.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)).ToList();

        if (failedSteps is { Count: > 0 })
        {
            var failedStep = failedSteps[0];
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = $"Step '{failedStep.Name}' failed: {failedStep.Error ?? "Unknown error"}"
            };
        }

        return new ExpectationResult
        {
            Expectation = expectation,
            Passed = true,
            Message = "All steps completed successfully"
        };
    }

    private ExpectationResult ValidateFileExists(Expectation expectation, StoryResponse story)
    {
        if (string.IsNullOrEmpty(expectation.Path))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "file_exists expectation requires a 'path'"
            };
        }

        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "Story does not have a worktree path"
            };
        }

        var fullPath = fileSystem.Path.Combine(story.WorktreePath, expectation.Path);
        var exists = fileSystem.File.Exists(fullPath);

        return new ExpectationResult
        {
            Expectation = expectation,
            Passed = exists,
            Message = exists
                ? $"File found: {expectation.Path}"
                : $"File not found: {expectation.Path}"
        };
    }

    private async Task<ExpectationResult> ValidateFileContainsAsync(
        Expectation expectation,
        StoryResponse story,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(expectation.Path))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "file_contains expectation requires a 'path'"
            };
        }

        if (string.IsNullOrEmpty(expectation.Pattern))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "file_contains expectation requires a 'pattern'"
            };
        }

        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "Story does not have a worktree path"
            };
        }

        // Support glob patterns (e.g., **/*.cs, **/Calculator*.cs)
        string? targetFile = null;
        if (expectation.Path.Contains('*'))
        {
            // Use glob matching
            var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
            matcher.AddInclude(expectation.Path);
            var result = matcher.Execute(
                new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(
                    new System.IO.DirectoryInfo(story.WorktreePath)));
            var firstMatch = result.Files.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstMatch.Path))
            {
                targetFile = fileSystem.Path.Combine(story.WorktreePath, firstMatch.Path);
            }
        }
        else
        {
            targetFile = fileSystem.Path.Combine(story.WorktreePath, expectation.Path);
        }

        if (targetFile == null || !fileSystem.File.Exists(targetFile))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = $"File not found: {expectation.Path}"
            };
        }

        var content = await fileSystem.File.ReadAllTextAsync(targetFile, ct);
        var regex = new Regex(expectation.Pattern, RegexOptions.Multiline);
        var matches = regex.IsMatch(content);

        return new ExpectationResult
        {
            Expectation = expectation,
            Passed = matches,
            Message = matches
                ? $"Pattern found in {expectation.Path}"
                : $"Pattern not found in {expectation.Path}"
        };
    }

    private async Task<ExpectationResult> ValidateFileNotContainsAsync(
        Expectation expectation,
        StoryResponse story,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(expectation.Path))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "file_not_contains expectation requires a 'path'"
            };
        }

        if (string.IsNullOrEmpty(expectation.Pattern))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "file_not_contains expectation requires a 'pattern'"
            };
        }

        if (string.IsNullOrEmpty(story.WorktreePath))
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "Story does not have a worktree path"
            };
        }

        var targetFile = fileSystem.Path.Combine(story.WorktreePath, expectation.Path);

        if (!fileSystem.File.Exists(targetFile))
        {
            // File doesn't exist = pattern definitely not found = PASS
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = true,
                Message = $"File not found (pattern cannot exist): {expectation.Path}"
            };
        }

        var content = await fileSystem.File.ReadAllTextAsync(targetFile, ct);
        var regex = new Regex(expectation.Pattern, RegexOptions.Multiline);
        var matches = regex.IsMatch(content);

        return new ExpectationResult
        {
            Expectation = expectation,
            Passed = !matches,  // Inverted: pass if pattern NOT found
            Message = matches
                ? $"Pattern unexpectedly found in {expectation.Path}"
                : $"Pattern correctly absent from {expectation.Path}"
        };
    }

    private ExpectationResult ValidateIndexUsage(Expectation expectation, StoryResponse story)
    {
        // Build tool call trace from step outputs (which contain serialized JSON)
        var toolCalls = new List<ToolCallRecord>();

        if (story.Steps != null)
        {
            foreach (var step in story.Steps)
            {
                if (string.IsNullOrEmpty(step.Output))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(step.Output);
                    if (doc.RootElement.TryGetProperty("toolSteps", out var toolStepsArray))
                    {
                        foreach (var toolStep in toolStepsArray.EnumerateArray())
                        {
                            var action = toolStep.TryGetProperty("action", out var actionProp)
                                ? actionProp.GetString() ?? "unknown"
                                : "unknown";

                            var input = toolStep.TryGetProperty("actionInput", out var inputProp)
                                ? inputProp.GetString() ?? ""
                                : "";

                            toolCalls.Add(new ToolCallRecord(
                                action,
                                input,
                                null));
                        }
                    }

                    // Also check for toolCalls from deterministic agents
                    if (doc.RootElement.TryGetProperty("toolCalls", out var toolCallsArray))
                    {
                        foreach (var toolCall in toolCallsArray.EnumerateArray())
                        {
                            var toolName = toolCall.TryGetProperty("toolName", out var nameProp)
                                ? nameProp.GetString() ?? "unknown"
                                : "unknown";

                            var input = toolCall.TryGetProperty("input", out var inputProp)
                                ? inputProp.GetString() ?? ""
                                : "";

                            toolCalls.Add(new ToolCallRecord(
                                toolName,
                                input,
                                null));
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed output
                }
            }
        }

        if (toolCalls.Count == 0)
        {
            return new ExpectationResult
            {
                Expectation = expectation,
                Passed = false,
                Message = "No tool calls found in story execution"
            };
        }

        var metrics = indexAnalyzer.Analyze(toolCalls);

        var failures = new List<string>();

        // Check min_aura_tool_ratio
        if (expectation.MinAuraToolRatio.HasValue &&
            metrics.AuraToolRatio < expectation.MinAuraToolRatio.Value)
        {
            failures.Add($"Aura tool ratio {metrics.AuraToolRatio:P0} < required {expectation.MinAuraToolRatio.Value:P0}");
        }

        // Check max_steps_to_target
        if (expectation.MaxStepsToTarget.HasValue &&
            metrics.StepsToFirstRelevantCode > expectation.MaxStepsToTarget.Value)
        {
            failures.Add($"Steps to target {metrics.StepsToFirstRelevantCode} > allowed {expectation.MaxStepsToTarget.Value}");
        }

        var passed = failures.Count == 0;

        return new ExpectationResult
        {
            Expectation = expectation,
            Passed = passed,
            Message = passed
                ? $"Index usage OK: {metrics.AuraToolRatio:P0} Aura ratio, {metrics.StepsToFirstRelevantCode} steps"
                : string.Join("; ", failures),
            IndexMetrics = metrics
        };
    }
}
