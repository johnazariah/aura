// <copyright file="McpHandlerInspectTests.cs" company="Aura">
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

public class McpHandlerInspectTests
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

    public McpHandlerInspectTests()
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
    public async Task Inspect_TypeMembers_CallsGraphService()
    {
        // Arrange
        var memberNode = new CodeNode
        {
            NodeType = CodeNodeType.Method,
            Name = "DoWork",
            FullName = "TestNs.MyService.DoWork",
            FilePath = "src/MyService.cs",
            LineNumber = 15,
        };
        _graphService.GetTypeMembersAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode> { memberNode });

        var request = BuildToolCallRequest("aura_inspect", new { operation = "type_members", typeName = "MyService" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        await _graphService.Received(1).GetTypeMembersAsync("MyService", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inspect_TypeMembers_ReturnsEmptyArray_WhenNotFound()
    {
        // Arrange
        _graphService.GetTypeMembersAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CodeNode>());

        var request = BuildToolCallRequest("aura_inspect", new { operation = "type_members", typeName = "NonExistentType" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task Inspect_UnknownOperation_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_inspect", new { operation = "nonexistent" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("Unknown inspect operation");
    }

    [Fact]
    public async Task Inspect_MissingOperation_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_inspect", new { typeName = "Something" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("operation");
    }

    [Fact]
    public async Task Inspect_TypeMembers_ReturnsMultipleMembers()
    {
        // Arrange
        var members = new List<CodeNode>
        {
            new()
            {
                NodeType = CodeNodeType.Method,
                Name = "MethodA",
                FullName = "TestNs.MyService.MethodA",
                FilePath = "src/MyService.cs",
                LineNumber = 10,
            },
            new()
            {
                NodeType = CodeNodeType.Property,
                Name = "PropB",
                FullName = "TestNs.MyService.PropB",
                FilePath = "src/MyService.cs",
                LineNumber = 20,
            },
        };

        _graphService.GetTypeMembersAsync("MyService", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(members);

        var request = BuildToolCallRequest("aura_inspect", new { operation = "type_members", typeName = "MyService" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response!.Error.Should().BeNull();

        var resultJson = JsonSerializer.Serialize(response.Result);
        var resultDoc = JsonDocument.Parse(resultJson);
        var content = resultDoc.RootElement.GetProperty("content");
        var text = content[0].GetProperty("text").GetString()!;
        text.Should().Contain("MethodA");
        text.Should().Contain("PropB");
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
