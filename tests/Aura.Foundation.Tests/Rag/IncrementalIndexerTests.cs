// <copyright file="IncrementalIndexerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Rag;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class IncrementalIndexerTests : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MockFileSystem _fileSystem;
    private readonly IOptions<RagWatcherOptions> _options;
    private readonly ILogger<IncrementalIndexer> _logger;
    private readonly IncrementalIndexer _indexer;

    public IncrementalIndexerTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _fileSystem = new MockFileSystem();
        _options = Options.Create(new RagWatcherOptions());
        _logger = NullLogger<IncrementalIndexer>.Instance;

        _indexer = new IncrementalIndexer(_scopeFactory, _fileSystem, _options, _logger);
    }

    public void Dispose()
    {
        _indexer.Dispose();
    }

    [Fact]
    public void RagWatcherOptions_DefaultDebounceMs_Is500()
    {
        // Arrange
        var options = new RagWatcherOptions();

        // Assert
        options.DebounceMs.Should().Be(500);
    }

    [Fact]
    public void RagWatcherOptions_DefaultPatterns_IncludesPdf()
    {
        // Arrange
        var options = new RagWatcherOptions();

        // Assert
        options.DefaultPatterns.Should().Contain("*.pdf");
    }

    [Fact]
    public void RagWatcherOptions_DefaultPatterns_IncludesExpectedTypes()
    {
        // Arrange
        var options = new RagWatcherOptions();

        // Assert
        options.DefaultPatterns.Should().Contain("*.cs");
        options.DefaultPatterns.Should().Contain("*.ts");
        options.DefaultPatterns.Should().Contain("*.md");
        options.DefaultPatterns.Should().Contain("*.py");
        options.DefaultPatterns.Should().Contain("*.json");
        options.DefaultPatterns.Should().Contain("*.pdf");
    }

    [Fact]
    public void RagWatcherOptions_SectionName_IsCorrect()
    {
        // Assert
        RagWatcherOptions.SectionName.Should().Be("Aura:Rag:Watcher");
    }

    [Fact]
    public void WatchDirectory_NonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        var path = "/nonexistent/directory";

        // Act
        var act = () => _indexer.WatchDirectory(path);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void UnwatchDirectory_UnknownPath_DoesNotThrow()
    {
        // Arrange
        var path = "/unknown/path";

        // Act
        var act = () => _indexer.UnwatchDirectory(path);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - should not throw
        _indexer.Dispose();
        _indexer.Dispose();
    }
}
