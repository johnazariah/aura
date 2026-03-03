// <copyright file="PdfIngestorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Tests.Ingestors;

using Aura.Foundation.Rag;
using Aura.Module.Researcher.Ingestors;
using Aura.Module.Researcher.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public class PdfIngestorTests
{
    private readonly IPdfExtractor _pdfExtractor = Substitute.For<IPdfExtractor>();
    private readonly ILogger<PdfIngestor> _logger = NullLogger<PdfIngestor>.Instance;
    private readonly PdfIngestor _sut;

    public PdfIngestorTests()
    {
        _sut = new PdfIngestor(_pdfExtractor, _logger, chunkSize: 200);
    }

    [Fact]
    public void IngestorId_ReturnsPdf()
    {
        _sut.IngestorId.Should().Be("pdf");
    }

    [Fact]
    public void ContentType_ReturnsPdf()
    {
        _sut.ContentType.Should().Be(RagContentType.Pdf);
    }

    [Fact]
    public void SupportedExtensions_ContainsPdf()
    {
        _sut.SupportedExtensions.Should().Contain(".pdf");
    }

    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("document.PDF", true)]
    [InlineData("document.Pdf", true)]
    [InlineData("document.txt", false)]
    [InlineData("document.cs", false)]
    [InlineData("document.md", false)]
    [InlineData("pdf", false)]
    public void CanIngest_ChecksFileExtension(string filePath, bool expected)
    {
        _sut.CanIngest(filePath).Should().Be(expected);
    }

    [Fact]
    public async Task IngestAsync_PdfToTextNotAvailable_ReturnsEmptyList()
    {
        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(false);

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestAsync_ExtractRawThrows_ReturnsEmptyList()
    {
        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("extraction failed"));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestAsync_EmptyText_ReturnsEmptyList()
    {
        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("   ", 1, new Dictionary<string, string>()));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestAsync_ValidPdf_ReturnsChunks()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "Test Document",
            ["Author"] = "Jane Doe",
        };

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("This is a test paragraph.", 5, metadata));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Text.Should().Contain("This is a test paragraph.");
    }

    [Fact]
    public async Task IngestAsync_ValidPdf_MetadataIncludesPageCount()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "Test Doc",
        };

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Content here.", 42, metadata));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Metadata.Should().ContainKey("pageCount");
        result[0].Metadata!["pageCount"].Should().Be("42");
    }

    [Fact]
    public async Task IngestAsync_WithAuthor_MetadataIncludesAuthor()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "Test Doc",
            ["Author"] = "John Smith",
        };

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Content here.", 1, metadata));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Metadata.Should().ContainKey("author");
        result[0].Metadata!["author"].Should().Be("John Smith");
    }

    [Fact]
    public async Task IngestAsync_NoTitle_UsesFileNameAsTitle()
    {
        var metadata = new Dictionary<string, string>();

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("docs/report.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Content here.", 1, metadata));

        var result = await _sut.IngestAsync("docs/report.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Title.Should().Be("report");
    }

    [Fact]
    public async Task IngestAsync_WithTitle_UsesPdfTitle()
    {
        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "My Report",
        };

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("report.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Content here.", 1, metadata));

        var result = await _sut.IngestAsync("report.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Title.Should().Be("My Report");
    }

    [Fact]
    public async Task IngestAsync_LargeText_SplitsByParagraphBoundaries()
    {
        var paragraph1 = new string('A', 150);
        var paragraph2 = new string('B', 150);
        var text = $"{paragraph1}\n\n{paragraph2}";

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent(text, 1, new Dictionary<string, string>()));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().HaveCount(2);
        result[0].Text.Should().Contain(paragraph1);
        result[1].Text.Should().Contain(paragraph2);
    }

    [Fact]
    public async Task IngestAsync_MultipleChunks_FirstIsDocument_RestAreSection()
    {
        var paragraph1 = new string('A', 150);
        var paragraph2 = new string('B', 150);
        var text = $"{paragraph1}\n\n{paragraph2}";

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent(text, 1, new Dictionary<string, string>()));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().HaveCount(2);
        result[0].ChunkType.Should().Be("document");
        result[1].ChunkType.Should().Be("section");
    }

    [Fact]
    public async Task IngestAsync_MultipleChunks_SecondChunkTitleIncludesPartNumber()
    {
        var paragraph1 = new string('A', 150);
        var paragraph2 = new string('B', 150);
        var text = $"{paragraph1}\n\n{paragraph2}";

        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "My Doc",
        };

        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent(text, 1, metadata));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("My Doc");
        result[1].Title.Should().Be("My Doc (part 2)");
    }

    [Fact]
    public async Task IngestAsync_Chunks_ContainSourceFileMetadata()
    {
        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("docs/report.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Some content.", 1, new Dictionary<string, string>()));

        var result = await _sut.IngestAsync("docs/report.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Metadata.Should().ContainKey("sourceFile");
        result[0].Metadata!["sourceFile"].Should().Be("report.pdf");
    }

    [Fact]
    public async Task IngestAsync_Chunks_ContainChunkIndex()
    {
        _pdfExtractor.IsPdfToTextAvailableAsync().Returns(true);
        _pdfExtractor.ExtractRawAsync("test.pdf", preserveLayout: false, Arg.Any<CancellationToken>())
            .Returns(new RawPdfContent("Content here.", 1, new Dictionary<string, string>()));

        var result = await _sut.IngestAsync("test.pdf", "");

        result.Should().NotBeEmpty();
        result[0].Metadata.Should().ContainKey("chunkIndex");
        result[0].Metadata!["chunkIndex"].Should().Be("1");
    }
}
