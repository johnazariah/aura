// <copyright file="SourceFetcherServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

#nullable enable

using Aura.Module.Researcher.Data.Entities;
using Aura.Module.Researcher.Fetchers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Aura.Module.Researcher.Tests.Fetchers;

public class SourceFetcherServiceTests
{
    private readonly ISourceFetcher _arxivFetcher;
    private readonly ISourceFetcher _webFetcher;
    private readonly SourceFetcherService _sut;

    public SourceFetcherServiceTests()
    {
        _arxivFetcher = Substitute.For<ISourceFetcher>();
        _arxivFetcher.Name.Returns("arXiv");
        _arxivFetcher.CanHandle(Arg.Is<string>(s => s.Contains("arxiv"))).Returns(true);

        _webFetcher = Substitute.For<ISourceFetcher>();
        _webFetcher.Name.Returns("Web");
        _webFetcher.CanHandle(Arg.Any<string>()).Returns(true); // Fallback

        // arXiv first (more specific), then web (fallback)
        _sut = new SourceFetcherService(
            [_arxivFetcher, _webFetcher],
            NullLogger<SourceFetcherService>.Instance);
    }

    [Fact]
    public void Fetchers_ReturnsAllRegisteredFetchers()
    {
        // Act
        var fetchers = _sut.Fetchers;

        // Assert
        fetchers.Should().HaveCount(2);
        fetchers.Select(f => f.Name).Should().Contain(["arXiv", "Web"]);
    }

    [Fact]
    public async Task FetchAsync_WithArxivUrl_UsesArxivFetcher()
    {
        // Arrange
        var url = "https://arxiv.org/abs/1706.03762";
        _arxivFetcher.FetchAsync(url, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Source = new Source { Title = "Test Paper", SourceType = SourceType.Paper },
                Success = true,
            });

        // Act
        var result = await _sut.FetchAsync(url);

        // Assert
        result.Success.Should().BeTrue();
        result.Source.Title.Should().Be("Test Paper");

        await _arxivFetcher.Received(1).FetchAsync(url, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _webFetcher.DidNotReceive().FetchAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchAsync_WithGenericUrl_UsesWebFetcher()
    {
        // Arrange
        var url = "https://example.com/article";
        _arxivFetcher.CanHandle(url).Returns(false); // arXiv doesn't handle this

        _webFetcher.FetchAsync(url, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Source = new Source { Title = "Web Article", SourceType = SourceType.Article },
                Success = true,
            });

        // Act
        var result = await _sut.FetchAsync(url);

        // Assert
        result.Success.Should().BeTrue();
        result.Source.Title.Should().Be("Web Article");
        result.Source.SourceType.Should().Be(SourceType.Article);
    }

    [Fact]
    public async Task FetchAsync_WithNoMatchingFetcher_ReturnsError()
    {
        // Arrange
        var url = "ftp://invalid.com";
        _arxivFetcher.CanHandle(url).Returns(false);
        _webFetcher.CanHandle(url).Returns(false);

        // Act
        var result = await _sut.FetchAsync(url);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No fetcher found");
    }

    [Fact]
    public async Task FetchAsync_PassesDownloadPdfParameter()
    {
        // Arrange
        var url = "https://arxiv.org/abs/1234";
        _arxivFetcher.FetchAsync(url, false, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Source = new Source { Title = "Test" },
                Success = true,
            });

        // Act
        await _sut.FetchAsync(url, downloadPdf: false);

        // Assert
        await _arxivFetcher.Received(1).FetchAsync(url, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_SearchesAllFetchers()
    {
        // Arrange
        var query = "transformer attention";

        _arxivFetcher.SearchAsync(query, 10, Arg.Any<CancellationToken>())
            .Returns([new SearchResult { Title = "ArXiv Paper", Url = "https://arxiv.org/abs/1234", Source = "arXiv" }]);

        _webFetcher.SearchAsync(query, 10, Arg.Any<CancellationToken>())
            .Returns([new SearchResult { Title = "Web Article", Url = "https://example.com", Source = "Web" }]);

        // Act
        var results = await _sut.SearchAsync(query, limit: 10);

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.Title).Should().Contain(["ArXiv Paper", "Web Article"]);
    }

    [Fact]
    public async Task SearchAsync_WithSpecificSources_OnlySearchesThoseFetchers()
    {
        // Arrange
        var query = "transformer";

        _arxivFetcher.SearchAsync(query, 10, Arg.Any<CancellationToken>())
            .Returns([new SearchResult { Title = "ArXiv Paper", Url = "https://arxiv.org/abs/5678", Source = "arXiv" }]);

        // Act
        var results = await _sut.SearchAsync(query, sources: ["arXiv"], limit: 10);

        // Assert
        results.Should().HaveCount(1);
        results.First().Title.Should().Be("ArXiv Paper");

        await _webFetcher.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
