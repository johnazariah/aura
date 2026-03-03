// <copyright file="PdfToMarkdownServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Tests.Services;

using Aura.Module.Researcher.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class PdfToMarkdownServiceTests
{
    private readonly IPdfExtractor _pdfExtractor;
    private readonly PdfToMarkdownService _service;

    public PdfToMarkdownServiceTests()
    {
        _pdfExtractor = Substitute.For<IPdfExtractor>();
        _service = new PdfToMarkdownService(
            _pdfExtractor,
            NullLogger<PdfToMarkdownService>.Instance);
    }

    [Fact]
    public async Task ConvertAsync_WithSimpleText_ProducesMarkdown()
    {
        // Arrange
        var rawContent = new RawPdfContent(
            Text: "A Sample Paper Title\n\nThis is the body of the paper.",
            PageCount: 1,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNullOrEmpty();
        result.Content.Should().Contain("---");
        result.Content.Should().Contain("title:");
    }

    [Fact]
    public async Task ConvertAsync_ExtractsTitleFromFirstLine()
    {
        // Arrange
        var rawContent = new RawPdfContent(
            Text: "Deep Learning for Natural Language Processing\n\nSome body text here.",
            PageCount: 1,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Title.Should().Be("Deep Learning for Natural Language Processing");
    }

    [Fact]
    public async Task ConvertAsync_ExtractsTitleFromMetadata_WhenAvailable()
    {
        // Arrange
        var rawContent = new RawPdfContent(
            Text: "Short\n\nBody text.",
            PageCount: 1,
            Metadata: new Dictionary<string, string> { ["Title"] = "Metadata Title Value" });

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Title.Should().Be("Metadata Title Value");
    }

    [Fact]
    public async Task ConvertAsync_DetectsSectionHeadings()
    {
        // Arrange
        var text = "A Paper Title That Is Long Enough\n\n" +
                   "1 Introduction\n" +
                   "This is the introduction.\n\n" +
                   "2 Related Work\n" +
                   "Some related work.\n\n" +
                   "3.1 Sub Section Details\n" +
                   "Sub section body.\n";

        var rawContent = new RawPdfContent(
            Text: text,
            PageCount: 2,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Sections.Should().NotBeEmpty();
        result.Sections.Should().Contain(s => s.Title == "Introduction");
        result.Sections.Should().Contain(s => s.Title == "Related Work");
    }

    [Fact]
    public async Task ConvertAsync_WithEmptyText_ReturnsUntitledDocument()
    {
        // Arrange
        var rawContent = new RawPdfContent(
            Text: "",
            PageCount: 0,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Untitled");
        result.Content.Should().NotBeNull();
    }

    [Fact]
    public async Task ConvertAsync_ExtractsCitations_WhenEnabled()
    {
        // Arrange
        var text = "A Paper Title That Is Long Enough\n\n" +
                   "Body text.\n\n" +
                   "References\n" +
                   "[1] Smith et al. A great paper. 2021.\n" +
                   "[2] Jones et al. Another paper. 10.1234/test.2022\n";

        var rawContent = new RawPdfContent(
            Text: text,
            PageCount: 1,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        var options = new PdfConversionOptions { ExtractCitations = true };

        // Act
        var result = await _service.ConvertAsync("test.pdf", options);

        // Assert
        result.Citations.Should().NotBeEmpty();
        result.Citations.Should().Contain(c => c.Key == "1");
        result.Citations.Should().Contain(c => c.Key == "2");
    }

    [Fact]
    public async Task ConvertAsync_DetectsAbstract()
    {
        // Arrange
        var text = "A Paper Title That Is Long Enough\n\n" +
                   "Abstract\n" +
                   "This is the abstract of the paper with important findings.\n" +
                   "1 Introduction\n" +
                   "This is the introduction.\n";

        var rawContent = new RawPdfContent(
            Text: text,
            PageCount: 1,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        var result = await _service.ConvertAsync("test.pdf");

        // Assert
        result.Abstract.Should().NotBeNullOrEmpty();
        result.Abstract.Should().Contain("abstract of the paper");
    }

    [Fact]
    public async Task EnhanceAsync_ReturnsDocumentAsIs()
    {
        // Arrange (enhancement is not yet implemented)
        var document = new MarkdownDocument
        {
            Title = "Test",
            Content = "# Test\n\nBody.",
        };

        // Act
        var result = await _service.EnhanceAsync(document, EnhancementLevel.Basic);

        // Assert
        result.Should().BeSameAs(document);
    }

    [Fact]
    public async Task ConvertAsync_CallsExtractorWithPreserveLayout()
    {
        // Arrange
        var rawContent = new RawPdfContent(
            Text: "A Paper Title That Is Long Enough\n\nBody text.",
            PageCount: 1,
            Metadata: new Dictionary<string, string>());

        _pdfExtractor.ExtractRawAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(rawContent);

        // Act
        await _service.ConvertAsync("my-paper.pdf");

        // Assert
        await _pdfExtractor.Received(1).ExtractRawAsync("my-paper.pdf", preserveLayout: true, Arg.Any<CancellationToken>());
    }
}
