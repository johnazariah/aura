// <copyright file="GoCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Go coding agent that uses ReAct with Go-specific tools.
/// 
/// Capabilities:
/// - Understands Go project structure (go.mod, go.sum)
/// - Uses go build/test/vet for validation
/// - Runs gofmt for formatting
/// - Follows Go idioms and effective Go guidelines
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GoCodingAgent"/> class.
/// </remarks>
public sealed class GoCodingAgent(
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry,
    ILlmProviderRegistry llmRegistry,
    ILogger<GoCodingAgent> logger) : IAgent
{
    private const double DefaultTemperature = 0.2;

    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly ILogger<GoCodingAgent> _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "go-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Go Coding Agent",
        Description: "Go specialist agent using ReAct with Go-specific tools. " +
                     "Understands Go modules, runs go build/test/vet, uses gofmt, " +
                     "and follows Effective Go guidelines.",
        Capabilities: ["go-coding", "coding"],
        Priority: 10,  // Specialist priority
        Languages: ["go"],
        Provider: "ollama",
        Temperature: DefaultTemperature,
        Tools:
        [
            // File operations
            "file.read",
            "file.modify",
            "file.write",
            "file.list",
            "file.exists",
            // Go-specific tools
            "go.build",
            "go.test",
            "go.vet",
            "go.fmt",
            "go.mod_tidy",
            // Shell for go commands
            "shell.execute",
        ],
        Tags: ["coding", "go", "golang", "agentic", "specialist"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Go coding agent for task: {Task}",
            context.Prompt.Length > 100 ? context.Prompt[..100] + "..." : context.Prompt);

        var llmProvider = _llmRegistry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No LLM provider available");

        var availableTools = GetAvailableTools();
        var enhancedTask = BuildEnhancedTask(context);

        var options = new ReActOptions
        {
            MaxSteps = 15,
            Model = Metadata.Model,
            Temperature = Metadata.Temperature,
            WorkingDirectory = context.WorkspacePath,
            AdditionalContext = BuildAdditionalContext(context),
            RequireConfirmation = false,
        };

        var result = await _reactExecutor.ExecuteAsync(
            enhancedTask,
            availableTools,
            llmProvider,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Go coding agent completed: Success={Success}, Steps={StepCount}",
            result.Success, result.Steps.Count);

        return BuildAgentOutput(result);
    }

    private IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        var tools = new List<ToolDefinition>();
        foreach (var toolId in Metadata.Tools)
        {
            var tool = _toolRegistry.GetTool(toolId);
            if (tool is not null)
            {
                tools.Add(tool);
            }
        }
        return tools;
    }

    private static string BuildEnhancedTask(AgentContext context)
    {
        var task = new StringBuilder();

        task.AppendLine("# Go Development Task");
        task.AppendLine();
        task.AppendLine("## Objective");
        task.AppendLine(context.Prompt);
        task.AppendLine();

        task.AppendLine("## Instructions");
        task.AppendLine("""
            You are an expert Go developer using tools to make code changes.
            
            Follow this workflow:
            1. **Understand**: Read relevant files, check go.mod for module info
            2. **Plan**: Think through the changes needed
            3. **Implement**: Make targeted edits using file.modify or file.write
            4. **Format**: Run go.fmt to format code (Go requires specific formatting)
            5. **Validate**: Run go.build to check compilation, go.vet for issues
            6. **Test**: Run go.test to execute tests
            7. **Fix**: If there are errors, analyze and fix them
            
            Go Best Practices (Effective Go):
            - Use short, descriptive variable names (i, n, err, ctx)
            - Use MixedCaps (PascalCase for exported, camelCase for unexported)
            - Handle errors explicitly - check every error return
            - Use defer for cleanup operations
            - Prefer composition over inheritance (embed structs)
            - Use interfaces for abstraction (accept interfaces, return structs)
            - Keep interfaces small (1-3 methods)
            - Use context.Context for cancellation and timeouts
            - Use goroutines and channels for concurrency
            - Avoid global state
            - Write table-driven tests
            - Use the blank identifier (_) for unused values
            
            Error Handling Pattern:
            ```go
            result, err := someFunction()
            if err != nil {
                return fmt.Errorf("context: %w", err)
            }
            ```
            
            Project Structure:
            - Check go.mod for module path and dependencies
            - cmd/ contains main packages (executables)
            - internal/ for private packages
            - pkg/ for public packages (optional)
            - Tests are in *_test.go files alongside source
            
            When you're done, provide a clear summary of what was changed.
            """);

        if (!string.IsNullOrEmpty(context.WorkspacePath))
        {
            task.AppendLine();
            task.AppendLine($"## Workspace");
            task.AppendLine($"Working directory: {context.WorkspacePath}");
        }

        return task.ToString();
    }

    private static string? BuildAdditionalContext(AgentContext context)
    {
        if (string.IsNullOrEmpty(context.RagContext))
        {
            return null;
        }

        return $"""
            ## Relevant Code Context (from RAG)
            {context.RagContext}
            """;
    }

    private static AgentOutput BuildAgentOutput(ReActResult result)
    {
        var artifacts = new Dictionary<string, string>
        {
            ["success"] = result.Success.ToString().ToLowerInvariant(),
            ["steps"] = result.Steps.Count.ToString(),
            ["language"] = "go",
        };

        var toolCalls = result.Steps
            .Where(s => s.Action != "finish")
            .Select(s => new ToolCall(s.Action, s.ActionInput, s.Observation))
            .ToList();

        if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            artifacts["error"] = result.Error;
        }

        var content = result.Success
            ? $"## Task Completed Successfully\n\n{result.FinalAnswer}"
            : $"## Task Did Not Complete\n\n**Error:** {result.Error}";

        return new AgentOutput(
            Content: content,
            TokensUsed: result.TotalTokensUsed,
            ToolCalls: toolCalls,
            Artifacts: artifacts);
    }
}
