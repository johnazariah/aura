// <copyright file="InternalAgentExecutor.cs" company="Aura">
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
/// Executes steps using Aura's internal ReAct agents.
/// Supports language-specific agents from the agent registry.
/// </summary>
public sealed class InternalAgentExecutor : IStepExecutor
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<InternalAgentExecutor> _logger;

    /// <inheritdoc/>
    public string ExecutorId => "internal";

    /// <inheritdoc/>
    public string DisplayName => "Internal ReAct Agents";

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalAgentExecutor"/> class.
    /// </summary>
    public InternalAgentExecutor(
        IAgentRegistry agentRegistry,
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmProviderRegistry,
        IPromptRegistry promptRegistry,
        ILogger<InternalAgentExecutor> logger)
    {
        _agentRegistry = agentRegistry;
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _llmProviderRegistry = llmProviderRegistry;
        _promptRegistry = promptRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Internal agents are available if we have a configured LLM provider
        var provider = _llmProviderRegistry.GetDefaultProvider();
        return Task.FromResult(provider != null);
    }

    /// <inheritdoc/>
    public async Task ExecuteStepAsync(
        StoryStep step,
        Story story,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default)
    {
        var worktreePath = story.WorktreePath ?? story.RepositoryPath;

        step.Status = StepStatus.Running;
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Attempts++;

        // Find the best agent for this step's capability and language
        var agent = _agentRegistry.GetBestForCapability(step.Capability, step.Language);
        if (agent is null)
        {
            step.Status = StepStatus.Failed;
            step.Error = $"No agent found for capability '{step.Capability}'" +
                (step.Language != null ? $" with language '{step.Language}'" : "");
            step.CompletedAt = DateTimeOffset.UtcNow;
            return;
        }

        step.AssignedAgentId = agent.AgentId;

        _logger.LogInformation(
            "[{WorktreeName}] Executing step {StepId}: {StepName} with agent {AgentId}",
            Path.GetFileName(worktreePath),
            step.Id,
            step.Name,
            agent.AgentId);

        try
        {
            // Build the prompt
            var prompt = BuildPrompt(step, priorSteps);

            // Get tools for the agent
            var tools = GetToolsForAgent(agent);

            // Get the LLM provider
            var provider = agent.Metadata.Provider is not null
                ? _llmProviderRegistry.GetProvider(agent.Metadata.Provider)
                : _llmProviderRegistry.GetDefaultProvider();

            if (provider is null)
            {
                step.Status = StepStatus.Failed;
                step.Error = "No LLM provider available";
                step.CompletedAt = DateTimeOffset.UtcNow;
                return;
            }

            // Execute with ReAct loop
            var reactOptions = new ReActOptions
            {
                WorkingDirectory = worktreePath,
                MaxSteps = 20,
                Temperature = agent.Metadata.Temperature,
                RequireConfirmation = false,
            };

            var result = await _reactExecutor.ExecuteAsync(
                prompt,
                tools,
                provider,
                reactOptions,
                ct);

            if (result.Success)
            {
                step.Status = StepStatus.Completed;
                step.Output = JsonSerializer.Serialize(new
                {
                    content = result.FinalAnswer,
                    steps = result.Steps.Count,
                    tokensUsed = result.TotalTokensUsed,
                });
                step.CompletedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "[{WorktreeName}] Step {StepId} completed with {Steps} ReAct steps",
                    Path.GetFileName(worktreePath),
                    step.Id,
                    result.Steps.Count);
            }
            else
            {
                step.Status = StepStatus.Failed;
                step.Error = result.Error ?? "Unknown error during execution";
                step.CompletedAt = DateTimeOffset.UtcNow;

                _logger.LogWarning(
                    "[{WorktreeName}] Step {StepId} failed: {Error}",
                    Path.GetFileName(worktreePath),
                    step.Id,
                    step.Error);
            }
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.Error = ex.Message;
            step.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogError(ex,
                "[{WorktreeName}] Step {StepId} failed with exception",
                Path.GetFileName(worktreePath),
                step.Id);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteStepsAsync(
        IReadOnlyList<StoryStep> steps,
        Story story,
        int maxParallelism,
        IReadOnlyList<StoryStep>? priorSteps = null,
        CancellationToken ct = default)
    {
        if (steps.Count == 0)
        {
            return;
        }

        var worktreeName = Path.GetFileName(story.WorktreePath ?? story.RepositoryPath ?? "unknown");
        _logger.LogInformation(
            "[{WorktreeName}] Executing {StepCount} steps with parallelism {MaxParallelism}",
            worktreeName,
            steps.Count,
            maxParallelism);

        // Use SemaphoreSlim to limit parallelism
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var tasks = steps.Select(async step =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await ExecuteStepAsync(step, story, priorSteps, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var completed = steps.Count(s => s.Status == StepStatus.Completed);
        var failed = steps.Count(s => s.Status == StepStatus.Failed);
        _logger.LogInformation(
            "[{WorktreeName}] Execution complete: {Completed} succeeded, {Failed} failed",
            worktreeName,
            completed,
            failed);
    }

    private string BuildPrompt(StoryStep step, IReadOnlyList<StoryStep>? priorSteps)
    {
        try
        {
            var dependencyOutputs = priorSteps?
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

    private List<ToolDefinition> GetToolsForAgent(IAgent agent)
    {
        // Get tools from agent metadata or use defaults
        var toolNames = agent.Metadata.Tools?.ToList() ?? ["file.read", "file.write", "file.list"];

        return toolNames
            .Select(name => _toolRegistry.GetTool(name))
            .Where(t => t is not null)
            .Cast<ToolDefinition>()
            .ToList();
    }
}
