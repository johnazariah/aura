// <copyright file="IngestorRegistryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag.Ingestors;

using Aura.Foundation.Rag;
using Aura.Foundation.Rag.Ingestors;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class IngestorRegistryTests
{
    private readonly IngestorRegistry _sut;

    public IngestorRegistryTests()
    {
        _sut = new IngestorRegistry(NullLogger<IngestorRegistry>.Instance);
    }

    [Fact]
    public void Constructor_RegistersDefaultIngestors()
    {
        // Assert
        _sut.Ingestors.Should().HaveCountGreaterThanOrEqualTo(3);
        _sut.Ingestors.Should().Contain(i => i is MarkdownIngestor);
        _sut.Ingestors.Should().Contain(i => i is CodeIngestor);
        _sut.Ingestors.Should().Contain(i => i is PlainTextIngestor);
    }

    [Theory]
    [InlineData("README.md", "markdown")]
    [InlineData("Program.cs", "code")]
    [InlineData("app.ts", "code")]
    [InlineData("script.py", "code")]
    [InlineData("notes.txt", "plaintext")]
    [InlineData("LICENSE", "plaintext")]
    public void GetIngestor_ReturnsCorrectIngestor(string filePath, string expectedIngestorId)
    {
        // Act
        var ingestor = _sut.GetIngestor(filePath);

        // Assert
        ingestor.Should().NotBeNull();
        ingestor!.IngestorId.Should().Be(expectedIngestorId);
    }

    [Fact]
    public void GetIngestor_UnknownExtension_ReturnsFallback()
    {
        // Act
        var ingestor = _sut.GetIngestor("data.xyz");

        // Assert
        ingestor.Should().NotBeNull();
        ingestor!.IngestorId.Should().Be("plaintext");
    }

    [Fact]
    public void Register_AddsNewIngestor()
    {
        // Arrange
        var customIngestor = new CustomTestIngestor();
        var initialCount = _sut.Ingestors.Count;

        // Act
        _sut.Register(customIngestor);

        // Assert
        _sut.Ingestors.Should().HaveCount(initialCount + 1);
        _sut.Ingestors.Should().Contain(customIngestor);
    }

    [Fact]
    public void Register_NewIngestorTakesPriority()
    {
        // Arrange - Register a custom ingestor for .md files
        var customIngestor = new CustomTestIngestor();
        _sut.Register(customIngestor);

        // Act
        var ingestor = _sut.GetIngestor("test.custom");

        // Assert
        ingestor.Should().Be(customIngestor);
    }

    private class CustomTestIngestor : IContentIngestor
    {
        public string IngestorId => "custom-test";
        public IReadOnlyList<string> SupportedExtensions { get; } = [".custom"];
        public RagContentType ContentType => RagContentType.Unknown;

        public bool CanIngest(string filePath)
            => SupportedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<IngestedChunk>> IngestAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IngestedChunk>>(
                [new IngestedChunk(content, "custom")]);
        }
    }
}
