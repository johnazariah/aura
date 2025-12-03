// <copyright file="TextIngesterAgentTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class TextIngesterAgentTests
{
    private readonly TextIngesterAgent _agent = new(NullLogger<TextIngesterAgent>.Instance);

    [Fact]
    public void AgentId_ShouldBeTextIngester()
    {
        Assert.Equal("text-ingester", _agent.AgentId);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectCapabilities()
    {
        Assert.Contains("ingest:txt", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:md", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:rst", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:adoc", _agent.Metadata.Capabilities);
        Assert.Contains("ingest:log", _agent.Metadata.Capabilities);
    }

    [Fact]
    public void Metadata_ShouldHavePriority50()
    {
        Assert.Equal(50, _agent.Metadata.Priority);
    }

    [Fact]
    public void Metadata_ShouldBeNativeProvider()
    {
        Assert.Equal("native", _agent.Metadata.Provider);
    }

    [Fact]
    public async Task ExecuteAsync_WithMarkdown_ShouldChunkBySections()
    {
        // Arrange
        var markdown = """
            # Introduction

            This is the introduction section with some text.
            It spans multiple lines.

            ## Getting Started

            Here's how to get started with the project.

            ### Prerequisites

            You need to install the following:
            - .NET 8.0
            - Visual Studio Code

            ## Conclusion

            That's all for now!
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "README.md",
                ["content"] = markdown,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        Assert.NotNull(output);
        Assert.Contains("chunks", output.Artifacts.Keys);

        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);
        Assert.NotNull(chunks);
        Assert.True(chunks.Count >= 4, $"Expected at least 4 sections, got {chunks.Count}");

        // All chunks should be sections
        Assert.All(chunks, c => Assert.Equal(ChunkTypes.Section, c.ChunkType));

        // Check section names
        var sectionNames = chunks.Select(c => c.SymbolName).ToList();
        Assert.Contains("Introduction", sectionNames);
        Assert.Contains("Getting Started", sectionNames);
        Assert.Contains("Prerequisites", sectionNames);
        Assert.Contains("Conclusion", sectionNames);
    }

    [Fact]
    public async Task ExecuteAsync_WithPlainText_ShouldChunkByParagraphs()
    {
        // Arrange
        var text = """
            This is the first paragraph.
            It has multiple lines.

            This is the second paragraph.
            With more content here.

            And this is the third paragraph.
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "notes.txt",
                ["content"] = text,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);
        Assert.NotNull(chunks);
        Assert.Equal(3, chunks.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyContent_ShouldReturnEmptyChunks()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "empty.txt",
                ["content"] = string.Empty,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);

        // Assert
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);
        Assert.NotNull(chunks);
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ExecuteAsync_ChunksHaveCorrectFilePath()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "/path/to/document.md",
                ["content"] = "# Title\n\nSome content.",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        Assert.NotNull(chunks);
        Assert.All(chunks, c => Assert.Equal("/path/to/document.md", c.FilePath));
    }

    [Fact]
    public async Task ExecuteAsync_ChunksHaveCorrectLanguage()
    {
        // Arrange
        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "doc.txt",
                ["content"] = "Some content here.\n\nMore content.",
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        Assert.NotNull(chunks);
        Assert.All(chunks, c => Assert.Equal("text", c.Language));
    }

    [Fact]
    public async Task ExecuteAsync_MarkdownChunksHaveLineNumbers()
    {
        // Arrange
        var markdown = """
            # Header One

            Content line 1.

            # Header Two

            Content line 2.
            """;

        var context = new AgentContext(
            Prompt: "Parse this file",
            Properties: new Dictionary<string, object>
            {
                ["filePath"] = "test.md",
                ["content"] = markdown,
            });

        // Act
        var output = await _agent.ExecuteAsync(context);
        var chunks = JsonSerializer.Deserialize<List<SemanticChunk>>(output.Artifacts["chunks"]);

        // Assert
        Assert.NotNull(chunks);
        Assert.Equal(2, chunks.Count);

        // First section starts at line 1
        Assert.Equal(1, chunks[0].StartLine);

        // Second section starts after the first
        Assert.True(chunks[1].StartLine > chunks[0].StartLine);
    }
}
