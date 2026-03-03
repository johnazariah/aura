// <copyright file="BackgroundIndexerTests.cs" company="Aura">
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

public class BackgroundIndexerTests : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MockFileSystem _fileSystem;
    private readonly IOptions<BackgroundIndexerOptions> _options;
    private readonly ILogger<BackgroundIndexer> _logger;
    private readonly BackgroundIndexer _indexer;

    public BackgroundIndexerTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _fileSystem = new MockFileSystem();
        _options = Options.Create(new BackgroundIndexerOptions());
        _logger = NullLogger<BackgroundIndexer>.Instance;

        _indexer = new BackgroundIndexer(_scopeFactory, _fileSystem, _options, _logger);
    }

    public void Dispose()
    {
        _indexer.Dispose();
    }

    [Fact]
    public void QueueContent_WhenQueueHasSpace_ReturnsTrue()
    {
        // Arrange
        var content = new RagContent("test-id", "test content");

        // Act
        var result = _indexer.QueueContent(content);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void QueueContent_MultipleTimes_ReturnsTrue()
    {
        // Arrange & Act & Assert
        for (var i = 0; i < 10; i++)
        {
            var content = new RagContent($"test-id-{i}", $"content {i}");
            _indexer.QueueContent(content).Should().BeTrue();
        }
    }

    [Fact]
    public void QueueDirectory_NewPath_ReturnsNewJob()
    {
        // Arrange
        var path = "/some/directory";

        // Act
        var (jobId, isNew) = _indexer.QueueDirectory(path);

        // Assert
        jobId.Should().NotBeEmpty();
        isNew.Should().BeTrue();
    }

    [Fact]
    public void QueueDirectory_DuplicatePath_ReturnsExistingJob()
    {
        // Arrange
        var path = "/some/directory";

        // Act
        var (firstJobId, firstIsNew) = _indexer.QueueDirectory(path);
        var (secondJobId, secondIsNew) = _indexer.QueueDirectory(path);

        // Assert
        firstIsNew.Should().BeTrue();
        secondIsNew.Should().BeFalse();
        secondJobId.Should().Be(firstJobId);
    }

    [Fact]
    public void QueueDirectory_DifferentPaths_ReturnsDifferentJobs()
    {
        // Arrange & Act
        var (jobId1, isNew1) = _indexer.QueueDirectory("/path/one");
        var (jobId2, isNew2) = _indexer.QueueDirectory("/path/two");

        // Assert
        isNew1.Should().BeTrue();
        isNew2.Should().BeTrue();
        jobId1.Should().NotBe(jobId2);
    }

    [Fact]
    public void GetStatus_Initially_ReturnsZeroCounts()
    {
        // Act
        var status = _indexer.GetStatus();

        // Assert
        status.ProcessedItems.Should().Be(0);
        status.FailedItems.Should().Be(0);
        status.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public void GetStatus_AfterQueueingDirectory_ReturnsActiveJob()
    {
        // Arrange
        _indexer.QueueDirectory("/some/path");

        // Act
        var status = _indexer.GetStatus();

        // Assert
        status.ActiveJobs.Should().Be(1);
    }

    [Fact]
    public void GetJobStatus_UnknownJobId_ReturnsNull()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var status = _indexer.GetJobStatus(unknownId);

        // Assert
        status.Should().BeNull();
    }

    [Fact]
    public void GetJobStatus_KnownJobId_ReturnsStatus()
    {
        // Arrange
        var (jobId, _) = _indexer.QueueDirectory("/some/path");

        // Act
        var status = _indexer.GetJobStatus(jobId);

        // Assert
        status.Should().NotBeNull();
        status!.JobId.Should().Be(jobId);
        status.State.Should().Be(IndexJobState.Queued);
    }

    [Fact]
    public void GetActiveJobs_WithNoJobs_ReturnsEmpty()
    {
        // Act
        var activeJobs = _indexer.GetActiveJobs();

        // Assert
        activeJobs.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveJobs_WithQueuedJobs_ReturnsOnlyActiveJobs()
    {
        // Arrange
        _indexer.QueueDirectory("/path/one");
        _indexer.QueueDirectory("/path/two");

        // Act
        var activeJobs = _indexer.GetActiveJobs();

        // Assert
        activeJobs.Should().HaveCount(2);
        activeJobs.Should().OnlyContain(j => j.State == IndexJobState.Queued);
    }

    [Fact]
    public void GetActiveJobs_JobSourceMatchesPath()
    {
        // Arrange
        var path = "/test/workspace";
        _indexer.QueueDirectory(path);

        // Act
        var activeJobs = _indexer.GetActiveJobs();

        // Assert
        activeJobs.Should().ContainSingle()
            .Which.Source.Should().Contain("test/workspace");
    }
}
