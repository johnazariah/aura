// <copyright file="LibraryServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

#nullable enable

using Aura.Foundation.Llm;
using Aura.Module.Researcher.Data;
using Aura.Module.Researcher.Data.Entities;
using Aura.Module.Researcher.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Aura.Module.Researcher.Tests.Services;

public class LibraryServiceTests : IDisposable
{
    private readonly ResearcherDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly LibraryService _sut;

    public LibraryServiceTests()
    {
        // Use EF Core InMemory database for testing
        var dbOptions = new DbContextOptionsBuilder<ResearcherDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ResearcherDbContext(dbOptions);

        // Create mock for embedding provider
        _embeddingProvider = Substitute.For<IEmbeddingProvider>();
        _embeddingProvider
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[1536]); // Return empty embedding

        _sut = new LibraryService(
            _db,
            _embeddingProvider,
            NullLogger<LibraryService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateSourceAsync_WithValidSource_AddsToDatabase()
    {
        // Arrange
        var source = new Source
        {
            Title = "Attention Is All You Need",
            Authors = ["Vaswani", "Shazeer", "Parmar"],
            Abstract = "The dominant sequence transduction models are based on complex recurrent or convolutional neural networks.",
            SourceType = SourceType.Paper,
            Url = "https://arxiv.org/abs/1706.03762",
        };

        // Act
        var result = await _sut.CreateSourceAsync(source);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Title.Should().Be("Attention Is All You Need");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var fromDb = await _db.Sources.FindAsync(result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Title.Should().Be("Attention Is All You Need");
    }

    [Fact]
    public async Task GetSourcesAsync_WithNoFilters_ReturnsAllSources()
    {
        // Arrange
        await SeedTestSources();

        // Act
        var result = await _sut.GetSourcesAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetSourcesAsync_FilterBySourceType_ReturnsMatchingSources()
    {
        // Arrange
        await SeedTestSources();

        // Act
        var result = await _sut.GetSourcesAsync(sourceType: SourceType.Paper);

        // Assert
        result.Should().HaveCount(2);
        result.All(s => s.SourceType == SourceType.Paper).Should().BeTrue();
    }

    [Fact]
    public async Task GetSourcesAsync_FilterByReadingStatus_ReturnsMatchingSources()
    {
        // Arrange
        await SeedTestSources();

        // Act
        var result = await _sut.GetSourcesAsync(status: ReadingStatus.InProgress);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("BERT: Pre-training of Deep Bidirectional Transformers");
    }

    [Fact]
    public async Task GetSourceAsync_WithExistingId_ReturnsSource()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Title = "Test Paper",
            Authors = ["Test Author"],
            SourceType = SourceType.Paper,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSourceAsync(source.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Paper");
    }

    [Fact]
    public async Task GetSourceAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _sut.GetSourceAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSourceAsync_WithExistingSource_ReturnsTrue()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Title = "To Delete",
            Authors = ["Author"],
            SourceType = SourceType.Article,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteSourceAsync(source.Id);

        // Assert
        result.Should().BeTrue();

        var fromDb = await _db.Sources.FindAsync(source.Id);
        fromDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSourceAsync_WithNonExistingId_ReturnsFalse()
    {
        // Act
        var result = await _sut.DeleteSourceAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSourceAsync_UpdatesTimestamp()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Title = "Original Title",
            Authors = ["Author"],
            SourceType = SourceType.Paper,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();
        _db.Entry(source).State = EntityState.Detached;

        // Act
        source.Title = "Updated Title";
        var result = await _sut.UpdateSourceAsync(source);

        // Assert
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task AddExcerptAsync_WithValidExcerpt_AddsToDatabase()
    {
        // Arrange
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Title = "Paper",
            Authors = ["Author"],
            SourceType = SourceType.Paper,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();

        var excerpt = new Excerpt
        {
            SourceId = source.Id,
            Content = "This is an important excerpt from the paper.",
            PageNumber = 5,
        };

        // Act
        var result = await _sut.AddExcerptAsync(excerpt);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Content.Should().Be("This is an important excerpt from the paper.");
        result.PageNumber.Should().Be(5);
        result.SourceId.Should().Be(source.Id);
    }

    [Fact]
    public async Task GetExcerptsAsync_ReturnsExcerptsOrderedByPageNumber()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var source = new Source
        {
            Id = sourceId,
            Title = "Paper",
            Authors = ["Author"],
            SourceType = SourceType.Paper,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Sources.Add(source);

        var excerpts = new[]
        {
            new Excerpt { Id = Guid.NewGuid(), SourceId = sourceId, Content = "Page 10", PageNumber = 10, CreatedAt = DateTime.UtcNow },
            new Excerpt { Id = Guid.NewGuid(), SourceId = sourceId, Content = "Page 1", PageNumber = 1, CreatedAt = DateTime.UtcNow },
            new Excerpt { Id = Guid.NewGuid(), SourceId = sourceId, Content = "Page 5", PageNumber = 5, CreatedAt = DateTime.UtcNow },
        };
        _db.Excerpts.AddRange(excerpts);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetExcerptsAsync(sourceId);

        // Assert
        result.Should().HaveCount(3);
        result[0].PageNumber.Should().Be(1);
        result[1].PageNumber.Should().Be(5);
        result[2].PageNumber.Should().Be(10);
    }

    [Fact]
    public async Task CreateSourceAsync_GeneratesEmbedding()
    {
        // Arrange
        var source = new Source
        {
            Title = "Test Paper",
            Authors = ["Test Author"],
            Abstract = "This paper describes important research.",
            SourceType = SourceType.Paper,
        };

        // Act
        await _sut.CreateSourceAsync(source);

        // Assert
        await _embeddingProvider.Received(1).GenerateEmbeddingAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("important research")),
            Arg.Any<CancellationToken>());
    }

    private async Task SeedTestSources()
    {
        var sources = new[]
        {
            new Source
            {
                Id = Guid.NewGuid(),
                Title = "Attention Is All You Need",
                Authors = ["Vaswani"],
                SourceType = SourceType.Paper,
                ReadingStatus = ReadingStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
            },
            new Source
            {
                Id = Guid.NewGuid(),
                Title = "BERT: Pre-training of Deep Bidirectional Transformers",
                Authors = ["Devlin"],
                SourceType = SourceType.Paper,
                ReadingStatus = ReadingStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
            },
            new Source
            {
                Id = Guid.NewGuid(),
                Title = "Understanding Transformer Architecture",
                Authors = ["Blog Author"],
                SourceType = SourceType.Article,
                ReadingStatus = ReadingStatus.ToRead,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            },
        };

        _db.Sources.AddRange(sources);
        await _db.SaveChangesAsync();
    }
}
