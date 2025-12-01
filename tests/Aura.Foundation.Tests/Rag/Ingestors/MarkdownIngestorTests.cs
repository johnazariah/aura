// <copyright file="MarkdownIngestorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag.Ingestors;

using Aura.Foundation.Rag.Ingestors;
using FluentAssertions;
using Xunit;

public class MarkdownIngestorTests
{
    private readonly MarkdownIngestor _sut = new();

    [Theory]
    [InlineData(".md", true)]
    [InlineData(".markdown", true)]
    [InlineData(".mdx", true)]
    [InlineData(".txt", false)]
    [InlineData(".cs", false)]
    public void CanIngest_ReturnsCorrectResult(string extension, bool expected)
    {
        // Arrange
        var filePath = $"test{extension}";

        // Act
        var result = _sut.CanIngest(filePath);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IngestAsync_SimpleDocument_ReturnsOneChunk()
    {
        // Arrange
        var content = "This is a simple document without headers.";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().HaveCount(1);
        // Simple content without headers gets chunk type "section" (no header) or "document" 
        chunks[0].ChunkType.Should().BeOneOf("section", "document");
        chunks[0].Text.Should().Contain("simple document");
    }

    [Fact]
    public async Task IngestAsync_WithHeaders_SplitsBySection()
    {
        // Arrange
        var content = @"# Introduction

This is the introduction.

## Getting Started

This is how to get started.

## Configuration

Configure the settings here.";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().HaveCountGreaterThanOrEqualTo(3);
        chunks.Should().Contain(c => c.Title == "Introduction");
        chunks.Should().Contain(c => c.Title == "Getting Started");
        chunks.Should().Contain(c => c.Title == "Configuration");
    }

    [Fact]
    public async Task IngestAsync_WithCodeBlock_ExtractsCodeSeparately()
    {
        // Arrange
        var content = @"# Code Example

Here's some code:

```csharp
public class HelloWorld
{
    public static void Main()
    {
        Console.WriteLine(""Hello, World!"");
    }
}
```

That was the code.";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().Contain(c => c.ChunkType == "code-block");
        var codeChunk = chunks.First(c => c.ChunkType == "code-block");
        codeChunk.Language.Should().Be("csharp");
        codeChunk.Text.Should().Contain("HelloWorld");
    }

    [Fact]
    public async Task IngestAsync_TrackLineNumbers()
    {
        // Arrange
        var content = @"# Section 1

Content for section 1.

# Section 2

Content for section 2.";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().AllSatisfy(c =>
        {
            c.StartLine.Should().BePositive();
            c.EndLine.Should().BePositive();
            c.EndLine.Should().BeGreaterThanOrEqualTo(c.StartLine!.Value);
        });
    }

    [Fact]
    public async Task IngestAsync_EmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var content = "";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestAsync_WhitespaceOnly_ReturnsEmptyList()
    {
        // Arrange
        var content = "   \n   \n   ";

        // Act
        var chunks = await _sut.IngestAsync("test.md", content);

        // Assert
        chunks.Should().BeEmpty();
    }
}
