// <copyright file="McpHandlerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp;

using System.Text.Json;
using Aura.Api.Mcp;
using Aura.Api.Mcp.Tools;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
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
    private readonly IStoryService _workflowService;
    private readonly IGitHubService _gitHubService;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IPythonRefactoringService _pythonRefactoringService;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly ITestGenerationService _testGenerationService;
    private readonly IGitWorktreeService _worktreeService;
    private readonly ITreeBuilderService _treeBuilderService;
    private readonly IAuraDocsTool _auraDocsTool;
    private readonly McpHandler _handler;

    public McpHandlerTests()
    {
        _ragService = Substitute.For<IRagService>();
        _graphService = Substitute.For<ICodeGraphService>();
        _workflowService = Substitute.For<IStoryService>();
        _gitHubService = Substitute.For<IGitHubService>();
        _roslynService = Substitute.For<IRoslynWorkspaceService>();
        _refactoringService = Substitute.For<IRoslynRefactoringService>();
        _pythonRefactoringService = Substitute.For<IPythonRefactoringService>();
        _typeScriptRefactoringService = Substitute.For<ITypeScriptRefactoringService>();
        _testGenerationService = Substitute.For<ITestGenerationService>();
        _worktreeService = Substitute.For<IGitWorktreeService>();
        _treeBuilderService = Substitute.For<ITreeBuilderService>();
        _auraDocsTool = Substitute.For<IAuraDocsTool>();

        _handler = new McpHandler(
            _ragService,
            _graphService,
            _workflowService,
            _gitHubService,
            _roslynService,
            _refactoringService,
            _pythonRefactoringService,
            _typeScriptRefactoringService,
            _testGenerationService,
            _worktreeService,
            _treeBuilderService,
            _auraDocsTool,
            NullLogger<McpHandler>.Instance);
    }

    [Fact]
    public void GetToolNames_IncludesAuraDocs()
    {
        // Act
        var toolNames = _handler.GetToolNames();

        // Assert
        toolNames.Should().Contain("aura_docs");
    }

    [Fact]
    public async Task AuraDocs_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "how to use agents";
        var expectedResults = new
        {
            results = new[]
            {
                new
                {
                    content = "Agents are autonomous components...",
                    sourcePath = "docs/agents.md",
                    chunkIndex = 0,
                    score = 0.85,
                    contentType = "Documentation",
                    metadata = new Dictionary<string, string> { ["category"] = "guides" }
                }
            },
            totalResults = 1,
            query
        };

        _auraDocsTool.SearchDocumentationAsync(query, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object>(expectedResults));

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 1,
            @params = new
            {
                name = "aura_docs",
                arguments = new { query }
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
        var root = resultDoc.RootElement;

        root.GetProperty("content").EnumerateArray().Should().HaveCount(1);
        var content = root.GetProperty("content")[0];
        content.GetProperty("type").GetString().Should().Be("text");
        content.GetProperty("text").GetString().Should().Contain("agents");

        await _auraDocsTool.Received(1).SearchDocumentationAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithEmptyQuery_CallsToolWithEmptyString()
    {
        // Arrange
        var query = string.Empty;
        var expectedResults = new
        {
            results = Array.Empty<object>(),
            totalResults = 0,
            query
        };

        _auraDocsTool.SearchDocumentationAsync(query, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object>(expectedResults));

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 2,
            @params = new
            {
                name = "aura_docs",
                arguments = new { query }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().BeNull();

        await _auraDocsTool.Received(1).SearchDocumentationAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithMissingQueryParameter_ReturnsError()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 3,
            @params = new
            {
                name = "aura_docs",
                arguments = new { }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        // The error message comes from JsonElement.GetProperty throwing KeyNotFoundException
        response.Error!.Message.Should().ContainAny("key", "query");

        await _auraDocsTool.DidNotReceiveWithAnyArgs().SearchDocumentationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithNullArguments_ReturnsError()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 4,
            @params = new
            {
                name = "aura_docs",
                arguments = (object?)null
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();

        await _auraDocsTool.DidNotReceiveWithAnyArgs().SearchDocumentationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithMalformedJson_ReturnsParseError()
    {
        // Arrange
        var malformedJson = "{invalid json}";

        // Act
        var responseJson = await _handler.HandleAsync(malformedJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32700);
        response.Error.Message.Should().Contain("Parse error");

        await _auraDocsTool.DidNotReceiveWithAnyArgs().SearchDocumentationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithMultipleResults_ReturnsAllResults()
    {
        // Arrange
        var query = "authentication methods";
        var expectedResults = new
        {
            results = new[]
            {
                new
                {
                    content = "JWT authentication is supported...",
                    sourcePath = "docs/auth.md",
                    chunkIndex = 0,
                    score = 0.92,
                    contentType = "Documentation"
                },
                new
                {
                    content = "OAuth2 flow can be configured...",
                    sourcePath = "docs/oauth.md",
                    chunkIndex = 0,
                    score = 0.88,
                    contentType = "Documentation"
                },
                new
                {
                    content = "API keys are managed through...",
                    sourcePath = "docs/api-keys.md",
                    chunkIndex = 0,
                    score = 0.75,
                    contentType = "Documentation"
                }
            },
            totalResults = 3,
            query
        };

        _auraDocsTool.SearchDocumentationAsync(query, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object>(expectedResults));

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 5,
            @params = new
            {
                name = "aura_docs",
                arguments = new { query }
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
        var root = resultDoc.RootElement;

        root.GetProperty("content").EnumerateArray().Should().HaveCount(1);

        await _auraDocsTool.Received(1).SearchDocumentationAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var query = "C# async/await & Task<T>";
        var expectedResults = new
        {
            results = new[]
            {
                new
                {
                    content = "Async/await pattern with Task<T>...",
                    sourcePath = "docs/csharp.md",
                    chunkIndex = 0,
                    score = 0.89,
                    contentType = "Documentation"
                }
            },
            totalResults = 1,
            query
        };

        _auraDocsTool.SearchDocumentationAsync(query, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object>(expectedResults));

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 6,
            @params = new
            {
                name = "aura_docs",
                arguments = new { query }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().BeNull();

        await _auraDocsTool.Received(1).SearchDocumentationAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuraDocs_WhenToolThrowsException_ReturnsError()
    {
        // Arrange
        var query = "test query";
        _auraDocsTool.SearchDocumentationAsync(query, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<object>(new InvalidOperationException("RAG service unavailable")));

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 7,
            @params = new
            {
                name = "aura_docs",
                arguments = new { query }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("RAG service unavailable");
    }

    [Fact]
    public void AuraDocs_IsRegisteredInToolsDictionary()
    {
        // Act
        var toolNames = _handler.GetToolNames();

        // Assert
        toolNames.Should().Contain("aura_docs");
        toolNames.Should().HaveCount(14); // All registered tools including aura_docs
    }

    [Fact]
    public async Task HandleAsync_WithToolsListMethod_IncludesAuraDocs()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = 9
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

        var tools = resultDoc.RootElement.GetProperty("tools").EnumerateArray();
        var toolNames = tools.Select(t => t.GetProperty("name").GetString()).ToList();

        toolNames.Should().Contain("aura_docs");
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
