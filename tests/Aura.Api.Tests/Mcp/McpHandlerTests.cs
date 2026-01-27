// <copyright file="McpHandlerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp;

using System.Text.Json;
using Aura.Api.Mcp;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
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
    private readonly IDocsService _docsService;
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
        _docsService = Substitute.For<IDocsService>();

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
            _docsService,
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
        toolNames.Should().HaveCount(16); // All registered tools including aura_docs, aura_docs_list, aura_docs_get
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

    [Fact]
    public async Task AuraDocsList_WithNoFilters_ReturnsAllDocuments()
    {
        // Arrange
        var allDocuments = new List<DocumentEntry>
        {
            new("quick-start", "Quick Start Guide", "Get started with Aura in 5 minutes", "guides", new List<string> { "beginner", "setup" }),
            new("architecture", "Architecture Overview", "Learn about Aura's architecture", "reference", new List<string> { "advanced", "architecture" }),
            new("api-reference", "API Reference", "Complete API documentation", "reference", new List<string> { "api", "reference" })
        };

        _docsService.ListDocuments(null, null).Returns(allDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 100,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new { }
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

        var textContent = content.GetProperty("text").GetString();
        textContent.Should().Contain("quick-start");
        textContent.Should().Contain("architecture");
        textContent.Should().Contain("api-reference");

        _docsService.Received(1).ListDocuments(null, null);
    }

    [Fact]
    public async Task AuraDocsList_WithCategoryFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var filteredDocuments = new List<DocumentEntry>
        {
            new("architecture", "Architecture Overview", "Learn about Aura's architecture", "reference", new List<string> { "advanced", "architecture" }),
            new("api-reference", "API Reference", "Complete API documentation", "reference", new List<string> { "api", "reference" })
        };

        _docsService.ListDocuments("reference", null).Returns(filteredDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 101,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new { category = "reference" }
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
        var textContent = content.GetProperty("text").GetString();

        textContent.Should().Contain("architecture");
        textContent.Should().Contain("api-reference");
        textContent.Should().NotContain("quick-start");

        _docsService.Received(1).ListDocuments("reference", null);
    }

    [Fact]
    public async Task AuraDocsList_WithTagsFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var filteredDocuments = new List<DocumentEntry>
        {
            new("quick-start", "Quick Start Guide", "Get started with Aura in 5 minutes", "guides", new List<string> { "beginner", "setup" }),
            new("installation", "Installation Guide", "Install Aura on your system", "guides", new List<string> { "beginner", "installation" })
        };

        _docsService.ListDocuments(
            null,
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("beginner")))
            .Returns(filteredDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 102,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new { tags = new[] { "beginner" } }
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
        var textContent = content.GetProperty("text").GetString();

        textContent.Should().Contain("quick-start");
        textContent.Should().Contain("installation");

        _docsService.Received(1).ListDocuments(
            null,
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("beginner")));
    }

    [Fact]
    public async Task AuraDocsList_WithCategoryAndTagsFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var filteredDocuments = new List<DocumentEntry>
        {
            new("quick-start", "Quick Start Guide", "Get started with Aura in 5 minutes", "guides", new List<string> { "beginner", "setup" })
        };

        _docsService.ListDocuments(
            "guides",
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("beginner")))
            .Returns(filteredDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 103,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new
                {
                    category = "guides",
                    tags = new[] { "beginner" }
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

        _docsService.Received(1).ListDocuments(
            "guides",
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("beginner")));
    }

    [Fact]
    public async Task AuraDocsList_WithEmptyTagsArray_FiltersEmpty()
    {
        // Arrange
        var allDocuments = new List<DocumentEntry>
        {
            new("quick-start", "Quick Start Guide", "Get started with Aura", "guides", new List<string> { "beginner" })
        };

        _docsService.ListDocuments(null, Arg.Is<IReadOnlyList<string>>(tags => tags.Count == 0))
            .Returns(allDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 104,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new { tags = Array.Empty<string>() }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
    }

    [Fact]
    public async Task AuraDocsGet_WithValidId_ReturnsDocumentContent()
    {
        // Arrange
        var documentContent = new DocumentContent(
            "quick-start",
            "Quick Start Guide",
            "guides",
            new List<string> { "beginner", "setup" },
            "# Quick Start\n\nWelcome to Aura...",
            "2024-01-27");

        _docsService.GetDocument("quick-start").Returns(documentContent);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 105,
            @params = new
            {
                name = "aura_docs_get",
                arguments = new { id = "quick-start" }
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

        var textContent = content.GetProperty("text").GetString();
        textContent.Should().Contain("quick-start");
        textContent.Should().Contain("Quick Start Guide");
        textContent.Should().Contain("# Quick Start");
        textContent.Should().Contain("Welcome to Aura");

        _docsService.Received(1).GetDocument("quick-start");
    }

    [Fact]
    public async Task AuraDocsGet_WithInvalidId_ReturnsError()
    {
        // Arrange
        _docsService.GetDocument("non-existent").Returns((DocumentContent?)null);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 106,
            @params = new
            {
                name = "aura_docs_get",
                arguments = new { id = "non-existent" }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("non-existent");
        response.Error.Message.Should().Contain("not found");

        _docsService.Received(1).GetDocument("non-existent");
    }

    [Fact]
    public async Task AuraDocsGet_WithMissingIdParameter_ReturnsError()
    {
        // Arrange
        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 107,
            @params = new
            {
                name = "aura_docs_get",
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
        response.Error!.Message.Should().ContainAny("key", "dictionary");

        _docsService.DidNotReceiveWithAnyArgs().GetDocument(Arg.Any<string>());
    }

    [Fact]
    public async Task AuraDocsGet_WithEmptyId_CallsServiceWithEmptyString()
    {
        // Arrange
        _docsService.GetDocument(string.Empty).Returns((DocumentContent?)null);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 108,
            @params = new
            {
                name = "aura_docs_get",
                arguments = new { id = string.Empty }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();

        _docsService.Received(1).GetDocument(string.Empty);
    }

    [Fact]
    public async Task AuraDocsList_WithMultipleTags_PassesAllTags()
    {
        // Arrange
        var filteredDocuments = new List<DocumentEntry>
        {
            new("doc1", "Document 1", "Summary 1", "guides", new List<string> { "tag1", "tag2" })
        };

        _docsService.ListDocuments(
            null,
            Arg.Is<IReadOnlyList<string>>(tags => tags.Count == 3 && tags.Contains("tag1") && tags.Contains("tag2") && tags.Contains("tag3")))
            .Returns(filteredDocuments);

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 109,
            @params = new
            {
                name = "aura_docs_list",
                arguments = new { tags = new[] { "tag1", "tag2", "tag3" } }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);

        // Act
        var responseJson = await _handler.HandleAsync(requestJson);

        // Assert
        var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        response.Should().NotBeNull();
        response!.Error.Should().BeNull();

        _docsService.Received(1).ListDocuments(
            null,
            Arg.Is<IReadOnlyList<string>>(tags => tags.Count == 3));
    }
}
