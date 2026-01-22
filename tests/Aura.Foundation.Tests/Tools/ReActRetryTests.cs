using Aura.Foundation.Llm;
using Aura.Foundation.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class ReActRetryTests
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ReActExecutor> _logger;
    private readonly ReActExecutor _sut;

    public ReActRetryTests()
    {
        _toolRegistry = Substitute.For<IToolRegistry>();
        _logger = Substitute.For<ILogger<ReActExecutor>>();
        _sut = new ReActExecutor(_toolRegistry, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_RetryDisabled_DoesNotRetryAfterMaxSteps()
    {
        // Arrange - LLM always returns a response using a non-existent tool
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                return Task.FromResult(CreateInvalidToolResponse());
            });

        var options = new ReActOptions
        {
            RetryOnFailure = false,
            MaxSteps = 2
        };

        // Act
        var result = await _sut.ExecuteAsync(
            "Test task",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert - should fail after MaxSteps without retry
        callCount.Should().Be(2); // MaxSteps calls only
    }

    [Fact]
    public async Task ExecuteAsync_RetryEnabled_RetriesOnMaxStepsExhausted()
    {
        // Arrange - First 2 calls return invalid tool (hit max steps), then finish
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount < 3)
                {
                    // First attempt: hit max steps with invalid tool
                    return Task.FromResult(CreateInvalidToolResponse());
                }
                // Retry attempt: finish successfully
                return Task.FromResult(CreateFinishResponse("Done!"));
            });

        var options = new ReActOptions
        {
            RetryOnFailure = true,
            MaxRetries = 1,
            MaxSteps = 2,
            RetryCondition = RetryCondition.AllFailures
        };

        // Act
        var result = await _sut.ExecuteAsync(
            "Test task",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert
        result.Success.Should().BeTrue();
        callCount.Should().BeGreaterThan(2); // More than initial MaxSteps
    }

    [Fact]
    public async Task ExecuteAsync_RetryConditionBuildErrors_DoesNotRetryNonBuildError()
    {
        // Arrange - LLM returns invalid tool, no build error keywords
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                return Task.FromResult(CreateInvalidToolResponse());
            });

        var options = new ReActOptions
        {
            RetryOnFailure = true,
            MaxRetries = 2,
            MaxSteps = 1,
            RetryCondition = RetryCondition.BuildErrors
        };

        // Act
        var result = await _sut.ExecuteAsync(
            "Test task",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert - should not retry since it's not a build error
        callCount.Should().Be(1); // Only initial attempt, no retries
    }

    [Fact]
    public async Task ExecuteAsync_MaxRetriesExhausted_ReturnsFailure()
    {
        // Arrange - LLM always returns invalid tool
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                return Task.FromResult(CreateInvalidToolResponse());
            });

        var options = new ReActOptions
        {
            RetryOnFailure = true,
            MaxRetries = 2,
            MaxSteps = 1,
            RetryCondition = RetryCondition.AllFailures
        };

        // Act
        var result = await _sut.ExecuteAsync(
            "Test task",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed after 3 attempts"); // 1 initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_AllStepsRecorded_AcrossAttempts()
    {
        // Arrange - First 2 calls hit max steps, then finish
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return Task.FromResult(CreateInvalidToolResponse());
                }
                return Task.FromResult(CreateFinishResponse("Done!"));
            });

        var options = new ReActOptions
        {
            RetryOnFailure = true,
            MaxRetries = 1,
            MaxSteps = 2,
            RetryCondition = RetryCondition.AllFailures
        };

        // Act
        var result = await _sut.ExecuteAsync(
            "Test task",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert - should have steps from both attempts
        result.Success.Should().BeTrue();
        result.Steps.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public async Task ExecuteAsync_RetryPromptContainsErrorContext()
    {
        // Arrange
        string? capturedPrompt = null;
        var callCount = 0;
        var llm = Substitute.For<ILlmProvider>();
        llm.GenerateAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                // prompt is the second string argument (index 1)
                var prompt = callInfo.ArgAt<string>(1);
                if (callCount > 2) // Capture the retry prompt
                {
                    capturedPrompt = prompt;
                }
                if (callCount <= 2)
                {
                    return Task.FromResult(CreateInvalidToolResponse());
                }
                return Task.FromResult(CreateFinishResponse("Done!"));
            });

        var options = new ReActOptions
        {
            RetryOnFailure = true,
            MaxRetries = 1,
            MaxSteps = 2,
            RetryCondition = RetryCondition.AllFailures
        };

        // Act
        await _sut.ExecuteAsync(
            "Original task text",
            new List<ToolDefinition>(),
            llm,
            options);

        // Assert - retry prompt should contain original task and error context
        capturedPrompt.Should().NotBeNull();
        capturedPrompt.Should().Contain("Original task text");
        capturedPrompt.Should().Contain("Previous Attempt Failed");
    }

    private static LlmResponse CreateInvalidToolResponse()
    {
        // Returns a response trying to use a non-existent tool
        return new LlmResponse(
            """
            Thought: I need to use this tool
            Action: nonexistent_tool
            Action Input: {"param": "value"}
            """,
            50);
    }

    private static LlmResponse CreateFinishResponse(string answer)
    {
        return new LlmResponse(
            $"Thought: I have completed the task\nAction: finish\nAction Input: {answer}",
            30);
    }
}
