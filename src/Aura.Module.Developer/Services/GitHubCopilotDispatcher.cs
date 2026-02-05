// <copyright file="GitHubCopilotDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aura.Foundation.Prompts;
using Aura.Module.Developer.Data.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes steps using GitHub Copilot CLI in YOLO mode.
/// Implements both <see cref="IStepExecutor"/> for unified execution
/// and <see cref="IGitHubCopilotDispatcher"/> for legacy compatibility.
/// </summary>
public sealed class GitHubCopilotDispatcher : IGitHubCopilotDispatcher, IStepExecutor
{
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<GitHubCopilotDispatcher> _logger;
    private readonly SemaphoreSlim _availabilityCheck = new(1, 1);
    private bool? _isAvailable;
    private string? _copilotPath;

    /// <inheritdoc/>
    public string ExecutorId => "copilot";

    /// <inheritdoc/>
    public string DisplayName => "GitHub Copilot CLI";

    // Common installation paths for copilot CLI
    private static readonly string[] CopilotSearchPaths =
    [
        "copilot", // In PATH
        @"C:\nvm4w\nodejs\copilot.cmd",
        @"C:\Program Files\nodejs\copilot.cmd",
        @"C:\Program Files (x86)\nodejs\copilot.cmd",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubCopilotDispatcher"/> class.
    /// </summary>
    public GitHubCopilotDispatcher(IPromptRegistry promptRegistry, ILogger<GitHubCopilotDispatcher> logger)
    {
        _promptRegistry = promptRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_isAvailable.HasValue)
        {
            return _isAvailable.Value;
        }

        await _availabilityCheck.WaitAsync(ct);
        try
        {
            if (_isAvailable.HasValue)
            {
                return _isAvailable.Value;
            }

            // Search for copilot CLI in common installation paths
            foreach (var path in CopilotSearchPaths)
            {
                var (exitCode, output) = await RunCommandAsync(path, "--version", null, null, ct);
                if (exitCode == 0)
                {
                    _copilotPath = path;
                    _logger.LogInformation("GitHub Copilot CLI found at {Path}: {Version}", path, output.Trim());
                    _isAvailable = true;
                    return true;
                }
            }

            _logger.LogWarning("GitHub Copilot CLI not found. Install with: winget install GitHub.Copilot");
            _isAvailable = false;
            return false;
        }
        finally
        {
            _availabilityCheck.Release();
        }
    }

    /// <inheritdoc/>
    public Task ExecuteStepAsync(
        StoryStep step,
        Story story,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default)
    {
        var worktreePath = story.WorktreePath ?? story.RepositoryPath
            ?? throw new InvalidOperationException("Story has no worktree or repository path");
        return DispatchStepWithStoryAsync(step, story, worktreePath, priorSteps, githubToken: null, ct);
    }

    /// <inheritdoc/>
    public Task ExecuteStepsAsync(
        IReadOnlyList<StoryStep> steps,
        Story story,
        int maxParallelism,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default)
    {
        var worktreePath = story.WorktreePath ?? story.RepositoryPath
            ?? throw new InvalidOperationException("Story has no worktree or repository path");
        return DispatchStepsWithStoryAsync(steps, story, worktreePath, maxParallelism, priorSteps, githubToken: null, ct);
    }

    /// <inheritdoc/>
    public async Task DispatchStepAsync(
        StoryStep step,
        string worktreePath,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct))
        {
            step.Status = StepStatus.Failed;
            step.Error = "GitHub Copilot CLI is not available";
            step.CompletedAt = DateTimeOffset.UtcNow;
            return;
        }

        step.Status = StepStatus.Running;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching step {StepId}: {StepName} to GH Copilot CLI",
            worktreeName,
            step.Id,
            step.Name);

        try
        {
            // Build the prompt for copilot
            var prompt = BuildPrompt(step, completedSteps);

            // Write MCP config to temp file (Copilot CLI requires file path with @ prefix)
            var mcpConfigPath = Path.Combine(Path.GetTempPath(), "aura-mcp-config.json");
            const string mcpConfig = """
                {
                  "mcpServers": {
                    "aura": {
                      "type": "http",
                      "url": "http://localhost:5300/mcp",
                      "tools": ["*"]
                    }
                  }
                }
                """;
            await File.WriteAllTextAsync(mcpConfigPath, mcpConfig, ct);

            // Run copilot CLI in YOLO mode (auto-accept all tool calls)
            // --yolo: Allow all tools, paths, and URLs without confirmation
            // --no-ask-user: Work autonomously without asking questions
            // --add-dir: Grant access to the worktree directory
            // --additional-mcp-config: Connect to Aura MCP server for aura_search, aura_generate, etc.
            // -s: Silent mode - output only the agent response
            // -p: Non-interactive mode with prompt
            var args = $"-p \"{EscapeArgument(prompt)}\" --yolo --no-ask-user --add-dir \"{worktreePath}\" --additional-mcp-config \"@{mcpConfigPath}\" -s";

            var (exitCode, output) = await RunCommandAsync(_copilotPath!, args, worktreePath, githubToken, ct);

            // Extract tool improvement proposal if present
            var (cleanOutput, _) = ExtractToolImprovementProposal(output);

            if (exitCode == 0)
            {
                _logger.LogInformation("[{WorktreeName}] Step {StepId} completed successfully", worktreeName, step.Id);
                step.Status = StepStatus.Completed;
                // Wrap output in JSON since the column is JSONB
                step.Output = JsonSerializer.Serialize(new { content = cleanOutput });
                step.CompletedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _logger.LogWarning("[{WorktreeName}] Step {StepId} failed with exit code {ExitCode}", worktreeName, step.Id, exitCode);
                step.Status = StepStatus.Failed;
                step.Error = $"Exit code {exitCode}: {output}";
                step.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{WorktreeName}] Step {StepId} failed with exception", worktreeName, step.Id);
            step.Status = StepStatus.Failed;
            step.Error = ex.Message;
            step.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <inheritdoc/>
    public async Task DispatchStepsAsync(
        IReadOnlyList<StoryStep> steps,
        string worktreePath,
        int maxParallelism,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default)
    {
        if (steps.Count == 0)
        {
            return;
        }

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching {StepCount} steps with parallelism {MaxParallelism}",
            worktreeName,
            steps.Count,
            maxParallelism);

        // Use SemaphoreSlim to limit parallelism
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var dispatchTasks = steps.Select(async step =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await DispatchStepAsync(step, worktreePath, completedSteps, githubToken, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(dispatchTasks);

        var completed = steps.Count(s => s.Status == StepStatus.Completed);
        var failed = steps.Count(s => s.Status == StepStatus.Failed);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatch complete: {Completed} succeeded, {Failed} failed",
            worktreeName,
            completed,
            failed);
    }

    /// <summary>
    /// Internal dispatch method that has full Story context for building proper prompts.
    /// </summary>
    private async Task DispatchStepWithStoryAsync(
        StoryStep step,
        Story story,
        string worktreePath,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct))
        {
            step.Status = StepStatus.Failed;
            step.Error = "GitHub Copilot CLI is not available";
            step.CompletedAt = DateTimeOffset.UtcNow;
            return;
        }

        step.Status = StepStatus.Running;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching step {StepId}: {StepName} to GH Copilot CLI",
            worktreeName,
            step.Id,
            step.Name);

        try
        {
            // Build the prompt for copilot with full story context
            var prompt = BuildPromptWithStory(step, story, completedSteps);

            // Write MCP config to temp file (Copilot CLI requires file path with @ prefix)
            var mcpConfigPath = Path.Combine(Path.GetTempPath(), $"aura-mcp-config-{step.Id}.json");
            const string mcpConfig = """
                {
                  "mcpServers": {
                    "aura": {
                      "type": "http",
                      "url": "http://localhost:5300/mcp",
                      "tools": ["*"]
                    }
                  }
                }
                """;
            await File.WriteAllTextAsync(mcpConfigPath, mcpConfig, ct);

            // Run copilot CLI in YOLO mode (auto-accept all tool calls)
            var args = $"-p \"{EscapeArgument(prompt)}\" --yolo --no-ask-user --add-dir \"{worktreePath}\" --additional-mcp-config \"@{mcpConfigPath}\" -s";

            var (exitCode, output) = await RunCommandAsync(_copilotPath!, args, worktreePath, githubToken, ct);

            // Extract tool improvement proposal if present
            var (cleanOutput, _) = ExtractToolImprovementProposal(output);

            if (exitCode == 0)
            {
                _logger.LogInformation("[{WorktreeName}] Step {StepId} completed successfully", worktreeName, step.Id);
                step.Status = StepStatus.Completed;
                step.Output = JsonSerializer.Serialize(new { content = cleanOutput });
                step.CompletedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _logger.LogWarning("[{WorktreeName}] Step {StepId} failed with exit code {ExitCode}", worktreeName, step.Id, exitCode);
                step.Status = StepStatus.Failed;
                step.Error = $"Exit code {exitCode}: {output}";
                step.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{WorktreeName}] Step {StepId} failed with exception", worktreeName, step.Id);
            step.Status = StepStatus.Failed;
            step.Error = ex.Message;
            step.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Internal dispatch method for multiple steps that has full Story context.
    /// </summary>
    private async Task DispatchStepsWithStoryAsync(
        IReadOnlyList<StoryStep> steps,
        Story story,
        string worktreePath,
        int maxParallelism,
        IReadOnlyList<StoryStep>? completedSteps = null,
        string? githubToken = null,
        CancellationToken ct = default)
    {
        if (steps.Count == 0)
        {
            return;
        }

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching {StepCount} steps with parallelism {MaxParallelism}",
            worktreeName,
            steps.Count,
            maxParallelism);

        // Use SemaphoreSlim to limit parallelism
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var dispatchTasks = steps.Select(async step =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await DispatchStepWithStoryAsync(step, story, worktreePath, completedSteps, githubToken, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(dispatchTasks);

        var completed = steps.Count(s => s.Status == StepStatus.Completed);
        var failed = steps.Count(s => s.Status == StepStatus.Failed);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatch complete: {Completed} succeeded, {Failed} failed",
            worktreeName,
            completed,
            failed);
    }

    private string BuildPrompt(StoryStep step, IReadOnlyList<StoryStep>? completedDependencies = null)
    {
        // Try to use the template, fall back to inline if not found
        try
        {
            var dependencyOutputs = completedDependencies?
                .Where(s => s.Output != null)
                .Select(s => new { title = s.Name, output = s.Output })
                .ToList();

            return _promptRegistry.Render("step-execute", new
            {
                title = step.Name,
                description = step.Description ?? step.Name,
                dependencyOutputs = dependencyOutputs?.Count > 0 ? dependencyOutputs : null,
            });
        }
        catch
        {
            // Fallback to simple prompt if template not found
            var sb = new StringBuilder();
            sb.AppendLine($"# Step: {step.Name}");
            sb.AppendLine();
            sb.AppendLine("## Instructions");
            sb.AppendLine(step.Description ?? step.Name);
            sb.AppendLine();
            sb.AppendLine("Execute this step by making the necessary code changes. Be thorough and complete.");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Builds a prompt with full story context, matching InternalAgentExecutor behavior.
    /// </summary>
    private string BuildPromptWithStory(StoryStep step, Story story, IReadOnlyList<StoryStep>? priorSteps)
    {
        try
        {
            var dependencyOutputs = priorSteps?
                .Where(s => s.Output != null)
                .Select(s => new { title = s.Name, output = s.Output })
                .ToList();

            // Extract analysis summary from analyzed context JSON
            string? analysisText = null;
            if (!string.IsNullOrEmpty(story.AnalyzedContext))
            {
                try
                {
                    using var doc = JsonDocument.Parse(story.AnalyzedContext);
                    if (doc.RootElement.TryGetProperty("analysis", out var analysisProp))
                    {
                        analysisText = analysisProp.GetString();
                    }
                }
                catch
                {
                    // If JSON parsing fails, use the raw context
                    analysisText = story.AnalyzedContext;
                }
            }

            return _promptRegistry.Render("step-execute", new
            {
                stepName = step.Name,
                stepDescription = step.Description ?? step.Name,
                issueTitle = story.Title,
                analysis = analysisText,
                revisionFeedback = step.NeedsRework ? step.PreviousOutput : null,
                dependencyOutputs = dependencyOutputs?.Count > 0 ? dependencyOutputs : null,
            });
        }
        catch
        {
            // Fallback to simple prompt if template not found
            var sb = new StringBuilder();
            sb.AppendLine($"# Step: {step.Name}");
            sb.AppendLine();
            sb.AppendLine($"## Story: {story.Title}");
            sb.AppendLine();
            sb.AppendLine("## Instructions");
            sb.AppendLine(step.Description ?? step.Name);
            sb.AppendLine();
            sb.AppendLine("Execute this step by making the necessary code changes. Use Aura MCP tools (aura_generate, aura_refactor) for C# code changes.");
            return sb.ToString();
        }
    }

    private static string EscapeArgument(string arg)
    {
        // Escape double quotes and backslashes for command line
        return arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static (string CleanOutput, string? ToolProposal) ExtractToolImprovementProposal(string output)
    {
        // Look for the tool improvement proposal section in the agent output
        const string ProposalHeader = "### Tool Improvement Proposal";
        var proposalIndex = output.IndexOf(ProposalHeader, StringComparison.OrdinalIgnoreCase);

        if (proposalIndex < 0)
        {
            return (output, null);
        }

        // Extract everything from the proposal header to the end (or next major section)
        var proposalStart = proposalIndex;
        var proposalEnd = output.Length;

        // Look for the next markdown header that would indicate end of proposal section
        var nextHeaderIndex = output.IndexOf("\n## ", proposalIndex + ProposalHeader.Length, StringComparison.Ordinal);
        if (nextHeaderIndex > 0)
        {
            proposalEnd = nextHeaderIndex;
        }

        var proposal = output[proposalStart..proposalEnd].Trim();
        var cleanOutput = output[..proposalStart].Trim();

        // Append any content after the proposal
        if (proposalEnd < output.Length)
        {
            cleanOutput += "\n\n" + output[proposalEnd..].Trim();
        }

        return (cleanOutput, proposal);
    }

    private async Task<(int ExitCode, string Output)> RunCommandAsync(
        string command,
        string arguments,
        string? workingDirectory,
        string? githubToken,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Pass GitHub token as environment variable if provided
        if (!string.IsNullOrEmpty(githubToken))
        {
            psi.Environment["GITHUB_TOKEN"] = githubToken;
            psi.Environment["GH_TOKEN"] = githubToken;
        }

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            return (process.ExitCode, string.IsNullOrEmpty(error) ? output : $"{output}\n{error}");
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
