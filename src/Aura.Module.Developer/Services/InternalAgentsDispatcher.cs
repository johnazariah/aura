// <copyright file="InternalAgentsDispatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Text;
using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Data.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches tasks to Aura's internal agents.
/// Uses the agent registry to select the best agent for each task based on detected language.
/// Supports hardcoded agents (RoslynCodingAgent), YAML-configured agents (LanguageSpecialistAgent),
/// and Handlebars prompt agents (from agents/*.md).
/// </summary>
public sealed class InternalAgentsDispatcher : ITaskDispatcher
{
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<InternalAgentsDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalAgentsDispatcher"/> class.
    /// </summary>
    public InternalAgentsDispatcher(
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmProviderRegistry,
        IPromptRegistry promptRegistry,
        IAgentRegistry agentRegistry,
        ILogger<InternalAgentsDispatcher> logger)
    {
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _llmProviderRegistry = llmProviderRegistry;
        _promptRegistry = promptRegistry;
        _agentRegistry = agentRegistry;
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

        // Detect project language
        var detectedLanguage = DetectProjectLanguage(worktreePath);

        // Find the best agent for this language from the registry
        // This supports: hardcoded agents (RoslynCodingAgent), YAML agents (LanguageSpecialistAgent),
        // and Handlebars prompt agents (from agents/*.md)
        var agent = _agentRegistry.GetBestForCapability("coding", detectedLanguage);

        _logger.LogInformation(
            "[{WorktreeName}] Dispatching task {TaskId}: {TaskTitle} to {AgentType} (detected: {Language})",
            Path.GetFileName(worktreePath),
            task.Id,
            task.Title,
            agent?.AgentId ?? "generic ReAct",
            detectedLanguage);

        try
        {
            // If we found a specialist agent, use it
            if (agent != null)
            {
                return await DispatchToAgentAsync(startedTask, task, worktreePath, completedTasks, agent, ct);
            }

            // Fall back to generic ReAct loop with language-specific tools
            return await DispatchToGenericAgentAsync(startedTask, task, worktreePath, completedTasks, detectedLanguage, ct);
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
                "[{WorktreeName}] Task {TaskId} failed with exception",
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

    private async Task<StoryTask> DispatchToAgentAsync(
        StoryTask startedTask,
        StoryTask task,
        string worktreePath,
        IReadOnlyList<StoryTask>? completedTasks,
        IAgent agent,
        CancellationToken ct)
    {
        // Build context for the agent
        var prompt = BuildPrompt(task, completedTasks);

        var context = new AgentContext(
            Prompt: prompt,
            WorkspacePath: worktreePath,
            Properties: new Dictionary<string, object>
            {
                ["taskId"] = task.Id,
                ["taskTitle"] = task.Title,
            });

        var result = await agent.ExecuteAsync(context, ct);

        // AgentOutput doesn't have explicit Success - check if Content is non-empty
        // and doesn't indicate failure
        var isSuccess = !string.IsNullOrWhiteSpace(result.Content) &&
                        !result.Content.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) &&
                        !result.Content.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase);

        if (isSuccess)
        {
            _logger.LogInformation(
                "[{WorktreeName}] Task {TaskId} completed via {AgentId}",
                Path.GetFileName(worktreePath),
                task.Id,
                agent.AgentId);

            return startedTask with
            {
                Status = StoryTaskStatus.Completed,
                Output = result.Content,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
        else
        {
            _logger.LogWarning(
                "[{WorktreeName}] Task {TaskId} failed via {AgentId}: {Content}",
                Path.GetFileName(worktreePath),
                task.Id,
                agent.AgentId,
                result.Content);

            return startedTask with
            {
                Status = StoryTaskStatus.Failed,
                Error = result.Content ?? $"{agent.AgentId} failed",
                Output = result.Content,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }
    private async Task<StoryTask> DispatchToGenericAgentAsync(
        StoryTask startedTask,
        StoryTask task,
        string worktreePath,
        IReadOnlyList<StoryTask>? completedTasks,
        string detectedLanguage,
        CancellationToken ct)
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

        // Build the task prompt with language-specific guidance
        var prompt = BuildPrompt(task, completedTasks, detectedLanguage);

        // Get available tools for task execution (filtered by language)
        var tools = GetTaskExecutionTools(detectedLanguage);

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

    private static string DetectProjectLanguage(string worktreePath)
    {
        // Check for language-specific project files
        if (Directory.GetFiles(worktreePath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(worktreePath, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return "csharp";
        }

        if (Directory.GetFiles(worktreePath, "*.fsproj", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return "fsharp";
        }

        if (File.Exists(Path.Combine(worktreePath, "package.json")))
        {
            // Check if it's TypeScript or JavaScript
            var tsConfig = Path.Combine(worktreePath, "tsconfig.json");
            if (File.Exists(tsConfig))
            {
                return "typescript";
            }

            return "javascript";
        }

        if (File.Exists(Path.Combine(worktreePath, "requirements.txt")) ||
            File.Exists(Path.Combine(worktreePath, "pyproject.toml")) ||
            File.Exists(Path.Combine(worktreePath, "setup.py")))
        {
            return "python";
        }

        if (File.Exists(Path.Combine(worktreePath, "go.mod")))
        {
            return "go";
        }

        if (File.Exists(Path.Combine(worktreePath, "Cargo.toml")))
        {
            return "rust";
        }

        return "unknown";
    }

    private string BuildPrompt(StoryTask task, IReadOnlyList<StoryTask>? completedTasks, string language = "unknown")
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

        // Language-specific guidance
        sb.AppendLine($"## Detected Language: {language}");
        sb.AppendLine();

        switch (language)
        {
            case "csharp":
                AppendCSharpGuidance(sb);
                break;
            case "python":
                AppendPythonGuidance(sb);
                break;
            case "typescript":
            case "javascript":
                AppendTypeScriptGuidance(sb, language);
                break;
            case "go":
                AppendGoGuidance(sb);
                break;
            case "rust":
                AppendRustGuidance(sb);
                break;
            default:
                AppendGenericGuidance(sb);
                break;
        }

        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("1. Analyze the task requirements");
        sb.AppendLine("2. Use the appropriate language-specific tools");
        sb.AppendLine("3. Commit your changes with a clear commit message");
        sb.AppendLine("4. Report success with a summary of what was done");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Work in the provided working directory. Do not ask for clarification - make reasonable decisions.");

        return sb.ToString();
    }

    private static void AppendCSharpGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Tool Selection (CRITICAL for C#)");
        sb.AppendLine();
        sb.AppendLine("For **C# code changes**, you MUST use Aura semantic tools. Do NOT use file.write or file.modify for C#.");
        sb.AppendLine();
        sb.AppendLine("| Task | CORRECT Tool | WRONG Tool |");
        sb.AppendLine("|------|--------------|------------|");
        sb.AppendLine("| Add field to class | `aura.generate(operation: \"property\", isField: true)` | file.modify |");
        sb.AppendLine("| Add property to class | `aura.generate(operation: \"property\")` | file.modify |");
        sb.AppendLine("| Add method to class | `aura.generate(operation: \"method\")` | file.modify |");
        sb.AppendLine("| Add constructor param | `aura.generate(operation: \"constructor\")` | file.modify |");
        sb.AppendLine("| Create new C# file | `aura.generate(operation: \"create_type\")` | file.write |");
        sb.AppendLine("| Implement interface | `aura.generate(operation: \"implement_interface\")` | file.modify |");
        sb.AppendLine("| Rename symbol | `aura.refactor(operation: \"rename\")` | find/replace |");
        sb.AppendLine();
        sb.AppendLine("**Why?** Aura tools understand C# syntax, handle usings, formatting, and find the right insertion point.");
        sb.AppendLine("**file.modify often creates duplicate declarations or syntax errors.**");
    }

    private static void AppendPythonGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Tool Selection for Python");
        sb.AppendLine();
        sb.AppendLine("| Task | Recommended Tool |");
        sb.AppendLine("|------|------------------|");
        sb.AppendLine("| Format code | `python.format` (uses black/ruff) |");
        sb.AppendLine("| Type check | `python.type_check` (mypy) |");
        sb.AppendLine("| Lint code | `python.lint` (ruff/pylint) |");
        sb.AppendLine("| Run tests | `python.run_tests` (pytest) |");
        sb.AppendLine("| Create/modify files | `file.write`, `file.modify` |");
        sb.AppendLine();
        sb.AppendLine("Use `shell.execute` for `pip install` if dependencies needed.");
    }

    private static void AppendTypeScriptGuidance(StringBuilder sb, string language)
    {
        sb.AppendLine($"## Tool Selection for {(language == "typescript" ? "TypeScript" : "JavaScript")}");
        sb.AppendLine();
        sb.AppendLine("| Task | Recommended Tool |");
        sb.AppendLine("|------|------------------|");
        sb.AppendLine("| Type check | `typescript.type_check` (tsc) |");
        sb.AppendLine("| Compile | `typescript.compile` |");
        sb.AppendLine("| Format code | `typescript.format` (prettier) |");
        sb.AppendLine("| Lint code | `typescript.lint` (eslint) |");
        sb.AppendLine("| Run tests | `typescript.run_tests` (jest/vitest) |");
        sb.AppendLine("| Create/modify files | `file.write`, `file.modify` |");
        sb.AppendLine();
        sb.AppendLine("Use `shell.execute` for `npm install` if dependencies needed.");
    }

    private static void AppendGoGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Tool Selection for Go");
        sb.AppendLine();
        sb.AppendLine("| Task | Recommended Tool |");
        sb.AppendLine("|------|------------------|");
        sb.AppendLine("| Build | `go.build` |");
        sb.AppendLine("| Run | `go.run` |");
        sb.AppendLine("| Test | `go.test` |");
        sb.AppendLine("| Format | `go.fmt` |");
        sb.AppendLine("| Vet (lint) | `go.vet` |");
        sb.AppendLine("| Manage dependencies | `go.mod_tidy` |");
        sb.AppendLine("| Create/modify files | `file.write`, `file.modify` |");
    }

    private static void AppendRustGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Tool Selection for Rust");
        sb.AppendLine();
        sb.AppendLine("| Task | Recommended Tool |");
        sb.AppendLine("|------|------------------|");
        sb.AppendLine("| Check | `rust.check` (cargo check) |");
        sb.AppendLine("| Build | `rust.build` (cargo build) |");
        sb.AppendLine("| Test | `rust.test` (cargo test) |");
        sb.AppendLine("| Format | `rust.fmt` (cargo fmt) |");
        sb.AppendLine("| Lint | `rust.clippy` |");
        sb.AppendLine("| Add dependency | `rust.add` (cargo add) |");
        sb.AppendLine("| Create/modify files | `file.write`, `file.modify` |");
    }

    private static void AppendGenericGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Generic Tool Selection");
        sb.AppendLine();
        sb.AppendLine("Use file operations (`file.read`, `file.write`, `file.modify`) for code changes.");
        sb.AppendLine("Use `shell.execute` for language-specific build/test commands.");
        sb.AppendLine("Use `git.commit` to save your changes.");
    }

    private IReadOnlyList<ToolDefinition> GetTaskExecutionTools(string language = "unknown")
    {
        // Get tools that are appropriate for task execution
        var allTools = _toolRegistry.GetAllTools();

        _logger.LogDebug(
            "[TOOL-DEBUG] Total tools in registry: {Count}",
            allTools.Count);

        // Base categories that are always relevant
        var relevantCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file", "git", "search", "shell"
        };

        // Add language-specific categories
        switch (language)
        {
            case "csharp":
                relevantCategories.Add("roslyn");
                relevantCategories.Add("dotnet");
                relevantCategories.Add("aura");
                break;
            case "fsharp":
                relevantCategories.Add("fsharp");
                relevantCategories.Add("dotnet");
                break;
            case "python":
                relevantCategories.Add("python");
                break;
            case "typescript":
            case "javascript":
                relevantCategories.Add("typescript");
                relevantCategories.Add("npm");
                break;
            case "go":
                relevantCategories.Add("go");
                break;
            case "rust":
                relevantCategories.Add("rust");
                relevantCategories.Add("cargo");
                break;
        }

        // Language-specific tool prefixes
        var languagePrefixes = language switch
        {
            "csharp" => new[] { "aura.", "roslyn.", "dotnet." },
            "fsharp" => new[] { "fsharp.", "dotnet." },
            "python" => new[] { "python." },
            "typescript" or "javascript" => new[] { "typescript.", "npm." },
            "go" => new[] { "go." },
            "rust" => new[] { "rust.", "cargo." },
            _ => Array.Empty<string>()
        };

        var filtered = allTools
            .Where(t => t.Categories.Any(c => relevantCategories.Contains(c)) ||
                        t.ToolId.StartsWith("file.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("git.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("search.", StringComparison.OrdinalIgnoreCase) ||
                        t.ToolId.StartsWith("shell.", StringComparison.OrdinalIgnoreCase) ||
                        languagePrefixes.Any(p => t.ToolId.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _logger.LogInformation(
            "[{Language}] Filtered tools: {Count}. IDs: [{Tools}]",
            language,
            filtered.Count,
            string.Join(", ", filtered.Select(t => t.ToolId)));

        return filtered;
    }
}
