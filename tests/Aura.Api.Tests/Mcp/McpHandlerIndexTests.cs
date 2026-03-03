// <copyright file="McpHandlerIndexTests.cs" company="Aura">
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

public class McpHandlerIndexTests : IDisposable
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
    private readonly string _tempDir;

    public McpHandlerIndexTests()
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

        _tempDir = Path.Combine(Path.GetTempPath(), $"aura-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task IndexDirectory_WithValidPath_QueuesDirectory()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _backgroundIndexer
            .QueueDirectory(Arg.Any<string>(), Arg.Any<RagIndexOptions?>())
            .Returns((jobId, true));

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "index_directory",
            path = _tempDir,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("jobId").GetString().Should().Be(jobId.ToString());
        content.GetProperty("isNewJob").GetBoolean().Should().BeTrue();
        content.GetProperty("path").GetString().Should().Be(_tempDir);

        _backgroundIndexer.Received(1)
            .QueueDirectory(_tempDir, Arg.Any<RagIndexOptions?>());
    }

    [Fact]
    public async Task IndexDirectory_WithMissingPath_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "index_directory",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("error").GetString()
            .Should().Contain("path is required");
    }

    [Fact]
    public async Task IndexDirectory_WithNonExistentPath_ReturnsError()
    {
        // Arrange
        var fakePath = Path.Combine(_tempDir, "does-not-exist-" + Guid.NewGuid().ToString("N"));
        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "index_directory",
            path = fakePath,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("error").GetString()
            .Should().Contain("Directory not found");
    }

    [Fact]
    public async Task IndexFile_WithValidPath_QueuesContent()
    {
        // Arrange
        var tempFile = Path.Combine(_tempDir, "test.cs");
        await File.WriteAllTextAsync(tempFile, "public class Foo {}");

        _backgroundIndexer.QueueContent(Arg.Any<RagContent>()).Returns(true);

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "index_file",
            path = tempFile,
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("path").GetString().Should().Be(tempFile);
        content.GetProperty("message").GetString().Should().Contain("queued");

        _backgroundIndexer.Received(1).QueueContent(Arg.Any<RagContent>());
    }

    [Fact]
    public async Task IndexFile_WithMissingPath_ReturnsError()
    {
        // Arrange
        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "index_file",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("error").GetString()
            .Should().Contain("path is required");
    }

    [Fact]
    public async Task Status_WithNoJobId_ReturnsActiveJobsList()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _backgroundIndexer.GetActiveJobs().Returns(new[]
        {
            new IndexJobStatus
            {
                JobId = jobId,
                Source = "/some/path",
                State = IndexJobState.Processing,
                TotalItems = 10,
                ProcessedItems = 5,
            },
        });
        _backgroundIndexer.GetStatus().Returns(new BackgroundIndexerStatus
        {
            QueuedItems = 2,
            ProcessedItems = 10,
            IsProcessing = true,
            ActiveJobs = 1,
        });

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "status",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("isProcessing").GetBoolean().Should().BeTrue();
        content.GetProperty("queuedItems").GetInt32().Should().Be(2);
        content.GetProperty("processedItems").GetInt32().Should().Be(10);
        content.GetProperty("activeJobs").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Status_WithSpecificJobId_ReturnsJobStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _backgroundIndexer.GetJobStatus(jobId).Returns(
            new IndexJobStatus
            {
                JobId = jobId,
                Source = "/some/path",
                State = IndexJobState.Completed,
                TotalItems = 10,
                ProcessedItems = 10,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            });

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "status",
            jobId = jobId.ToString(),
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("jobId").GetString().Should().Be(jobId.ToString());
        content.GetProperty("state").GetString().Should().Be("completed");
        content.GetProperty("source").GetString().Should().Be("/some/path");
    }

    [Fact]
    public async Task Stats_WithNoPath_ReturnsGlobalStats()
    {
        // Arrange
        _ragService.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new RagStats(100, 20, 51200));
        _ragService.IsHealthyAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "stats",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("totalChunks").GetInt32().Should().Be(100);
        content.GetProperty("totalDocuments").GetInt32().Should().Be(20);
        content.GetProperty("indexSizeBytes").GetInt64().Should().Be(51200);
        content.GetProperty("isHealthy").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Stats_WithPath_ReturnsDirectoryStats()
    {
        // Arrange
        _ragService.GetDirectoryStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RagDirectoryStats("/my/path", 50, 10, DateTime.UtcNow));

        var request = BuildToolCallRequest("aura_index", new
        {
            operation = "stats",
            path = "/my/path",
        });

        // Act
        var responseJson = await _handler.HandleAsync(request);

        // Assert
        var content = GetContentText(responseJson);
        content.GetProperty("isIndexed").GetBoolean().Should().BeTrue();
        content.GetProperty("chunkCount").GetInt32().Should().Be(50);
        content.GetProperty("fileCount").GetInt32().Should().Be(10);
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
