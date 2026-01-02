// <copyright file="TypeScriptCodingAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// TypeScript/JavaScript coding agent that uses ReAct with TS-specific tools.
/// 
/// Capabilities:
/// - Understands TypeScript/JavaScript project structure (package.json, tsconfig.json)
/// - Uses TypeScript compiler for type checking
/// - Runs tests with Jest/Vitest/Mocha
/// - Follows modern TS/JS idioms and ESLint rules
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TypeScriptCodingAgent"/> class.
/// </remarks>
public sealed class TypeScriptCodingAgent(
    IReActExecutor reactExecutor,
    IToolRegistry toolRegistry,
    ILlmProviderRegistry llmRegistry,
    ILogger<TypeScriptCodingAgent> logger) : IAgent
{
    private const double DefaultTemperature = 0.2;

    private readonly IReActExecutor _reactExecutor = reactExecutor;
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry = llmRegistry;
    private readonly ILogger<TypeScriptCodingAgent> _logger = logger;

    /// <inheritdoc/>
    public string AgentId => "typescript-coding";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "TypeScript Coding Agent",
        Description: "TypeScript/JavaScript specialist agent using ReAct with TS-specific tools. " +
                     "Understands npm/yarn/pnpm projects, runs TypeScript compiler, tests with " +
                     "Jest/Vitest, and follows modern TS idioms.",
        Capabilities: ["typescript-coding", "javascript-coding", "coding"],
        Priority: 10,  // Specialist priority
        Languages: ["typescript", "javascript"],
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
            // TypeScript-specific tools
            "typescript.compile",
            "typescript.type_check",
            "typescript.run_tests",
            "typescript.lint",
            "typescript.format",
            // Shell for npm/yarn/pnpm
            "shell.execute",
        ],
        Tags: ["coding", "typescript", "javascript", "agentic", "specialist"]);

    /// <inheritdoc/>
    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting TypeScript coding agent for task: {Task}",
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
            "TypeScript coding agent completed: Success={Success}, Steps={StepCount}",
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

        task.AppendLine("# TypeScript/JavaScript Development Task");
        task.AppendLine();
        task.AppendLine("## Objective");
        task.AppendLine(context.Prompt);
        task.AppendLine();

        task.AppendLine("## Instructions");
        task.AppendLine("""
            You are an expert TypeScript/JavaScript developer using tools to make code changes.
            
            Follow this workflow:
            1. **Understand**: Read relevant files, check package.json and tsconfig.json
            2. **Plan**: Think through the changes needed
            3. **Implement**: Make targeted edits using file.modify or file.write
            4. **Validate**: Run type checking (typescript.type_check) and linting (typescript.lint)
            5. **Test**: Run tests with typescript.run_tests
            6. **Fix**: If there are errors, analyze and fix them
            
            TypeScript Best Practices:
            - Use strict TypeScript settings (strict: true in tsconfig)
            - Define explicit types for function parameters and return values
            - Use interfaces for object shapes, types for unions/intersections
            - Prefer const over let, never use var
            - Use arrow functions for callbacks and short functions
            - Use async/await over raw Promises
            - Use template literals for string interpolation
            - Use optional chaining (?.) and nullish coalescing (??)
            - Export types alongside implementations
            - Use readonly for immutable properties
            
            JavaScript (if no TypeScript):
            - Use ES6+ features (const/let, arrow functions, destructuring)
            - Use JSDoc comments for type hints
            - Follow ESLint recommendations
            
            Project Structure:
            - Check package.json for dependencies and scripts
            - Check tsconfig.json for TypeScript configuration
            - Tests are usually in __tests__/, tests/, or *.test.ts files
            - Look for .eslintrc or eslint.config.js for linting rules
            
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
            ["language"] = "typescript",
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
