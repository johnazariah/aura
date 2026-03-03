// <copyright file="McpHandlerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp;

using System.Text.Json;
using Aura.Api.Mcp;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class McpHandlerTests
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

    public McpHandlerTests()
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
    public async Task HandleAsync_WithInitializeMethod_Succeeds()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "initialize",
            id = 10,
            @params = new
            {
                protocolVersion = "2024-11-05",
                clientInfo = new
                {
                    name = "test-client",
                    version = "1.0.0"
                }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        var resultJson = JsonSerializer.Serialize(response.Result);
        var resultDoc = JsonDocument.Parse(resultJson);

        resultDoc.RootElement.GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");
        resultDoc.RootElement.GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("Aura");
    }

    [Fact]
    public async Task HandleAsync_WithUnknownMethod_ReturnsMethodNotFoundError()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "unknown/method",
            id = 11
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32601);
        response.Error.Message.Should().Contain("Method not found");
    }
}
