// <copyright file="FallbackIngesterAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class FallbackIngesterAgentTests
{
    private readonly FallbackIngesterAgent _agent = new(NullLogger<FallbackIngesterAgent>.Instance);

    [Fact]
    public void AgentId_ShouldBeFallbackIngester()
    {
        Assert.Equal("fallback-ingester", _agent.AgentId);
    }

    [Fact]
    public void Metadata_ShouldHaveWildcardCapability()
    {
        Assert.Contains("ingest:*", _agent.Metadata.Capabilities);
    }

    [Fact]
    public void Metadata_ShouldHaveLowestPriority()
    {
        Assert.Equal(99, _agent.Metadata.Priority);
    }

    [Fact]
    public void Metadata_ShouldBeNativeProvider()
    {
        Assert.Equal("native", _agent.Metadata.Provider);
    }

    [Fact]
    public void Metadata_ShouldHaveFallbackTag()
    {
        Assert.Contains("fallback", _agent.Metadata.Tags);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSingleChunk()
    {
        // Arrange
        var content = """
            Some content here.
            Multiple lines of text.
            That we can't parse semantically.
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "unknown.xyz",
                ["content"] = content,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);
        Assert.NotNull(chunks);
        Assert.Single(chunks);
    }

    [Fact]
    public async Task ExecuteAsync_ChunkContainsWholeFile()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "test.unknown",
                ["content"] = content,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        Assert.NotNull(chunks);
        var chunk = chunks.Single();
        Assert.Equal(content, chunk.Text);
        Assert.Equal(ChunkTypes.File, chunk.ChunkType);
    }

    [Fact]
    public async Task ExecuteAsync_ChunkHasCorrectLineNumbers()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5";

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "test.xyz",
                ["content"] = content,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var chunk = chunks!.Single();
        Assert.Equal(1, chunk.StartLine);
        Assert.Equal(5, chunk.EndLine);
    }

    [Fact]
    public async Task ExecuteAsync_ChunkHasFilenameAsSymbol()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "/some/path/myfile.unknown",
                ["content"] = "content",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var chunk = chunks!.Single();
        Assert.Equal("myfile.unknown", chunk.SymbolName);
    }

    [Fact]
    public async Task ExecuteAsync_ChunkHasWarningMetadata()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "script.lisp",
                ["content"] = "(defun hello () (print 'hello))",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var chunk = chunks!.Single();
        Assert.True(chunk.Metadata.ContainsKey("warning"));
        Assert.Contains(".lisp", chunk.Metadata["warning"]);
    }

    [Fact]
    public async Task ExecuteAsync_OutputHasFallbackIndicator()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "test.xyz",
                ["content"] = "content",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(output.Artifacts.ContainsKey("fallback"));
        Assert.Equal("true", output.Artifacts["fallback"]);
    }

    [Fact]
    public async Task ExecuteAsync_ContentHasWarningEmoji()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "test.xyz",
                ["content"] = "content",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.Contains("⚠️", output.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyFile_ShouldStillWork()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "empty.xyz",
                ["content"] = string.Empty,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        Assert.NotNull(chunks);
        Assert.Single(chunks);
        Assert.Equal(string.Empty, chunks.Single().Text);
    }

    [Fact]
    public async Task ExecuteAsync_LanguageIsExtension()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "script.scm",
                ["content"] = "(define x 10)",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        var chunk = chunks!.Single();
        Assert.Equal("scm", chunk.Language);
    }
}
