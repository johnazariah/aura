// <copyright file="RoslynCodingAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Agents;

using System.IO.Abstractions;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using Aura.Foundation.Prompts;
using Aura.Module.Developer.Agents;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class RoslynCodingAgentTests
{
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IRoslynWorkspaceService _workspaceService;
    private readonly ILlmProviderRegistry _llmRegistry;
    private readonly ILlmProvider _llmProvider;
    private readonly IFileSystem _fileSystem;
    private readonly RoslynCodingAgent _agent;

    public RoslynCodingAgentTests()
    {
        _refactoringService = Substitute.For<IRoslynRefactoringService>();
        _workspaceService = Substitute.For<IRoslynWorkspaceService>();
        _llmRegistry = Substitute.For<ILlmProviderRegistry>();
        _llmProvider = Substitute.For<ILlmProvider>();
        _fileSystem = Substitute.For<IFileSystem>();
        var promptRegistry = Substitute.For<IPromptRegistry>();

        _llmRegistry.GetDefaultProvider().Returns(_llmProvider);

        _agent = new RoslynCodingAgent(
            _refactoringService,
            _workspaceService,
            _llmRegistry,
            promptRegistry,
            _fileSystem,
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
        Assert.Contains("software-development-csharp", _agent.Metadata.Capabilities);
        Assert.Contains("software-development", _agent.Metadata.Capabilities);
        Assert.Contains("coding", _agent.Metadata.Capabilities);
    }

    [Fact]
    public void Metadata_ShouldHavePriority10()
    {
        Assert.Equal(10, _agent.Metadata.Priority);
    }

    [Fact]
    public void Metadata_ShouldHaveEmptyTools()
    {
        // Deterministic agent doesn't expose tools - it calls Roslyn directly
        Assert.Empty(_agent.Metadata.Tools);
    }

    [Fact]
    public void Metadata_ShouldBeCSharpLanguage()
    {
        Assert.Contains("csharp", _agent.Metadata.Languages);
    }

    [Fact]
    public void Metadata_ShouldHaveDeterministicTags()
    {
        Assert.Contains("deterministic", _agent.Metadata.Tags);
        Assert.Contains("roslyn", _agent.Metadata.Tags);
        Assert.Contains("csharp", _agent.Metadata.Tags);
    }

    private void SetupLlmMock(string jsonContent, int tokensUsed = 50)
    {
        _llmProvider.ChatAsync(
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new LlmResponse(jsonContent, tokensUsed)));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExtractOperationsFromLlmAndExecute()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace(
            "Add a CalculateSum method to Calculator class",
            @"C:\work\myproject");

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.sln", Arg.Any<SearchOption>())
            .Returns([]);
        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.csproj", Arg.Any<SearchOption>())
            .Returns([@"C:\work\myproject\MyProject.csproj"]);

        SetupLlmMock("""
            {
                "summary": "Adding CalculateSum method to Calculator",
                "operations": [
                    {
                        "operation": "add_method",
                        "className": "Calculator",
                        "methodName": "CalculateSum",
                        "returnType": "int",
                        "parameters": "int[] numbers",
                        "body": "return numbers.Sum();",
                        "reason": "Add the requested method"
                    }
                ]
            }
            """, 100);

        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Added method")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        // In test environment, validation may fail since workspace isn't real
        // but the operations should have been executed
        Assert.Equal("1", output.Artifacts["operations_count"]);
        Assert.Equal("1", output.Artifacts["success_count"]); // The add_method succeeded

        // Should have called Roslyn refactoring service directly
        await _refactoringService.Received(1).AddMethodAsync(
            Arg.Is<AddMethodRequest>(r =>
                r.ClassName == "Calculator" &&
                r.MethodName == "CalculateSum"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoLlmProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _llmRegistry.GetDefaultProvider().Returns((ILlmProvider?)null);
        var promptRegistry = Substitute.For<IPromptRegistry>();

        var agent = new RoslynCodingAgent(
            _refactoringService,
            _workspaceService,
            _llmRegistry,
            promptRegistry,
            _fileSystem,
            NullLogger<RoslynCodingAgent>.Instance);

        var context = AgentContext.FromPrompt("Do something");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ExecuteAsync(context));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordToolCallsForOperations()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace("Add a method", @"C:\work\myproject");

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.sln", Arg.Any<SearchOption>())
            .Returns([@"C:\work\myproject\Test.sln"]);

        SetupLlmMock("""
            {
                "summary": "Adding method",
                "operations": [
                    {
                        "operation": "add_method",
                        "className": "Test",
                        "methodName": "DoIt",
                        "returnType": "void",
                        "body": "Console.WriteLine(\"Hello\");"
                    }
                ]
            }
            """);

        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Done")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert - Should have 1 tool call for the operation
        Assert.Single(output.ToolCalls);
        Assert.Equal("roslyn.add_method", output.ToolCalls[0].ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_ShouldReportError()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace("Add a method", @"C:\work\myproject");

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.sln", Arg.Any<SearchOption>())
            .Returns([@"C:\work\myproject\Test.sln"]);

        SetupLlmMock("""
            {
                "summary": "Adding method",
                "operations": [
                    {
                        "operation": "add_method",
                        "className": "MissingClass",
                        "methodName": "DoIt",
                        "returnType": "void"
                    }
                ]
            }
            """);

        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Failed("Class not found")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.Equal("false", output.Artifacts["success"]);
        Assert.Contains("errors", output.Artifacts.Keys);
        Assert.Contains("Class not found", output.Artifacts["errors"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleMalformedJsonFromLlm()
    {
        // Arrange
        var context = AgentContext.FromPrompt("Add a method");

        SetupLlmMock("This is not valid JSON at all");

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert - Should handle gracefully with no operations
        Assert.Equal("true", output.Artifacts["success"]); // No operations = no failures
        Assert.Equal("0", output.Artifacts["operations_count"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleJsonInCodeBlock()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace("Add a method", @"C:\work\myproject");

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.sln", Arg.Any<SearchOption>())
            .Returns([@"C:\work\myproject\Test.sln"]);

        SetupLlmMock("""
            Here's the plan:
            
            ```json
            {
                "summary": "Adding method",
                "operations": [
                    {
                        "operation": "add_method",
                        "className": "Test",
                        "methodName": "DoIt",
                        "returnType": "void",
                        "body": "// Implementation"
                    }
                ]
            }
            ```
            """);

        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Done")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert - Should extract JSON from code block
        Assert.Equal("1", output.Artifacts["operations_count"]);

        await _refactoringService.Received(1).AddMethodAsync(
            Arg.Is<AddMethodRequest>(r => r.MethodName == "DoIt"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFindCsprojWhenNoSlnExists()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace("Add a method", @"C:\work\myproject");

        // No .sln files
        _fileSystem.Directory.GetFiles(@"C:\work\myproject", "*.sln", SearchOption.TopDirectoryOnly)
            .Returns([]);

        // But there is a .csproj
        _fileSystem.Directory.GetFiles(@"C:\work\myproject", "*.csproj", SearchOption.AllDirectories)
            .Returns([@"C:\work\myproject\MyProject.csproj"]);

        SetupLlmMock("""{"summary": "test", "operations": [{"operation": "add_method", "className": "Test", "methodName": "M", "returnType": "void"}]}""");

        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Done")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert - Should use .csproj path
        await _refactoringService.Received(1).AddMethodAsync(
            Arg.Is<AddMethodRequest>(r => r.SolutionPath == @"C:\work\myproject\MyProject.csproj"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteMultipleOperationsSequentially()
    {
        // Arrange
        var context = AgentContext.FromPromptAndWorkspace("Add property and method", @"C:\work\myproject");

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.sln", Arg.Any<SearchOption>())
            .Returns([@"C:\work\myproject\Test.sln"]);

        SetupLlmMock("""
            {
                "summary": "Adding property and method",
                "operations": [
                    {"operation": "add_property", "className": "Test", "propertyName": "Name", "propertyType": "string"},
                    {"operation": "add_method", "className": "Test", "methodName": "GetName", "returnType": "string", "body": "return Name;"}
                ]
            }
            """);

        _refactoringService.AddPropertyAsync(Arg.Any<AddPropertyRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Done")));
        _refactoringService.AddMethodAsync(Arg.Any<AddMethodRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RefactoringResult.Succeeded("Done")));

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.Equal("2", output.Artifacts["operations_count"]);
        Assert.Equal(2, output.ToolCalls.Count);
        Assert.Equal("roslyn.add_property", output.ToolCalls[0].ToolName);
        Assert.Equal("roslyn.add_method", output.ToolCalls[1].ToolName);
    }
}
