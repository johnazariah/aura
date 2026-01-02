// <copyright file="RoslynCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sophisticated C# coding agent that uses Roslyn-powered tools
/// in an agentic ReAct loop to understand, plan, and implement code changes.
/// 
/// Capabilities:
/// - Understands existing codebase via code graph and RAG
/// - Plans multi-step changes before implementing
/// - Makes targeted edits using Roslyn's semantic understanding
/// - Validates compilation after changes
/// - Self-corrects when errors occur
/// - Produces documentation and tests alongside implementation
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RoslynCodingAgent"/> class.
/// </remarks>
public sealed class RoslynCodingAgent(
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry,
    ILlmProviderRegistry llmRegistry,
    ILogger<RoslynCodingAgent> logger) : IAgent
{
    private const double DefaultTemperature = 0.2;

    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly ILogger<RoslynCodingAgent> _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "roslyn-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Roslyn Coding Agent",
        Description: "Sophisticated C# coding agent that uses Roslyn-powered tools to understand, " +
                     "plan, and implement code changes. Uses ReAct reasoning to navigate the codebase, " +
                     "make targeted edits, validate compilation, and produce documentation.",
        Capabilities: ["software-development-csharp", "software-development", "coding"],
        Priority: 10,  // Highest priority for C# coding tasks
        Languages: ["csharp"],
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
            // Roslyn-powered tools
            "roslyn.validate_compilation",
            "roslyn.list_projects",
            "roslyn.list_classes",
            "roslyn.get_class_info",
            "roslyn.find_usages",
            "roslyn.find_implementations",
            "roslyn.find_callers",
            // Code graph tools
            "graph.get_type_members",
            // Test tools
            "roslyn.run_tests",
        ],
        Tags: ["coding", "roslyn", "agentic", "csharp", "sophisticated"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Roslyn coding agent for task: {Task}",
            context.Prompt.Length > 100 ? context.Prompt[..100] + "..." : context.Prompt);

        var llmProvider = _llmRegistry.GetDefaultProvider()
            ?? throw new InvalidOperationException("No LLM provider available");

        // Get all available tools from registry that this agent can use
        var availableTools = GetAvailableTools();

        _logger.LogDebug("Agent has access to {ToolCount} tools", availableTools.Count);

        // Build the enhanced task with context
        var enhancedTask = BuildEnhancedTask(context);

        // Build ReAct options
        var options = new ReActOptions
        {
            MaxSteps = 15,  // Allow more steps for complex tasks
            Model = Metadata.Model,
            Temperature = Metadata.Temperature,
            WorkingDirectory = context.WorkspacePath,
            AdditionalContext = BuildAdditionalContext(context),
            RequireConfirmation = false,  // Agent runs autonomously but with validation
            ConfirmationCallback = null,  // No user confirmation in automated mode
        };

        // Execute the ReAct loop
        var result = await _reactExecutor.ExecuteAsync(
            enhancedTask,
            availableTools,
            llmProvider,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Roslyn coding agent completed: Success={Success}, Steps={StepCount}, Tokens={Tokens}",
            result.Success,
            result.Steps.Count,
            result.TotalTokensUsed);

        // Build output with artifacts
        return BuildAgentOutput(result, context);
    }

    private IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        var tools = new List<ToolDefinition>();
        var toolIds = Metadata.Tools;

        foreach (var toolId in toolIds)
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

    private static string BuildEnhancedTask(AgentContext context)
    {
        var task = new StringBuilder();

        task.AppendLine("# C# Development Task");
        task.AppendLine();
        task.AppendLine("## Objective");
        task.AppendLine(context.Prompt);
        task.AppendLine();

        task.AppendLine("## Instructions");
        task.AppendLine("""
            You are an expert C# developer using Roslyn-powered tools to make code changes.
            
            Follow this workflow:
            1. **Understand**: Read relevant files, list classes, understand the codebase structure
            2. **Plan**: Think through the changes needed before making them
            3. **Implement**: Make targeted edits using file.modify (preferred) or file.write for new files
            4. **Validate**: Use roslyn.validate_compilation to check for errors after changes
            5. **Fix**: If there are compilation errors, analyze them and fix
            6. **Document**: Add/update XML documentation comments
            7. **Test**: Consider if tests are needed and create them
            
            Best Practices:
            - Use file.modify for surgical edits to existing files (find exact text, replace it)
            - Prefer small, targeted changes over large rewrites
            - Always validate compilation after making changes
            - Follow .NET naming conventions (PascalCase for public, _camelCase for private fields)
            - Use modern C# features (records, pattern matching, nullable reference types)
            - Include XML documentation for public APIs

            When writing tests:
            - **CRITICAL: BEFORE writing any test file, use file.list to find existing *Tests.cs files**
            - **Read at least one existing test file to learn the testing framework and conventions**
            - Match the project's framework: xUnit uses [Fact]/[Theory], NUnit uses [Test]/[TestFixture]
            - Use the same assertion style and mocking library as existing tests
            - If you see [Fact] in existing tests, use [Fact] not [Test]
            - If you see Assert.Equal(), don't use Assert.AreEqual()
            - **After validation passes, use roslyn.run_tests to actually run the tests and verify they pass**
            - If tests fail, analyze the error and fix the test code
            
            When modifying files:
            - Read the file first to understand its structure
            - Use exact text matching for old_text in file.modify
            - Include enough context in old_text to be unique
            
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
        var additionalContext = new StringBuilder();

        // Add RAG context if available
        if (!string.IsNullOrEmpty(context.RagContext))
        {
            additionalContext.AppendLine("## Relevant Code Context (from RAG)");
            additionalContext.AppendLine(context.RagContext);
            additionalContext.AppendLine();
        }

        // Add conversation history context
        if (context.ConversationHistory.Count > 0)
        {
            additionalContext.AppendLine("## Previous Conversation");
            foreach (var msg in context.ConversationHistory.TakeLast(5))  // Last 5 messages
            {
                var role = msg.Role == ChatRole.User ? "User" : "Assistant";
                var content = msg.Content.Length > 500
                    ? msg.Content[..500] + "..."
                    : msg.Content;
                additionalContext.AppendLine($"{role}: {content}");
            }
            additionalContext.AppendLine();
        }

        return additionalContext.Length > 0 ? additionalContext.ToString() : null;
    }

    private AgentOutput BuildAgentOutput(ReActResult result, AgentContext context)
    {
        var artifacts = new Dictionary<string, string>
        {
            ["success"] = result.Success.ToString().ToLowerInvariant(),
            ["steps"] = result.Steps.Count.ToString(),
            ["duration_ms"] = result.TotalDuration.TotalMilliseconds.ToString("F0"),
        };

        // Collect all tool calls made
        var toolCalls = result.Steps
            .Where(s => s.Action != "finish")
            .Select(s => new ToolCall(s.Action, s.ActionInput, s.Observation))
            .ToList();

        // Extract files that were modified (from file.modify and file.write observations)
        var modifiedFiles = result.Steps
            .Where(s => s.Action.Contains("file", StringComparison.OrdinalIgnoreCase) &&
                       s.Observation.Contains("path", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ActionInput)
            .ToList();

        if (modifiedFiles.Count > 0)
        {
            artifacts["modified_files"] = string.Join("\n", modifiedFiles);
        }

        // Build detailed reasoning trace
        var reasoningTrace = new StringBuilder();
        foreach (var step in result.Steps)
        {
            reasoningTrace.AppendLine($"## Step {step.StepNumber}");
            reasoningTrace.AppendLine($"**Thought:** {step.Thought}");
            reasoningTrace.AppendLine($"**Action:** {step.Action}");
            if (!string.IsNullOrEmpty(step.ActionInput))
            {
                reasoningTrace.AppendLine($"**Input:** {TruncateForDisplay(step.ActionInput)}");
            }
            reasoningTrace.AppendLine($"**Result:** {TruncateForDisplay(step.Observation)}");
            reasoningTrace.AppendLine();
        }
        artifacts["reasoning_trace"] = reasoningTrace.ToString();

        // Error info if failed
        if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            artifacts["error"] = result.Error;
        }

        // Build content summary
        var content = new StringBuilder();
        if (result.Success)
        {
            content.AppendLine("## Task Completed Successfully");
            content.AppendLine();
            content.AppendLine(result.FinalAnswer);
        }
        else
        {
            content.AppendLine("## Task Did Not Complete Successfully");
            content.AppendLine();
            if (!string.IsNullOrEmpty(result.Error))
            {
                content.AppendLine($"**Error:** {result.Error}");
            }
            content.AppendLine();
            content.AppendLine("### Steps Taken");
            foreach (var step in result.Steps.TakeLast(3))
            {
                content.AppendLine($"- {step.Action}: {TruncateForDisplay(step.Thought, 100)}");
            }
        }

        return new AgentOutput(
            Content: content.ToString(),
            TokensUsed: result.TotalTokensUsed,
            ToolCalls: toolCalls,
            Artifacts: artifacts);
    }

    private static string TruncateForDisplay(string text, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length > maxLength
            ? text[..maxLength] + "..."
            : text;
    }
}
