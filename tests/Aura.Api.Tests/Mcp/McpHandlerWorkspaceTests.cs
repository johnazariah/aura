// <copyright file="McpHandlerWorkspaceTests.cs" company="Aura">
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
using NSubstitute.ExceptionExtensions;
using Xunit;

public class McpHandlerWorkspaceTests
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

    public McpHandlerWorkspaceTests()
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
    public async Task WorkspaceList_ReturnsWorkspaces()
    {
        // Arrange
        var workspaces = new List<RegisteredWorkspace>
        {
            new("ws-1", "/path/one", "alias1", new[] { "tag1" }),
            new("ws-2", "/path/two", null, Array.Empty<string>()),
        };
        _workspaceRegistryService.ListWorkspaces().Returns(workspaces);
        _workspaceRegistryService.GetDefaultWorkspace().Returns(workspaces[0]);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "list",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("count").GetInt32().Should().Be(2);
        content.GetProperty("default").GetString().Should().Be("ws-1");
    }

    [Fact]
    public async Task WorkspaceAdd_WithPath_AddsWorkspace()
    {
        // Arrange
        var workspace = new RegisteredWorkspace("ws-new", "/new/path", "myalias", Array.Empty<string>());
        _workspaceRegistryService.AddWorkspace("/new/path", "myalias", Arg.Any<IReadOnlyList<string>?>())
            .Returns(workspace);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "add",
            path = "/new/path",
            alias = "myalias",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("workspace").GetProperty("id").GetString().Should().Be("ws-new");
        content.GetProperty("workspace").GetProperty("path").GetString().Should().Be("/new/path");
    }

    [Fact]
    public async Task WorkspaceAdd_WhenDuplicate_ReturnsError()
    {
        // Arrange
        _workspaceRegistryService.AddWorkspace(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>())
            .Throws(new InvalidOperationException("Workspace already registered"));

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "add",
            path = "/existing/path",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeFalse();
        content.GetProperty("error").GetString().Should().Contain("already registered");
    }

    [Fact]
    public async Task WorkspaceRemove_ExistingWorkspace_ReturnsSuccess()
    {
        // Arrange
        _workspaceRegistryService.RemoveWorkspace("ws-1").Returns(true);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "remove",
            id = "ws-1",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("message").GetString().Should().Contain("removed");
    }

    [Fact]
    public async Task WorkspaceRemove_NonExistentWorkspace_ReturnsFalse()
    {
        // Arrange
        _workspaceRegistryService.RemoveWorkspace("ws-unknown").Returns(false);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "remove",
            id = "ws-unknown",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeFalse();
        content.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task WorkspaceSetDefault_ExistingWorkspace_ReturnsSuccess()
    {
        // Arrange
        _workspaceRegistryService.SetDefault("ws-1").Returns(true);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "set_default",
            id = "ws-1",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("message").GetString().Should().Contain("Default workspace set");
    }

    [Fact]
    public async Task WorkspaceSetDefault_NonExistentWorkspace_ReturnsFalse()
    {
        // Arrange
        _workspaceRegistryService.SetDefault("ws-unknown").Returns(false);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "set_default",
            id = "ws-unknown",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeFalse();
        content.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task WorkspaceInvalidateCache_ReturnsResult()
    {
        // Arrange
        _roslynService.InvalidateCache(Arg.Any<string>()).Returns(true);

        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "invalidate_cache",
            path = "C:\\some\\path",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("message").GetString().Should().Contain("invalidated");
    }

    [Fact]
    public async Task WorkspaceStatus_ReturnsPathInfo()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "status",
            path = "C:\\some\\path",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.TryGetProperty("path", out _).Should().BeTrue();
        content.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WorkspaceDetectWorktree_ReturnsWorktreeInfo()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_workspace", new
        {
            operation = "detect_worktree",
            path = "C:\\some\\nonexistent\\path",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.TryGetProperty("isGitRepository", out _).Should().BeTrue();
        content.TryGetProperty("isWorktree", out _).Should().BeTrue();
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
}
