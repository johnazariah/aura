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
/// Dispatches tasks to GitHub Copilot CLI agents running in YOLO mode.
/// </summary>
public sealed class GitHubCopilotDispatcher : IGitHubCopilotDispatcher, ITaskDispatcher
{
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<GitHubCopilotDispatcher> _logger;
    private readonly SemaphoreSlim _availabilityCheck = new(1, 1);
    private bool? _isAvailable;
    private string? _copilotPath;

    /// <inheritdoc/>
    public DispatchTarget Target => DispatchTarget.CopilotCli;

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
                var (exitCode, output) = await RunCommandAsync(path, "--version", null, ct);
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
    public async Task<StoryTask> DispatchTaskAsync(
        StoryTask task,
        string worktreePath,
        IReadOnlyList<StoryTask>? completedTasks = null,
        CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct))
        {
            return task with
            {
                Status = StoryTaskStatus.Failed,
                Error = "GitHub Copilot CLI is not available",
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }

        var startedTask = task with
        {
            Status = StoryTaskStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching task {TaskId}: {TaskTitle} to GH Copilot CLI",
            worktreeName,
            task.Id,
            task.Title);

        try
        {
            // Filter completed tasks to only those this task depends on
            var dependencies = completedTasks?
                .Where(t => task.DependsOn.Contains(t.Id))
                .ToList();

            // Build the prompt for copilot
            var prompt = BuildPrompt(task, dependencies);

            // Run copilot CLI in YOLO mode (auto-accept all tool calls)
            // --yolo: Allow all tools, paths, and URLs without confirmation
            // --no-ask-user: Work autonomously without asking questions
            // --add-dir: Grant access to the worktree directory
            // -s: Silent mode - output only the agent response
            // -p: Non-interactive mode with prompt
            var args = $"-p \"{EscapeArgument(prompt)}\" --yolo --no-ask-user --add-dir \"{worktreePath}\" -s";

            var (exitCode, output) = await RunCommandAsync(_copilotPath!, args, worktreePath, ct);

            // Extract tool improvement proposal if present
            var (cleanOutput, toolProposal) = ExtractToolImprovementProposal(output);

            if (toolProposal != null)
            {
                _logger.LogInformation("[{WorktreeName}] Task {TaskId} included tool improvement proposal", worktreeName, task.Id);
            }

            if (exitCode == 0)
            {
                _logger.LogInformation("[{WorktreeName}] Task {TaskId} completed successfully", worktreeName, task.Id);
                return startedTask with
                {
                    Status = StoryTaskStatus.Completed,
                    Output = cleanOutput,
                    ToolImprovementProposal = toolProposal,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }
            else
            {
                _logger.LogWarning("[{WorktreeName}] Task {TaskId} failed with exit code {ExitCode}", worktreeName, task.Id, exitCode);
                return startedTask with
                {
                    Status = StoryTaskStatus.Failed,
                    Error = $"Exit code {exitCode}: {output}",
                    ToolImprovementProposal = toolProposal,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{WorktreeName}] Task {TaskId} failed with exception", worktreeName, task.Id);
            return startedTask with
            {
                Status = StoryTaskStatus.Failed,
                Error = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoryTask>> DispatchTasksAsync(
        IReadOnlyList<StoryTask> tasks,
        string worktreePath,
        int maxParallelism,
        IReadOnlyList<StoryTask>? completedTasks = null,
        CancellationToken ct = default)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching {TaskCount} tasks with parallelism {MaxParallelism}",
            worktreeName,
            tasks.Count,
            maxParallelism);

        // Use SemaphoreSlim to limit parallelism
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var results = new StoryTask[tasks.Count];

        var dispatchTasks = tasks.Select(async (task, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                results[index] = await DispatchTaskAsync(task, worktreePath, completedTasks, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(dispatchTasks);

        var completed = results.Count(t => t.Status == StoryTaskStatus.Completed);
        var failed = results.Count(t => t.Status == StoryTaskStatus.Failed);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatch complete: {Completed} succeeded, {Failed} failed",
            worktreeName,
            completed,
            failed);

        return results;
    }

    private string BuildPrompt(StoryTask task, IReadOnlyList<StoryTask>? completedDependencies = null)
    {
        // Try to use the template, fall back to inline if not found
        try
        {
            var dependencyOutputs = completedDependencies?
                .Where(t => t.Output != null)
                .Select(t => new { title = t.Title, output = t.Output })
                .ToList();

            return _promptRegistry.Render("task-execute", new
            {
                title = task.Title,
                description = task.Description,
                dependencyOutputs = dependencyOutputs?.Count > 0 ? dependencyOutputs : null,
            });
        }
        catch
        {
            // Fallback to simple prompt if template not found
            var sb = new StringBuilder();
            sb.AppendLine($"# Task: {task.Title}");
            sb.AppendLine();
            sb.AppendLine("## Instructions");
            sb.AppendLine(task.Description);
            sb.AppendLine();
            sb.AppendLine("Execute this task by making the necessary code changes. Be thorough and complete.");
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
