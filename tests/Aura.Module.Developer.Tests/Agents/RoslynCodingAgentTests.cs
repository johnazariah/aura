// <copyright file="RoslynCodingAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Agents;

using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class RoslynCodingAgentTests
{
    private readonly IReActExecutor _reactExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly ILlmProvider _llmProvider;
    private readonly RoslynCodingAgent _agent;

    public RoslynCodingAgentTests()
    {
        _reactExecutor = Substitute.For<IReActExecutor>();
        _toolRegistry = Substitute.For<IToolRegistry>();
        _llmRegistry = Substitute.For<ILlmProviderRegistry>();
        _llmProvider = Substitute.For<ILlmProvider>();

        _llmRegistry.GetDefaultProvider().Returns(_llmProvider);

        _agent = new RoslynCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            NullLogger<RoslynCodingAgent>.Instance);
    }

    [Fact]
    public void AgentId_ShouldBeRoslynCoding()
    {
        Assert.Equal("roslyn-coding", _agent.AgentId);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectCapabilities()
    {
        Assert.Contains("csharp-coding", _agent.Metadata.Capabilities);
        Assert.Contains("coding", _agent.Metadata.Capabilities);
        Assert.Contains("refactoring", _agent.Metadata.Capabilities);
        Assert.Contains("csharp-documentation", _agent.Metadata.Capabilities);
    }

    [Fact]
    public void Metadata_ShouldHavePriority10()
    {
        Assert.Equal(10, _agent.Metadata.Priority);
    }

    [Fact]
    public void Metadata_ShouldHaveRoslynTools()
    {
        var tools = _agent.Metadata.Tools;

        // File tools
        Assert.Contains("file.read", tools);
        Assert.Contains("file.modify", tools);
        Assert.Contains("file.write", tools);

        // Roslyn tools
        Assert.Contains("roslyn.validate_compilation", tools);
        Assert.Contains("roslyn.list_projects", tools);
        Assert.Contains("roslyn.list_classes", tools);
        Assert.Contains("roslyn.find_usages", tools);

        // Graph tools
        Assert.Contains("graph.get_type_members", tools);

        // Test tools
        Assert.Contains("roslyn.run_tests", tools);
    }

    [Fact]
    public void Metadata_ShouldBeCSharpLanguage()
    {
        Assert.Contains("csharp", _agent.Metadata.Languages);
    }

    [Fact]
    public void Metadata_ShouldHaveAgenticTags()
    {
        Assert.Contains("agentic", _agent.Metadata.Tags);
        Assert.Contains("roslyn", _agent.Metadata.Tags);
        Assert.Contains("sophisticated", _agent.Metadata.Tags);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallReActExecutor()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Add a new method to TestClass");

        var successfulResult = new ReActResult
        {
            Success = true,
            FinalAnswer = "Added method DoSomething to TestClass",
            Steps =
            [
                new ReActStep
                {
                    StepNumber = 1,
                    Thought = "I need to read the file first",
                    Action = "file.read",
                    ActionInput = """{"path": "TestClass.cs"}""",
                    Observation = "public class TestClass { }",
                },
                new ReActStep
                {
                    StepNumber = 2,
                    Thought = "Now I'll add the method",
                    Action = "file.modify",
                    ActionInput = """{"filePath": "TestClass.cs", "oldText": "{ }", "newText": "{ public void DoSomething() { } }"}""",
                    Observation = """{"filePath": "TestClass.cs", "replacementsMade": 1}""",
                },
                new ReActStep
                {
                    StepNumber = 3,
                    Thought = "The task is complete",
                    Action = "finish",
                    ActionInput = "Added method DoSomething to TestClass",
                    Observation = "Task completed",
                },
            ],
            TotalDuration = TimeSpan.FromSeconds(5),
            TotalTokensUsed = 500,
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(successfulResult));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        Assert.Contains("Successfully", output.Content);
        Assert.Equal("true", output.Artifacts["success"]);
        Assert.Equal("3", output.Artifacts["steps"]);
        Assert.Equal(500, output.TokensUsed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassWorkspacePathToReActOptions()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace(
            "Add a new method",
            @"C:\work\myproject");

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = [],
            }));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        await _reactExecutor.Received(1).ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Is<ReActOptions>(o => o.WorkingDirectory == @"C:\work\myproject"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRagContext_ShouldIncludeInAdditionalContext()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Add a new method")
        {
            RagContext = "Existing class has methods: Get(), Set()",
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = [],
            }));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        await _reactExecutor.Received(1).ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Is<ReActOptions>(o =>
                o.AdditionalContext != null &&
                o.AdditionalContext.Contains("Get(), Set()")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReActFails_ShouldReturnFailureOutput()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Do something impossible");

        var failedResult = new ReActResult
        {
            Success = false,
            FinalAnswer = "",
            Steps =
            [
                new ReActStep
                {
                    StepNumber = 1,
                    Thought = "I tried but failed",
                    Action = "file.read",
                    ActionInput = """{"path": "missing.cs"}""",
                    Observation = "Error: File not found",
                },
            ],
            Error = "Reached maximum steps without completing task",
            TotalDuration = TimeSpan.FromSeconds(10),
            TotalTokensUsed = 200,
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(failedResult));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        Assert.Equal("false", output.Artifacts["success"]);
        Assert.Contains("error", output.Artifacts.Keys);
        Assert.Contains("Did Not Complete", output.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoLlmProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _llmRegistry.GetDefaultProvider().Returns((ILlmProvider?)null);

        var agent = new RoslynCodingAgent(
            _reactExecutor,
            _toolRegistry,
            _llmRegistry,
            NullLogger<RoslynCodingAgent>.Instance);

        var context = AgentContext.FromPrompt("Do something");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ExecuteAsync(context));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRequestAvailableToolsFromRegistry()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Add a method");

        var fileReadTool = new ToolDefinition
        {
            ToolId = "file.read",
            Name = "Read File",
            Description = "Reads a file",
            Handler = (_, _) => Task.FromResult(ToolResult.Ok("content")),
        };

        var validateTool = new ToolDefinition
        {
            ToolId = "roslyn.validate_compilation",
            Name = "Validate Compilation",
            Description = "Validates compilation",
            Handler = (_, _) => Task.FromResult(ToolResult.Ok(new { success = true })),
        };

        _toolRegistry.GetTool("file.read").Returns(fileReadTool);
        _toolRegistry.GetTool("roslyn.validate_compilation").Returns(validateTool);
        _toolRegistry.GetTool(Arg.Is<string>(s => s != "file.read" && s != "roslyn.validate_compilation"))
            .Returns((ToolDefinition?)null);

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = [],
            }));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert - Should have called GetTool for each tool in metadata
        foreach (var toolId in _agent.Metadata.Tools)
        {
            _toolRegistry.Received().GetTool(toolId);
        }

        // Should pass the found tools to ReActExecutor
        await _reactExecutor.Received(1).ExecuteAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ToolDefinition>>(tools =>
                tools.Any(t => t.ToolId == "file.read") &&
                tools.Any(t => t.ToolId == "roslyn.validate_compilation")),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordToolCallsInOutput()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Add a method");

        var result = new ReActResult
        {
            Success = true,
            FinalAnswer = "Done",
            Steps =
            [
                new ReActStep
                {
                    StepNumber = 1,
                    Thought = "Reading file",
                    Action = "file.read",
                    ActionInput = """{"path": "Test.cs"}""",
                    Observation = "public class Test { }",
                },
                new ReActStep
                {
                    StepNumber = 2,
                    Thought = "Validating",
                    Action = "roslyn.validate_compilation",
                    ActionInput = """{"projectName": "Test"}""",
                    Observation = """{"success": true}""",
                },
                new ReActStep
                {
                    StepNumber = 3,
                    Thought = "Done",
                    Action = "finish",
                    ActionInput = "Completed",
                    Observation = "Task completed",
                },
            ],
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert - Should have 2 tool calls (not the "finish" action)
        Assert.Equal(2, output.ToolCalls.Count);
        Assert.Equal("file.read", output.ToolCalls[0].ToolName);
        Assert.Equal("roslyn.validate_compilation", output.ToolCalls[1].ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeReasoningTraceInArtifacts()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Add a method");

        var result = new ReActResult
        {
            Success = true,
            FinalAnswer = "Done",
            Steps =
            [
                new ReActStep
                {
                    StepNumber = 1,
                    Thought = "First I need to understand the codebase",
                    Action = "file.read",
                    ActionInput = """{"path": "Test.cs"}""",
                    Observation = "public class Test { }",
                },
            ],
        };

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.Contains("reasoning_trace", output.Artifacts.Keys);
        var trace = output.Artifacts["reasoning_trace"];
        Assert.Contains("Step 1", trace);
        Assert.Contains("First I need to understand the codebase", trace);
        Assert.Contains("file.read", trace);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseMaxSteps15()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Complex refactoring task");

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = [],
            }));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        await _reactExecutor.Received(1).ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Is<ReActOptions>(o => o.MaxSteps == 15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisableUserConfirmation()
    {
        // Arrange - agent runs autonomously
        var context = AgentContext.FromPrompt("Add a method");

        _reactExecutor.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Any<ReActOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReActResult
            {
                Success = true,
                FinalAnswer = "Done",
                Steps = [],
            }));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert - RequireConfirmation should be false for autonomous execution
        await _reactExecutor.Received(1).ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ToolDefinition>>(),
            Arg.Any<ILlmProvider>(),
            Arg.Is<ReActOptions>(o => o.RequireConfirmation == false),
            Arg.Any<CancellationToken>());
    }
}
