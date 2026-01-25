// <copyright file="InternalAgentsDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Text;
using System.Text.Json;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Data.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches tasks to Aura's internal ReAct agents.
/// Uses the configured LLM provider and Aura's tool registry.
/// </summary>
public sealed class InternalAgentsDispatcher : ITaskDispatcher
{
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<InternalAgentsDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalAgentsDispatcher"/> class.
    /// </summary>
    public InternalAgentsDispatcher(
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmProviderRegistry,
        IPromptRegistry promptRegistry,
        ILogger<InternalAgentsDispatcher> logger)
    {
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _llmProviderRegistry = llmProviderRegistry;
        _promptRegistry = promptRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DispatchTarget Target => DispatchTarget.InternalAgents;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Internal agents are always available if we have a configured LLM provider
        var provider = _llmProviderRegistry.GetDefaultProvider();
        return Task.FromResult(provider != null);
    }

    /// <inheritdoc/>
    public async Task<StoryTask> DispatchTaskAsync(
        StoryTask task,
        string worktreePath,
        IReadOnlyList<StoryTask>? completedTasks = null,
        CancellationToken ct = default)
    {
        var startedTask = task with
        {
            Status = StoryTaskStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };

        _logger.LogInformation(
            "[{WorktreeName}] Dispatching task {TaskId}: {TaskTitle} to internal ReAct agent",
            Path.GetFileName(worktreePath),
            task.Id,
            task.Title);

        try
        {
            var provider = _llmProviderRegistry.GetDefaultProvider();
            if (provider == null)
            {
                return startedTask with
                {
                    Status = StoryTaskStatus.Failed,
                    Error = "No LLM provider configured",
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }

            // Build the task prompt
            var prompt = BuildPrompt(task, completedTasks);

            // Get available tools for task execution
            var tools = GetTaskExecutionTools();

            var options = new ReActOptions
            {
                WorkingDirectory = worktreePath,
                MaxSteps = 20, // More steps for complex tasks
                RequireConfirmation = false, // Auto-approve for autonomous execution
                RetryOnFailure = true,
                MaxRetries = 2,
            };

            var result = await _reactExecutor.ExecuteAsync(
                prompt,
                tools,
                provider,
                options,
                ct);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[{WorktreeName}] Task {TaskId} completed successfully in {Steps} steps",
                    Path.GetFileName(worktreePath),
                    task.Id,
                    result.Steps.Count);

                return startedTask with
                {
                    Status = StoryTaskStatus.Completed,
                    Output = result.FinalAnswer,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }
            else
            {
                _logger.LogWarning(
                    "[{WorktreeName}] Task {TaskId} failed: {Error}",
                    Path.GetFileName(worktreePath),
                    task.Id,
                    result.Error);

                return startedTask with
                {
                    Status = StoryTaskStatus.Failed,
                    Error = result.Error ?? "Unknown error",
                    Output = result.FinalAnswer,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "[{WorktreeName}] Task {TaskId} was cancelled",
                Path.GetFileName(worktreePath),
                task.Id);

            return startedTask with
            {
                Status = StoryTaskStatus.Failed,
                Error = "Task was cancelled",
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{WorktreeName}] Task {TaskId} threw exception",
                Path.GetFileName(worktreePath),
                task.Id);

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
        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation(
            "[{WorktreeName}] Dispatching {TaskCount} tasks with parallelism {MaxParallelism}",
            worktreeName,
            tasks.Count,
            maxParallelism);

        using var semaphore = new SemaphoreSlim(maxParallelism);
        var dispatchTasks = tasks.Select(async task =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await DispatchTaskAsync(task, worktreePath, completedTasks, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(dispatchTasks);

        var succeeded = results.Count(r => r.Status == StoryTaskStatus.Completed);
        var failed = results.Count(r => r.Status == StoryTaskStatus.Failed);

        _logger.LogInformation(
            "[{WorktreeName}] Dispatch complete: {Succeeded} succeeded, {Failed} failed",
            worktreeName,
            succeeded,
            failed);

        return results;
    }

    private string BuildPrompt(StoryTask task, IReadOnlyList<StoryTask>? completedTasks)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Task Execution");
        sb.AppendLine();
        sb.AppendLine($"## Task: {task.Title}");
        sb.AppendLine();
        sb.AppendLine(task.Description);
        sb.AppendLine();

        if (completedTasks?.Count > 0)
        {
            sb.AppendLine("## Previously Completed Tasks (for context)");
            sb.AppendLine();
            foreach (var completed in completedTasks)
            {
                sb.AppendLine($"- **{completed.Title}**: {completed.Output?.Split('\n').FirstOrDefault() ?? "Completed"}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("1. Analyze the task requirements");
        sb.AppendLine("2. Use the available tools to implement the changes");
        sb.AppendLine("3. Prefer semantic tools (aura.*, roslyn.*) over text manipulation when available");
        sb.AppendLine("4. Commit your changes with a clear commit message");
        sb.AppendLine("5. Report success with a summary of what was done");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Work in the provided working directory. Do not ask for clarification - make reasonable decisions.");

        return sb.ToString();
    }

    private IReadOnlyList<ToolDefinition> GetTaskExecutionTools()
    {
        // Get tools that are appropriate for task execution
        var allTools = _toolRegistry.GetAllTools();

        // Include file operations, git, search, and coding tools
        var relevantCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file", "git", "search", "roslyn", "dotnet", "aura", "shell"
        };

        return allTools
            .Where(t => t.Categories.Any(c => relevantCategories.Contains(c)) ||
                        t.ToolId.StartsWith("file.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("git.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("search.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("aura.", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
