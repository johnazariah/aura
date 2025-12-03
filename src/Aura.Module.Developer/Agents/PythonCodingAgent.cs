// <copyright file="PythonCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Python coding agent that uses ReAct with Python-specific tools.
/// 
/// Capabilities:
/// - Understands Python project structure (pyproject.toml, requirements.txt)
/// - Uses type checking (mypy/pyright concepts)
/// - Runs pytest for testing
/// - Follows PEP 8 and modern Python idioms
/// </summary>
public sealed class PythonCodingAgent : IAgent
{
    private const string DefaultModel = "qwen2.5-coder:7b";
    private const double DefaultTemperature = 0.2;

    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly ILogger<PythonCodingAgent> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonCodingAgent"/> class.
    /// </summary>
    public PythonCodingAgent(
        IReActExecutor reactExecutor,
        IToolRegistry toolRegistry,
        ILlmProviderRegistry llmRegistry,
        ILogger<PythonCodingAgent> logger)
    {
        _reactExecutor = reactExecutor;
        _toolRegistry = toolRegistry;
        _llmRegistry = llmRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => "python-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "Python Coding Agent",
        Description: "Python specialist agent using ReAct with Python-specific tools. " +
                     "Understands project structure, runs tests with pytest, follows PEP 8, " +
                     "and uses modern Python idioms (type hints, dataclasses, async/await).",
        Capabilities: ["python-coding", "coding"],
        Priority: 10,  // Specialist priority for Python tasks
        Languages: ["python"],
        Provider: "ollama",
        Model: DefaultModel,
        Temperature: DefaultTemperature,
        Tools:
        [
            // File operations
            "file.read",
            "file.modify",
            "file.write",
            "file.list",
            "file.exists",
            // Python-specific tools
            "python.run_script",
            "python.run_tests",
            "python.lint",
            "python.format",
            "python.type_check",
            // Shell for pip, etc.
            "shell.execute",
        ],
        Tags: ["coding", "python", "agentic", "specialist"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Python coding agent for task: {Task}",
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
            "Python coding agent completed: Success={Success}, Steps={StepCount}",
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

        task.AppendLine("# Python Development Task");
        task.AppendLine();
        task.AppendLine("## Objective");
        task.AppendLine(context.Prompt);
        task.AppendLine();

        task.AppendLine("## Instructions");
        task.AppendLine("""
            You are an expert Python developer using tools to make code changes.
            
            Follow this workflow:
            1. **Understand**: Read relevant files, understand the project structure
            2. **Plan**: Think through the changes needed
            3. **Implement**: Make targeted edits using file.modify or file.write
            4. **Validate**: Run linting (python.lint) and type checking (python.type_check)
            5. **Test**: Run tests with python.run_tests (pytest)
            6. **Fix**: If there are errors, analyze and fix them
            
            Python Best Practices:
            - Use type hints for function signatures and class attributes
            - Follow PEP 8 naming: snake_case for functions/variables, PascalCase for classes
            - Use dataclasses or Pydantic for data structures
            - Prefer pathlib over os.path
            - Use f-strings for string formatting
            - Use context managers (with statements) for resources
            - Write docstrings for public functions and classes
            - Use async/await for I/O-bound operations when appropriate
            
            Project Structure:
            - Look for pyproject.toml or setup.py for project config
            - Look for requirements.txt or poetry.lock for dependencies
            - Tests are usually in tests/ directory
            - Use __init__.py for package structure
            
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
            ["language"] = "python",
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
