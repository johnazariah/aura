// <copyright file="SpawnSubAgentTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools.BuiltIn;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for spawning a sub-agent.
/// </summary>
public record SpawnSubAgentInput
{
    /// <summary>Agent ID to spawn (e.g., "code-review-agent").</summary>
    public required string Agent { get; init; }

    /// <summary>Clear, self-contained task description.</summary>
    public required string Task { get; init; }

    /// <summary>Optional context to pass to sub-agent.</summary>
    public string? Context { get; init; }

    /// <summary>Max steps for sub-agent (default: 10).</summary>
    public int MaxSteps { get; init; } = 10;

    /// <summary>Working directory (injected by framework).</summary>
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// Output from a sub-agent execution.
/// </summary>
public record SpawnSubAgentOutput
{
    /// <summary>Whether sub-agent completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Summary/answer from sub-agent.</summary>
    public required string Summary { get; init; }

    /// <summary>Number of steps used.</summary>
    public int StepsUsed { get; init; }

    /// <summary>Tokens consumed by sub-agent.</summary>
    public int TokensUsed { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Tool for spawning isolated sub-agents to handle complex subtasks.
/// Sub-agents get their own context window and return a summary.
/// </summary>
public class SpawnSubAgentTool : TypedToolBase<SpawnSubAgentInput, SpawnSubAgentOutput>
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmProviderRegistry;
    private readonly ILogger<SpawnSubAgentTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpawnSubAgentTool"/> class.
    /// </summary>
    public SpawnSubAgentTool(
        IAgentRegistry agentRegistry,
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmProviderRegistry,
        ILogger<SpawnSubAgentTool> logger)
    {
        _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        _reactExecutor = reactExecutor ?? throw new ArgumentNullException(nameof(reactExecutor));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _llmProviderRegistry = llmProviderRegistry ?? throw new ArgumentNullException(nameof(llmProviderRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override string ToolId => "spawn_subagent";

    /// <inheritdoc/>
    public override string Name => "Spawn Sub-Agent";

    /// <inheritdoc/>
    public override string Description => """
        Spawn an isolated sub-agent for a complex subtask.
        Use when: (1) task is self-contained, (2) context is filling up,
        (3) you need a fresh perspective.
        The sub-agent gets its own context window and returns a summary.
        """;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["agent", "execution"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<SpawnSubAgentOutput>> ExecuteAsync(
        SpawnSubAgentInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[SUB-AGENT] Spawning sub-agent '{Agent}' for task: {Task}",
            input.Agent,
            input.Task.Length > 100 ? input.Task[..100] + "..." : input.Task);

        // 1. Resolve agent from registry
        var agent = _agentRegistry.GetAgent(input.Agent);
        if (agent is null)
        {
            _logger.LogWarning("[SUB-AGENT] Agent '{Agent}' not found", input.Agent);
            return ToolResult<SpawnSubAgentOutput>.Fail($"Agent '{input.Agent}' not found in registry.");
        }

        // 2. Get default LLM provider
        var llmProvider = _llmProviderRegistry.GetDefaultProvider();
        if (llmProvider is null)
        {
            _logger.LogError("[SUB-AGENT] No default LLM provider configured");
            return ToolResult<SpawnSubAgentOutput>.Fail("No default LLM provider configured.");
        }

        // 3. Get all tools from registry (same tools as parent)
        var availableTools = _toolRegistry.GetAllTools();

        // 4. Build task prompt with optional context
        var taskPrompt = BuildTaskPrompt(agent, input);

        // 5. Execute new ReAct loop with MaxSteps from input
        var options = new ReActOptions
        {
            MaxSteps = input.MaxSteps,
            WorkingDirectory = input.WorkingDirectory,
            AdditionalContext = $"You are operating as a sub-agent spawned by a parent agent.\nAgent: {agent.AgentId}\nCapabilities: {string.Join(", ", agent.Metadata.Capabilities)}",
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ReActResult result;

        try
        {
            result = await _reactExecutor.ExecuteAsync(
                taskPrompt,
                availableTools,
                llmProvider,
                options,
                ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[SUB-AGENT] Execution cancelled for agent '{Agent}'", input.Agent);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUB-AGENT] Execution failed for agent '{Agent}'", input.Agent);
            return ToolResult<SpawnSubAgentOutput>.Fail($"Sub-agent execution failed: {ex.Message}");
        }

        stopwatch.Stop();

        // 6. Calculate tokens used from all steps
        var tokensUsed = result.Steps.Count > 0
            ? result.Steps[^1].CumulativeTokens
            : 0;

        _logger.LogInformation(
            "[SUB-AGENT] Agent '{Agent}' completed. Success={Success}, Steps={Steps}, Tokens={Tokens}",
            input.Agent,
            result.Success,
            result.Steps.Count,
            tokensUsed);

        // 7. Return summary result
        var output = new SpawnSubAgentOutput
        {
            Success = result.Success,
            Summary = result.FinalAnswer,
            StepsUsed = result.Steps.Count,
            TokensUsed = tokensUsed,
            Error = result.Error,
        };

        return ToolResult<SpawnSubAgentOutput>.Ok(output, stopwatch.Elapsed);
    }

    private static string BuildTaskPrompt(IAgent agent, SpawnSubAgentInput input)
    {
        var prompt = new System.Text.StringBuilder();

        // Include agent's description as context
        if (!string.IsNullOrEmpty(agent.Metadata.Description))
        {
            prompt.AppendLine("## Agent Role");
            prompt.AppendLine(agent.Metadata.Description);
            prompt.AppendLine();
        }

        // Include optional context from parent
        if (!string.IsNullOrEmpty(input.Context))
        {
            prompt.AppendLine("## Context from Parent Agent");
            prompt.AppendLine(input.Context);
            prompt.AppendLine();
        }

        // The actual task
        prompt.AppendLine("## Task");
        prompt.AppendLine(input.Task);

        return prompt.ToString();
    }
}
