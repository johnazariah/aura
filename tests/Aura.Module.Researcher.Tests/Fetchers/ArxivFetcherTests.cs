// <copyright file="ArxivFetcherTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

#nullable enable

using Aura.Module.Researcher.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aura.Module.Researcher.Tests.Fetchers;

public class ArxivFetcherTests
{
    private readonly ArxivFetcher _sut;

    public ArxivFetcherTests()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(new ResearcherModuleOptions
        {
            StoragePath = Path.GetTempPath(),
        });

        _sut = new ArxivFetcher(httpClient, options, NullLogger<ArxivFetcher>.Instance);
    }

    [Theory]
    [InlineData("1706.03762")]
    [InlineData("2301.00000")]
    [InlineData("hep-th/9905111")]
    public void CanHandle_WithArxivId_ReturnsTrue(string arxivId)
    {
        // Act
        var result = _sut.CanHandle(arxivId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://arxiv.org/abs/1706.03762")]
    [InlineData("https://arxiv.org/pdf/1706.03762.pdf")]
    [InlineData("http://arxiv.org/abs/2301.00000")]
    public void CanHandle_WithArxivUrl_ReturnsTrue(string url)
    {
        // Act
        var result = _sut.CanHandle(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://github.com/something")]
    [InlineData("https://example.com/paper.pdf")]
    [InlineData("random text")]
    public void CanHandle_WithNonArxivUrl_ReturnsFalse(string input)
    {
        // Act
        var result = _sut.CanHandle(input);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Name_ReturnsArxiv()
    {
        // Act & Assert
        _sut.Name.Should().Be("arXiv");
    }

    // Integration test - only run when needed
    // [Fact]
    // public async Task FetchAsync_WithValidArxivId_ReturnsMetadata()
    // {
    //     // Arrange
    //     var arxivId = "1706.03762"; // "Attention Is All You Need"
    //
    //     // Act
    //     var result = await _sut.FetchAsync(arxivId, downloadPdf: false);
    //
    //     // Assert
    //     result.Success.Should().BeTrue();
    //     result.Source.Title.Should().Contain("Attention");
    //     result.Source.Authors.Should().Contain("Vaswani");
    //     result.Source.SourceType.Should().Be(Data.Entities.SourceType.ArxivPaper);
    // }
}
