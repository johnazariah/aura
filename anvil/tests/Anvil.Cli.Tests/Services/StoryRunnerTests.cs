using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using Anvil.Cli.Services;
using Anvil.Cli.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Anvil.Cli.Tests.Services;

public class StoryRunnerTests
{
    private readonly FakeAuraClient _auraClient;
    private readonly IExpectationValidator _validator;
    private readonly StoryRunner _sut;
    private readonly RunOptions _options;

    public StoryRunnerTests()
    {
        _auraClient = new FakeAuraClient();
        _validator = Substitute.For<IExpectationValidator>();
        _sut = new StoryRunner(_auraClient, _validator, NullLogger<StoryRunner>.Instance);
        _options = new RunOptions(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));

        // Default validator behavior - return all expectations as passed
        _validator.ValidateAsync(Arg.Any<Scenario>(), Arg.Any<StoryResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpectationResult>>([]));
    }

    private static Scenario CreateScenario(string name = "test") => new()
    {
        Name = name,
        Description = "Test scenario",
        Language = "csharp",
        Repository = "/repos/test",
        Story = new StoryDefinition
        {
            Title = "Test Story",
            Description = "Test description"
        },
        Expectations =
        [
            new Expectation { Type = "compiles", Description = "Should compile" }
        ]
    };

    [Fact]
    public async Task RunAsync_WithSuccessfulStory_ReturnsSuccess()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        // Simulate story completing
        _auraClient.SetStoryStatus(storyId, "Completed");

        _validator.ValidateAsync(Arg.Any<Scenario>(), Arg.Any<StoryResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpectationResult>>(
            [
                new ExpectationResult
                {
                    Expectation = scenario.Expectations[0],
                    Passed = true,
                    Message = "Passed"
                }
            ]));

        // Act
        var result = await _sut.RunAsync(scenario, _options);

        // Assert
        result.Success.Should().BeTrue();
        result.StoryId.Should().Be(storyId);
        result.ExpectationResults.Should().HaveCount(1);
        result.ExpectationResults[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithFailedStory_ReturnsFailure()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Failed", "Build error");

        _validator.ValidateAsync(Arg.Any<Scenario>(), Arg.Any<StoryResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpectationResult>>(
            [
                new ExpectationResult
                {
                    Expectation = scenario.Expectations[0],
                    Passed = false,
                    Message = "Build failed"
                }
            ]));

        // Act
        var result = await _sut.RunAsync(scenario, _options);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithAuraDown_ThrowsUnavailable()
    {
        // Arrange
        var scenario = CreateScenario();
        _auraClient.ShouldThrowUnavailable = true;

        // Act
        var act = () => _sut.RunAsync(scenario, _options);

        // Assert
        await act.Should().ThrowAsync<AuraUnavailableException>();
    }

    [Fact]
    public async Task RunAsync_CallsAnalyzePlanRun_InOrder()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Completed");

        // Act
        await _sut.RunAsync(scenario, _options);

        // Assert
        _auraClient.CallLog.Should().ContainInOrder(
            $"CreateStory:Test Story",
            $"AnalyzeStory:{storyId}",
            $"PlanStory:{storyId}",
            $"RunStory:{storyId}"
        );
    }

    [Fact]
    public async Task RunAsync_DeletesStoryOnCompletion()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Completed");

        // Act
        await _sut.RunAsync(scenario, _options);

        // Assert
        _auraClient.CallLog.Should().Contain($"DeleteStory:{storyId}");
    }

    [Fact]
    public async Task RunAsync_DeletesStoryOnFailure()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Failed", "Error");

        // Act
        await _sut.RunAsync(scenario, _options);

        // Assert
        _auraClient.CallLog.Should().Contain($"DeleteStory:{storyId}");
    }

    [Fact]
    public async Task RunAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        // Story never completes - stays in Running status
        _auraClient.SetStoryStatus(storyId, "Running");

        var shortTimeout = new RunOptions(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(50));

        // Act
        var act = () => _sut.RunAsync(scenario, shortTimeout);

        // Assert
        await act.Should().ThrowAsync<StoryTimeoutException>()
            .Where(ex => ex.StoryId == storyId);
    }

    [Fact]
    public async Task RunAsync_RecordsDuration()
    {
        // Arrange
        var scenario = CreateScenario();
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Completed");

        // Act
        var result = await _sut.RunAsync(scenario, _options);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_IncludesScenarioInResult()
    {
        // Arrange
        var scenario = CreateScenario("my-test-scenario");
        var storyId = Guid.NewGuid();
        _auraClient.EnqueueStoryResponse(new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Status = "Created"
        });
        _auraClient.SetStoryStatus(storyId, "Completed");

        // Act
        var result = await _sut.RunAsync(scenario, _options);

        // Assert
        result.Scenario.Name.Should().Be("my-test-scenario");
    }
}
