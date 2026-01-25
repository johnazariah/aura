// <copyright file="AuraDocsToolTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Mcp.Tools;

using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Foundation.Rag;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class AuraDocsToolTests
{
    private readonly IRagService _ragService;
    private readonly AuraDocsTool _tool;

    public AuraDocsToolTests()
    {
        _ragService = Substitute.For<IRagService>();
        _tool = new AuraDocsTool(_ragService, NullLogger<AuraDocsTool>.Instance);
    }

    private static JsonDocument ConvertToJsonDocument(object result)
    {
        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json);
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "how to use agents";
        var expectedResults = new List<RagResult>
        {
            new("doc1", 0, "Agents are autonomous components...", 0.85)
            {
                SourcePath = "docs/agents.md",
                ContentType = RagContentType.Documentation,
                Metadata = new Dictionary<string, string> { ["category"] = "guides" }
            },
            new("doc1", 1, "To create an agent, define...", 0.75)
            {
                SourcePath = "docs/agents.md",
                ContentType = RagContentType.Documentation,
                Metadata = new Dictionary<string, string> { ["category"] = "guides" }
            }
        };

        _ragService
            .QueryAsync(
                query,
                Arg.Is<RagQueryOptions>(o =>
                    o.TopK == 10 &&
                    o.MinScore == 0.5 &&
                    o.ContentTypes != null &&
                    o.ContentTypes.Contains(RagContentType.Documentation) &&
                    o.ContentTypes.Contains(RagContentType.Markdown)),
                Arg.Any<CancellationToken>())
            .Returns(expectedResults);

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;

        root.GetProperty("query").GetString().Should().Be(query);
        root.GetProperty("resultCount").GetInt32().Should().Be(2);

        var results = root.GetProperty("results").EnumerateArray().ToList();
        results.Should().HaveCount(2);

        results[0].GetProperty("content").GetString().Should().Contain("Agents are autonomous");
        results[0].GetProperty("sourcePath").GetString().Should().Be("docs/agents.md");
        results[0].GetProperty("score").GetDouble().Should().Be(0.85);
        results[0].GetProperty("contentType").GetString().Should().Be("Documentation");
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithEmptyQuery_CallsRagServiceWithEmptyString()
    {
        // Arrange
        var query = string.Empty;
        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        await _ragService.Received(1).QueryAsync(
            string.Empty,
            Arg.Any<RagQueryOptions>(),
            Arg.Any<CancellationToken>());

        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        root.GetProperty("query").GetString().Should().BeEmpty();
        root.GetProperty("resultCount").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("agents")]
    [InlineData("workflow configuration")]
    [InlineData("RAG service usage")]
    public async Task SearchDocumentationAsync_WithVariousQueries_PassesQueryCorrectly(string query)
    {
        // Arrange
        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        await _ragService.Received(1).QueryAsync(
            query,
            Arg.Any<RagQueryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchDocumentationAsync_ConfiguresCorrectOptions()
    {
        // Arrange
        var query = "test query";
        RagQueryOptions? capturedOptions = null;

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Do<RagQueryOptions>(o => capturedOptions = o), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.TopK.Should().Be(10);
        capturedOptions.MinScore.Should().Be(0.5);
        capturedOptions.ContentTypes.Should().NotBeNull();
        capturedOptions.ContentTypes.Should().HaveCount(2);
        capturedOptions.ContentTypes.Should().Contain(RagContentType.Documentation);
        capturedOptions.ContentTypes.Should().Contain(RagContentType.Markdown);
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithNoResults_ReturnsEmptyResultSet()
    {
        // Arrange
        var query = "nonexistent topic";
        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        root.GetProperty("query").GetString().Should().Be(query);
        root.GetProperty("resultCount").GetInt32().Should().Be(0);
        root.GetProperty("results").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithHighScoreResults_IncludesAllResults()
    {
        // Arrange
        var query = "high relevance query";
        var expectedResults = new List<RagResult>
        {
            new("doc1", 0, "Perfect match content", 0.95)
            {
                SourcePath = "docs/perfect.md",
                ContentType = RagContentType.Documentation
            },
            new("doc2", 0, "Very good match", 0.88)
            {
                SourcePath = "docs/good.md",
                ContentType = RagContentType.Markdown
            },
            new("doc3", 0, "Good match", 0.75)
            {
                SourcePath = "docs/decent.md",
                ContentType = RagContentType.Documentation
            }
        };

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResults);

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        root.GetProperty("resultCount").GetInt32().Should().Be(3);

        var results = root.GetProperty("results").EnumerateArray().ToList();
        results.Should().HaveCount(3);
        results[0].GetProperty("score").GetDouble().Should().Be(0.95);
        results[1].GetProperty("score").GetDouble().Should().Be(0.88);
        results[2].GetProperty("score").GetDouble().Should().Be(0.75);
    }

    [Fact]
    public async Task SearchDocumentationAsync_IncludesMetadataInResults()
    {
        // Arrange
        var query = "metadata test";
        var metadata = new Dictionary<string, string>
        {
            ["author"] = "Aura Team",
            ["version"] = "1.0",
            ["tags"] = "getting-started,tutorial"
        };

        var expectedResults = new List<RagResult>
        {
            new("doc1", 0, "Content with metadata", 0.80)
            {
                SourcePath = "docs/tutorial.md",
                ContentType = RagContentType.Documentation,
                Metadata = metadata
            }
        };

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResults);

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        var results = root.GetProperty("results").EnumerateArray().ToList();
        var firstResult = results[0];

        var resultMetadata = firstResult.GetProperty("metadata");
        resultMetadata.GetProperty("author").GetString().Should().Be("Aura Team");
        resultMetadata.GetProperty("version").GetString().Should().Be("1.0");
        resultMetadata.GetProperty("tags").GetString().Should().Be("getting-started,tutorial");
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithNullMetadata_HandlesGracefully()
    {
        // Arrange
        var query = "no metadata";
        var expectedResults = new List<RagResult>
        {
            new("doc1", 0, "Content without metadata", 0.70)
            {
                SourcePath = "docs/simple.md",
                ContentType = RagContentType.Markdown,
                Metadata = null
            }
        };

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResults);

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        var results = root.GetProperty("results").EnumerateArray().ToList();
        var firstResult = results[0];

        firstResult.GetProperty("metadata").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SearchDocumentationAsync_RespectsCancellationToken()
    {
        // Arrange
        var query = "cancellation test";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RagResult>>>(_ => throw new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _tool.SearchDocumentationAsync(query, cts.Token));
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var query = "How to use C# async/await?";
        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        await _ragService.Received(1).QueryAsync(
            query,
            Arg.Any<RagQueryOptions>(),
            Arg.Any<CancellationToken>());

        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        root.GetProperty("query").GetString().Should().Be(query);
    }

    [Fact]
    public async Task SearchDocumentationAsync_WithVeryLongQuery_PassesThrough()
    {
        // Arrange
        var query = new string('a', 1000) + " " + new string('b', 1000);
        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(new List<RagResult>());

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        await _ragService.Received(1).QueryAsync(
            query,
            Arg.Any<RagQueryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchDocumentationAsync_WhenRagServiceThrows_PropagatesException()
    {
        // Arrange
        var query = "error test";
        _ragService
            .When(x => x.QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Database connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _tool.SearchDocumentationAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task SearchDocumentationAsync_ResultIncludesCorrectContentTypeString()
    {
        // Arrange
        var query = "content type test";
        var expectedResults = new List<RagResult>
        {
            new("doc1", 0, "Documentation content", 0.80)
            {
                SourcePath = "docs/doc.md",
                ContentType = RagContentType.Documentation
            },
            new("doc2", 0, "Markdown content", 0.75)
            {
                SourcePath = "docs/readme.md",
                ContentType = RagContentType.Markdown
            }
        };

        _ragService
            .QueryAsync(Arg.Any<string>(), Arg.Any<RagQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(expectedResults);

        // Act
        var result = await _tool.SearchDocumentationAsync(query, CancellationToken.None);

        // Assert
        using var doc = ConvertToJsonDocument(result);
        var root = doc.RootElement;
        var results = root.GetProperty("results").EnumerateArray().ToList();

        results[0].GetProperty("contentType").GetString().Should().Be("Documentation");
        results[1].GetProperty("contentType").GetString().Should().Be("Markdown");
    }
}
