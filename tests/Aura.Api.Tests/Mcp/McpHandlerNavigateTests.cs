// <copyright file="McpHandlerNavigateTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp;

using System.Text.Json;
using Aura.Api.Mcp;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class McpHandlerNavigateTests
{
    private readonly IRagService _ragService;
    private readonly ICodeGraphService _graphService;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IPythonRefactoringService _pythonRefactoringService;
    private readonly ITypeScriptLanguageService _typeScriptService;
    private readonly ITestGenerationService _testGenerationService;
    private readonly IGitWorktreeService _worktreeService;
    private readonly ITreeBuilderService _treeBuilderService;
    private readonly IWorkspaceRegistryService _workspaceRegistryService;
    private readonly IBackgroundIndexer _backgroundIndexer;
    private readonly McpHandler _handler;

    public McpHandlerNavigateTests()
    {
        _ragService = Substitute.For<IRagService>();
        _graphService = Substitute.For<ICodeGraphService>();
        _roslynService = Substitute.For<IRoslynWorkspaceService>();
        _refactoringService = Substitute.For<IRoslynRefactoringService>();
        _pythonRefactoringService = Substitute.For<IPythonRefactoringService>();
        _typeScriptService = Substitute.For<ITypeScriptLanguageService>();
        _testGenerationService = Substitute.For<ITestGenerationService>();
        _worktreeService = Substitute.For<IGitWorktreeService>();
        _treeBuilderService = Substitute.For<ITreeBuilderService>();
        _workspaceRegistryService = Substitute.For<IWorkspaceRegistryService>();
        _backgroundIndexer = Substitute.For<IBackgroundIndexer>();

        _handler = new McpHandler(
            _ragService,
            _graphService,
            _roslynService,
            _refactoringService,
            _pythonRefactoringService,
            _typeScriptService,
            _testGenerationService,
            _worktreeService,
            _treeBuilderService,
            _workspaceRegistryService,
            _backgroundIndexer,
            NullLogger<McpHandler>.Instance);
    }

    [Fact]
    public async Task Navigate_Callers_CallsGraphService()
    {
        // Arrange
        var callerNode = new CodeNode
        {
            NodeType = CodeNodeType.Method,
            Name = "CallerMethod",
            FullName = "TestNs.TestClass.CallerMethod",
            FilePath = "src/Test.cs",
            LineNumber = 10,
        };
        _graphService.FindCallersAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode> { callerNode });

        var request = BuildToolCallRequest("aura_navigate", new { operation = "callers", methodName = "MyMethod" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        await _graphService.Received(1).FindCallersAsync("MyMethod", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Navigate_Implementations_CallsGraphService()
    {
        // Arrange
        var implNode = new CodeNode
        {
            NodeType = CodeNodeType.Class,
            Name = "MyImpl",
            FullName = "TestNs.MyImpl",
            FilePath = "src/MyImpl.cs",
            LineNumber = 5,
        };
        _graphService.FindImplementationsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode> { implNode });

        var request = BuildToolCallRequest("aura_navigate", new { operation = "implementations", symbolName = "IMyInterface" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        await _graphService.Received(1).FindImplementationsAsync("IMyInterface", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Navigate_DerivedTypes_CallsGraphService()
    {
        // Arrange
        _graphService.FindDerivedTypesAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode>());

        var request = BuildToolCallRequest("aura_navigate", new { operation = "derived_types", symbolName = "BaseClass" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();

        await _graphService.Received(1).FindDerivedTypesAsync("BaseClass", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Navigate_UnknownOperation_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_navigate", new { operation = "nonexistent" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("Unknown navigate operation");
    }

    [Fact]
    public async Task Navigate_MissingOperation_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_navigate", new { symbolName = "Something" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("operation");
    }

    [Fact]
    public async Task Navigate_Definition_WithMissingSymbolName_ReturnsNotFound()
    {
        // Arrange
        _graphService.FindNodesAsync(Arg.Any<string>(), Arg.Any<CodeNodeType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode>());

        var request = BuildToolCallRequest("aura_navigate", new { operation = "definition" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();

        var resultJson = JsonSerializer.Serialize(response.Result);
        resultJson.Should().Contain("error");
        resultJson.Should().Contain("symbolName is required");
    }

    private static string BuildToolCallRequest(string toolName, object arguments)
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 1,
            @params = new
            {
                name = toolName,
                arguments,
            },
        };
        return JsonSerializer.Serialize(request);
    }
}
