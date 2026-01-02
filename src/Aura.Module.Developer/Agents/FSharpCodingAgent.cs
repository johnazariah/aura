// <copyright file="FSharpCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// F# coding agent that uses FSharp.Compiler.Service-powered tools
/// in an agentic ReAct loop to understand, plan, and implement F# code changes.
///
/// Capabilities:
/// - Understands F# idioms (pipelines, pattern matching, computation expressions)
/// - Uses FSharp.Compiler.Service for semantic analysis
/// - Formats code with Fantomas
/// - Runs tests with dotnet test
/// - Handles significant whitespace correctly
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FSharpCodingAgent"/> class.
/// </remarks>
public sealed class FSharpCodingAgent(
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry,
    ILlmProviderRegistry llmRegistry,
    IPromptRegistry promptRegistry,
    ILogger<FSharpCodingAgent> logger) : IAgent
{
    private const double DefaultTemperature = 0.2;

    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly IPromptRegistry _promptRegistry = promptRegistry;
    private readonly ILogger<FSharpCodingAgent> _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "fsharp-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "F# Coding Agent",
        Description: "F# specialist agent using dotnet tooling and Fantomas for formatting. " +
                     "Understands F# idioms including pipelines, pattern matching, and computation expressions.",
        Capabilities: ["fsharp-coding", "coding", "functional-programming"],
        Priority: 10,  // Specialist priority for F# tasks
        Languages: ["fsharp"],
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
            // F#-specific tools
            "fsharp.check_project",
            "fsharp.build",
            "fsharp.format",
            "fsharp.test",
            "fsharp.fsi",
        ],
        Tags: ["coding", "fsharp", "functional", "agentic", "specialist"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting F# coding agent for task: {Task}",
            context.Prompt.Length > 100 ? context.Prompt[..100] + "..." : context.Prompt);

        var llmProvider = _llmRegistry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No LLM provider available");

        var availableTools = GetAvailableTools();
        _logger.LogDebug("Agent has access to {ToolCount} tools", availableTools.Count);

        // Build task using prompt template
        var enhancedTask = BuildEnhancedTask(context);

        var options = new ReActOptions
        {
            MaxSteps = 15,
            Model = Metadata.Model,
            Temperature = Metadata.Temperature,
            WorkingDirectory = context.WorkspacePath,
            AdditionalContext = context.RagContext,
            RequireConfirmation = false,
        };

        var result = await _reactExecutor.ExecuteAsync(
            enhancedTask,
            availableTools,
            llmProvider,
            options,
            cancellationToken);

        _logger.LogInformation(
            "F# coding agent completed: Success={Success}, Steps={StepCount}",
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
            else
            {
                _logger.LogWarning("Tool {ToolId} not found in registry", toolId);
            }
        }

        return tools;
    }

    private string BuildEnhancedTask(AgentContext context)
    {
        // Try to use prompt template, fall back to inline if not available
        try
        {
            return _promptRegistry.Render("fsharp-coding", new
            {
                prompt = context.Prompt,
                workspacePath = context.WorkspacePath,
                ragContext = context.RagContext,
                conversationHistory = context.ConversationHistory
                    .TakeLast(5)
                    .Select(m => new { role = m.Role.ToString(), content = m.Content })
                    .ToList(),
            });
        }
        catch
        {
            // Fall back to inline prompt
            return BuildInlineTask(context);
        }
    }

    private static string BuildInlineTask(AgentContext context)
    {
        var task = new StringBuilder();

        task.AppendLine("# F# Development Task");
        task.AppendLine();
        task.AppendLine("## Objective");
        task.AppendLine(context.Prompt);
        task.AppendLine();

        task.AppendLine("## Instructions");
        task.AppendLine("""
            You are an expert F# developer using FSharp.Compiler.Service-powered tools to make code changes.

            Follow this workflow:
            1. **Understand**: Read relevant files, understand the module structure
            2. **Plan**: Think through the changes needed
            3. **Implement**: Make targeted edits using file.modify or file.write
            4. **Check**: Use fsharp.check_file or fsharp.check_project to verify
            5. **Format**: Use fsharp.format (Fantomas) to ensure proper formatting
            6. **Test**: Run tests with fsharp.test if applicable

            F# Best Practices:
            - Use the |> pipeline operator for data transformations
            - Prefer immutable data (records, discriminated unions)
            - Use pattern matching extensively
            - Use Option<'T> instead of null
            - Use Result<'T,'E> for error handling
            - Prefer function composition over classes
            - Use computation expressions for async, result, etc.
            - Keep functions small and focused
            - Use type inference - don't over-annotate

            F# Syntax Reminders:
            - Significant whitespace - indentation matters!
            - let bindings: `let x = 5`
            - Function definition: `let add x y = x + y`
            - Pattern matching: `match x with | Some v -> v | None -> 0`
            - Discriminated unions: `type Shape = Circle of float | Rectangle of float * float`
            - Records: `type Person = { Name: string; Age: int }`
            - Async: `async { let! result = asyncOp() return result }`
            - Pipeline: `[1;2;3] |> List.map (fun x -> x * 2) |> List.sum`

            Project Structure:
            - .fsproj files define F# projects
            - File order matters in F# - dependencies must come first
            - Use namespaces and modules for organization
            - Tests often use Expecto, FsUnit, or Unquote

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

    private static AgentOutput BuildAgentOutput(ReActResult result)
    {
        var artifacts = new Dictionary<string, string>
        {
            ["success"] = result.Success.ToString().ToLowerInvariant(),
            ["steps"] = result.Steps.Count.ToString(),
            ["language"] = "fsharp",
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
