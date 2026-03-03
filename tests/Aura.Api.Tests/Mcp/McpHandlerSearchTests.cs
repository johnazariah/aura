// <copyright file="McpHandlerSearchTests.cs" company="Aura">
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

public class McpHandlerSearchTests
{
    private readonly IRagService _ragService;
    private readonly ICodeGraphService _graphService;
    private readonly IGitWorktreeService _worktreeService;
    private readonly McpHandler _handler;

    public McpHandlerSearchTests()
    {
        _ragService = Substitute.For<IRagService>();
        _graphService = Substitute.For<ICodeGraphService>();
        _worktreeService = Substitute.For<IGitWorktreeService>();

        _handler = new McpHandler(
            _ragService,
            _graphService,
            Substitute.For<IRoslynWorkspaceService>(),
            Substitute.For<IRoslynRefactoringService>(),
            Substitute.For<IPythonRefactoringService>(),
            Substitute.For<ITypeScriptLanguageService>(),
            Substitute.For<ITestGenerationService>(),
            _worktreeService,
            Substitute.For<ITreeBuilderService>(),
            Substitute.For<IWorkspaceRegistryService>(),
            Substitute.For<IBackgroundIndexer>(),
            NullLogger<McpHandler>.Instance);
    }

    [Fact]
    public async Task Search_WithQuery_ReturnsResults()
    {
        // Arrange
        _ragService.QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new RagResult("doc1", 0, "some matching text", 0.85)
                {
                    SourcePath = "/src/Foo.cs",
                    ContentType = RagContentType.Code,
                },
            });
        _graphService.FindNodesAsync(Arg.Any<string>(), Arg.Any<CodeNodeType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeNode>());

        var request = BuildToolCallRequest("aura_search", new { query = "UserService" });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var results = GetContentArray(responseJson);
        results.GetArrayLength().Should().BeGreaterThan(0);

        var first = results[0];
        first.GetProperty("content").GetString().Should().Be("some matching text");
        first.GetProperty("filePath").GetString().Should().Be("/src/Foo.cs");
        first.GetProperty("score").GetDouble().Should().BeApproximately(0.85, 0.001);
    }

    [Fact]
    public async Task Search_WithWorkspacePath_ResolvesWorktree()
    {
        // Arrange
        _worktreeService.GetMainRepositoryPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GitResult<string>.Ok("/main/repo"));
        _ragService.QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RagResult>());
        _graphService.FindNodesAsync(Arg.Any<string>(), Arg.Any<CodeNodeType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeNode>());

        var request = BuildToolCallRequest("aura_search", new
        {
            query = "SomeClass",
            workspacePath = "/worktree/path",
        });

        // Act
        await _handler.HandleAsync(request);

        // Assert - verify RAG query used resolved path as source prefix
        await _ragService.Received(1).QueryAsync(
            "SomeClass",
            Arg.Is<RagQueryOptions?>(o => o != null && o.SourcePathPrefix != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithContentTypeCode_PassesCorrectRagContentTypes()
    {
        // Arrange
        _ragService.QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RagResult>());
        _graphService.FindNodesAsync(Arg.Any<string>(), Arg.Any<CodeNodeType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeNode>());

        var request = BuildToolCallRequest("aura_search", new
        {
            query = "IRepository",
            contentType = "code",
        });

        // Act
        await _handler.HandleAsync(request);

        // Assert
        await _ragService.Received(1).QueryAsync(
            Arg.Any<string>(),
            Arg.Is<RagQueryOptions?>(o =>
                o != null &&
                o.ContentTypes != null &&
                o.ContentTypes.Count == 1 &&
                o.ContentTypes[0] == RagContentType.Code),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithLimitParameter_RespectsLimit()
    {
        // Arrange
        _ragService.QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RagResult>());
        _graphService.FindNodesAsync(Arg.Any<string>(), Arg.Any<CodeNodeType?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeNode>());

        var request = BuildToolCallRequest("aura_search", new
        {
            query = "SomeMethod",
            limit = 3,
        });

        // Act
        await _handler.HandleAsync(request);

        // Assert
        await _ragService.Received(1).QueryAsync(
            Arg.Any<string>(),
            Arg.Is<RagQueryOptions?>(o => o != null && o.TopK == 3),
            Arg.Any<CancellationToken>());
    }

    private static string BuildToolCallRequest(string toolName, object arguments)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = "test-1",
            method = "tools/call",
            @params = new { name = toolName, arguments },
        };
        return JsonSerializer.Serialize(request);
    }

    private static JsonElement GetContentText(string responseJson)
    {
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var resultText = response.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;
        return JsonSerializer.Deserialize<JsonElement>(resultText);
    }

    private static JsonElement GetContentArray(string responseJson)
    {
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var resultText = response.GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;
        return JsonSerializer.Deserialize<JsonElement>(resultText);
    }
}
