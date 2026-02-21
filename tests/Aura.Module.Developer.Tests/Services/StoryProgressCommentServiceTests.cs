// <copyright file="StoryProgressCommentServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Services;

using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class StoryProgressCommentServiceTests
{
    private readonly IGitHubService _gitHub = Substitute.For<IGitHubService>();
    private readonly StoryProgressCommentService _sut;

    public StoryProgressCommentServiceTests()
    {
        _gitHub.IsConfigured.Returns(true);
        _sut = new StoryProgressCommentService(_gitHub, NullLogger<StoryProgressCommentService>.Instance);
    }

    [Fact]
    public async Task PostAnalysisCompleteCommentAsync_WhenConfigured_PostsComment()
    {
        // Act
        await _sut.PostAnalysisCompleteCommentAsync("owner", "repo", 42, CancellationToken.None);

        // Assert
        await _gitHub.Received(1).PostCommentAsync(
            "owner", "repo", 42,
            Arg.Is<string>(s => s.Contains("Analysis complete")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostAnalysisCompleteCommentAsync_WhenNotConfigured_SkipsComment()
    {
        // Arrange
        _gitHub.IsConfigured.Returns(false);

        // Act
        await _sut.PostAnalysisCompleteCommentAsync("owner", "repo", 42, CancellationToken.None);

        // Assert
        await _gitHub.DidNotReceive().PostCommentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostPlanningCompleteCommentAsync_WhenConfigured_PostsCommentWithStepCount()
    {
        // Act
        await _sut.PostPlanningCompleteCommentAsync("owner", "repo", 42, 5, CancellationToken.None);

        // Assert
        await _gitHub.Received(1).PostCommentAsync(
            "owner", "repo", 42,
            Arg.Is<string>(s => s.Contains("Planning complete") && s.Contains("5 steps")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostPlanningCompleteCommentAsync_WithSingleStep_UsesSingularForm()
    {
        // Act
        await _sut.PostPlanningCompleteCommentAsync("owner", "repo", 42, 1, CancellationToken.None);

        // Assert
        await _gitHub.Received(1).PostCommentAsync(
            "owner", "repo", 42,
            Arg.Is<string>(s => s.Contains("1 step") && !s.Contains("1 steps")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostPlanningCompleteCommentAsync_WhenNotConfigured_SkipsComment()
    {
        // Arrange
        _gitHub.IsConfigured.Returns(false);

        // Act
        await _sut.PostPlanningCompleteCommentAsync("owner", "repo", 42, 5, CancellationToken.None);

        // Assert
        await _gitHub.DidNotReceive().PostCommentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostWaveCompleteCommentAsync_WhenConfigured_PostsCommentWithWaveProgress()
    {
        // Act
        await _sut.PostWaveCompleteCommentAsync("owner", "repo", 42, 2, 4, CancellationToken.None);

        // Assert
        await _gitHub.Received(1).PostCommentAsync(
            "owner", "repo", 42,
            Arg.Is<string>(s => s.Contains("Wave 2/4")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostWaveCompleteCommentAsync_WhenNotConfigured_SkipsComment()
    {
        // Arrange
        _gitHub.IsConfigured.Returns(false);

        // Act
        await _sut.PostWaveCompleteCommentAsync("owner", "repo", 42, 2, 4, CancellationToken.None);

        // Assert
        await _gitHub.DidNotReceive().PostCommentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostPRReadyCommentAsync_WhenConfigured_PostsCommentWithPrLink()
    {
        // Arrange
        var prLink = "https://github.com/owner/repo/pull/99";

        // Act
        await _sut.PostPRReadyCommentAsync("owner", "repo", 42, prLink, CancellationToken.None);

        // Assert
        await _gitHub.Received(1).PostCommentAsync(
            "owner", "repo", 42,
            Arg.Is<string>(s => s.Contains("Pull request ready") && s.Contains(prLink)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostPRReadyCommentAsync_WhenNotConfigured_SkipsComment()
    {
        // Arrange
        _gitHub.IsConfigured.Returns(false);

        // Act
        await _sut.PostPRReadyCommentAsync("owner", "repo", 42, "https://github.com/owner/repo/pull/99", CancellationToken.None);

        // Assert
        await _gitHub.DidNotReceive().PostCommentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
