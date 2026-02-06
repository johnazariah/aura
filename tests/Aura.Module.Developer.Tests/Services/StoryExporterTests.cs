// <copyright file="StoryExporterTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Aura.Module.Developer.Tests.Services;

public sealed class StoryExporterTests : IDisposable
{
    private readonly IStoryService _storyService;
    private readonly StoryExporter _sut;
    private readonly string _tempDir;

    public StoryExporterTests()
    {
        _storyService = Substitute.For<IStoryService>();
        _sut = new StoryExporter(_storyService, NullLogger<StoryExporter>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "aura-export-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_WithValidStory_ExportsResearchPlanChanges()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        var story = new Story
        {
            Id = storyId,
            Title = "Add caching to UserService",
            Description = "Implement Redis caching for user lookups",
            RepositoryPath = _tempDir,
            Status = StoryStatus.Completed,
            AnalyzedContext = """
            {
                "analysis": "Need to add caching layer to improve performance",
                "coreRequirements": ["Add Redis cache", "Implement cache invalidation"],
                "constraints": ["Must be backward compatible"],
                "affectedFiles": ["src/UserService.cs", "src/CacheManager.cs"]
            }
            """,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Steps =
            [
                new StoryStep
                {
                    Id = Guid.NewGuid(),
                    StoryId = storyId,
                    Order = 1,
                    Name = "Implement cache wrapper",
                    Capability = "coding",
                    Status = StepStatus.Completed,
                    StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
                    CompletedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    Output = """{"summary": "Added CacheWrapper class"}""",
                },
                new StoryStep
                {
                    Id = Guid.NewGuid(),
                    StoryId = storyId,
                    Order = 2,
                    Name = "Add unit tests",
                    Capability = "testing",
                    Status = StepStatus.Completed,
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                    CompletedAt = DateTimeOffset.UtcNow,
                    Output = """{"summary": "Added 5 unit tests"}""",
                },
            ],
        };

        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns(story);

        var request = new StoryExportRequest
        {
            OutputPath = Path.Combine(_tempDir, ".project"),
            Include = ["research", "plan", "changes"],
        };

        // Act
        var result = await _sut.ExportAsync(storyId, request);

        // Assert
        result.Exported.Should().HaveCount(3);

        // Verify research file
        var researchFile = result.Exported.FirstOrDefault(e => e.Type == "research");
        researchFile.Should().NotBeNull();
        var researchPath = Path.Combine(_tempDir, researchFile!.Path);
        File.Exists(researchPath).Should().BeTrue();
        var researchContent = await File.ReadAllTextAsync(researchPath);
        researchContent.Should().Contain("# Research: Add caching to UserService");
        researchContent.Should().Contain("Add Redis cache");
        researchContent.Should().Contain("`src/UserService.cs`");

        // Verify plan file
        var planFile = result.Exported.FirstOrDefault(e => e.Type == "plan");
        planFile.Should().NotBeNull();
        var planPath = Path.Combine(_tempDir, planFile!.Path);
        File.Exists(planPath).Should().BeTrue();
        var planContent = await File.ReadAllTextAsync(planPath);
        planContent.Should().Contain("# Plan: Add caching to UserService");
        planContent.Should().Contain("### Step 1: Implement cache wrapper");
        planContent.Should().Contain("### Step 2: Add unit tests");

        // Verify changes file
        var changesFile = result.Exported.FirstOrDefault(e => e.Type == "changes");
        changesFile.Should().NotBeNull();
        var changesPath = Path.Combine(_tempDir, changesFile!.Path);
        File.Exists(changesPath).Should().BeTrue();
        var changesContent = await File.ReadAllTextAsync(changesPath);
        changesContent.Should().Contain("# Changes: Add caching to UserService");
        changesContent.Should().Contain("[x] Step 1: Implement cache wrapper ✅");
        changesContent.Should().Contain("[x] Step 2: Add unit tests ✅");
    }

    [Fact]
    public async Task ExportAsync_WithWorktreePath_UsesWorktreeDirectory()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        var worktreePath = Path.Combine(_tempDir, "worktree");
        Directory.CreateDirectory(worktreePath);

        var story = new Story
        {
            Id = storyId,
            Title = "Test story",
            WorktreePath = worktreePath,
            Status = StoryStatus.Analyzed,
            Steps = [],
        };

        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns(story);

        var request = new StoryExportRequest
        {
            Include = ["research"],
        };

        // Act
        var result = await _sut.ExportAsync(storyId, request);

        // Assert
        result.Exported.Should().HaveCount(1);
        var expectedPath = Path.Combine(worktreePath, ".project", "research");
        Directory.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithNoPathAndNoWorktree_ThrowsException()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        var story = new Story
        {
            Id = storyId,
            Title = "Test story",
            RepositoryPath = null,
            WorktreePath = null,
            Steps = [],
        };

        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns(story);

        var request = new StoryExportRequest();

        // Act
        var act = () => _sut.ExportAsync(storyId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot determine output path*");
    }

    [Fact]
    public async Task ExportAsync_WithStoryNotFound_ThrowsException()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns((Story?)null);

        var request = new StoryExportRequest();

        // Act
        var act = () => _sut.ExportAsync(storyId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{storyId}*not found*");
    }

    [Fact]
    public async Task ExportAsync_WithPartialInclude_ExportsOnlyRequested()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        var story = new Story
        {
            Id = storyId,
            Title = "Test story",
            RepositoryPath = _tempDir,
            Status = StoryStatus.Planned,
            Steps = [],
        };

        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns(story);

        var request = new StoryExportRequest
        {
            OutputPath = Path.Combine(_tempDir, ".project"),
            Include = ["plan"],
        };

        // Act
        var result = await _sut.ExportAsync(storyId, request);

        // Assert
        result.Exported.Should().HaveCount(1);
        result.Exported[0].Type.Should().Be("plan");
    }

    [Fact]
    public async Task ExportAsync_WithUnknownArtifactType_AddsWarning()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        var story = new Story
        {
            Id = storyId,
            Title = "Test story",
            RepositoryPath = _tempDir,
            Status = StoryStatus.Planned,
            Steps = [],
        };

        _storyService.GetByIdWithStepsAsync(storyId, Arg.Any<CancellationToken>())
            .Returns(story);

        var request = new StoryExportRequest
        {
            OutputPath = Path.Combine(_tempDir, ".project"),
            Include = ["unknown_type"],
        };

        // Act
        var result = await _sut.ExportAsync(storyId, request);

        // Assert
        result.Exported.Should().BeEmpty();
        result.Warnings.Should().Contain("Unknown artifact type: unknown_type");
    }
}
