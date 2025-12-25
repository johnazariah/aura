// <copyright file="TextChunkerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Rag;
using FluentAssertions;
using Xunit;

public class TextChunkerTests
{
    private readonly TextChunker _chunker;

    public TextChunkerTests()
    {
        _chunker = new TextChunker(chunkSize: 100, chunkOverlap: 20);
    }

    [Fact]
    public void Split_EmptyText_ReturnsEmptyList()
    {
        var chunks = _chunker.Split("");
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Split_WhitespaceOnly_ReturnsEmptyList()
    {
        var chunks = _chunker.Split("   \n\t  ");
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        var text = "Hello, world!";
        var chunks = _chunker.Split(text);
        chunks.Should().HaveCount(1);
        chunks[0].Should().Be(text);
    }

    [Fact]
    public void Split_PlainText_SplitsOnParagraphs()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i =>
            "This is paragraph " + i + " with some content."));

        var chunks = _chunker.Split(text, RagContentType.PlainText);

        chunks.Should().HaveCountGreaterThan(1);
        var allContent = string.Join(" ", chunks);
        allContent.Should().Contain("paragraph 1");
        allContent.Should().Contain("paragraph 10");
    }

    [Fact]
    public void Split_Markdown_PreservesHeaders()
    {
        var text = @"# Introduction
This is the intro.

## Section One
Content for section one goes here.

## Section Two
Content for section two goes here.";

        var chunks = _chunker.Split(text, RagContentType.Markdown);

        chunks.Should().NotBeEmpty();
        chunks.Any(c => c.Contains("Introduction") || c.Contains("Section")).Should().BeTrue();
    }

    [Fact]
    public void Split_Code_PreservesAllContent()
    {
        var code = @"public class Example
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        var chunks = _chunker.Split(code, RagContentType.Code);

        chunks.Should().NotBeEmpty();
        var allContent = string.Concat(chunks);
        allContent.Should().Contain("public class Example");
        allContent.Should().Contain("Console.WriteLine");
    }

    [Fact]
    public void Split_Code_HasOverlapForContext()
    {
        var code = @"public class First
{
    public void Method1() { }
}

public class Second
{
    public void Method2() { }
}

public class Third
{
    public void Method3() { }
}";

        var chunker = new TextChunker(chunkSize: 50, chunkOverlap: 10);
        var chunks = chunker.Split(code, RagContentType.Code);

        // Should produce multiple chunks
        chunks.Should().HaveCountGreaterThan(1);

        // All content should be preserved across chunks
        var allContent = string.Concat(chunks);
        allContent.Should().Contain("First");
        allContent.Should().Contain("Second");
        allContent.Should().Contain("Third");
    }

    [Fact]
    public void Split_PlainText_NoBreaks_ReturnsWholeText()
    {
        var text = new string('a', 200);
        var chunks = _chunker.Split(text, RagContentType.PlainText);
        chunks.Should().HaveCount(1);
    }

    [Fact]
    public void Split_PlainText_WithParagraphBreaks_SplitsCorrectly()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 20).Select(i =>
            new string((char)('a' + (i % 26)), 30)));

        var chunks = _chunker.Split(text, RagContentType.PlainText);
        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Split_DefaultContentType_TreatsAsPlainText()
    {
        var text = "Some plain text content.";
        var chunks = _chunker.Split(text);
        chunks.Should().HaveCount(1);
        chunks[0].Should().Be(text);
    }

    [Fact]
    public void Constructor_WithDefaults_Works()
    {
        var chunker = new TextChunker();
        var result = chunker.Split("test");
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Split_Markdown_LargeDocument_SplitsOnHeaders()
    {
        var text = @"# Chapter 1
" + new string('x', 150) + @"

# Chapter 2
" + new string('y', 150) + @"

# Chapter 3
" + new string('z', 150);

        var chunks = _chunker.Split(text, RagContentType.Markdown);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Any(c => c.Contains("Chapter 1")).Should().BeTrue();
    }
}
