// <copyright file="WorkflowVerificationServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Services.Verification;

using Aura.Module.Developer.Services.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public sealed class WorkflowVerificationServiceTests
{
    private readonly IProjectVerificationDetector _detector = Substitute.For<IProjectVerificationDetector>();
    private readonly WorkflowVerificationService _service;

    public WorkflowVerificationServiceTests()
    {
        _service = new WorkflowVerificationService(
            _detector,
            NullLogger<WorkflowVerificationService>.Instance);
    }

    [Fact]
    public async Task VerifyAsync_NoProjectsDetected_ReturnsSuccessWithEmptySteps()
    {
        // Arrange
        _detector.DetectProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetectedProject>>([]));

        // Act
        var result = await _service.VerifyAsync(@"C:\project");

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Projects);
        Assert.Empty(result.StepResults);
        Assert.Equal("No verification steps detected", result.Summary);
    }

    [Fact]
    public async Task VerifyAsync_AllRequiredStepsPass_ReturnsSuccess()
    {
        // Arrange
        var projects = new List<DetectedProject>
        {
            new()
            {
                ProjectType = "test",
                ProjectPath = @"C:\project\test.proj",
                ProjectName = "test",
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "echo",
                        Command = "cmd",
                        Arguments = ["/c", "echo", "hello"],
                        WorkingDirectory = @"C:\",
                        Required = true,
                        TimeoutSeconds = 10,
                    },
                ],
            },
        };
        _detector.DetectProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetectedProject>>(projects));

        // Act
        var result = await _service.VerifyAsync(@"C:\project");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.StepResults);
        Assert.True(result.StepResults[0].Success);
    }

    [Fact]
    public async Task VerifyAsync_OptionalStepFails_StillReturnsSuccess()
    {
        // Arrange
        var projects = new List<DetectedProject>
        {
            new()
            {
                ProjectType = "test",
                ProjectPath = @"C:\project\test.proj",
                ProjectName = "test",
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "fail",
                        Command = "cmd",
                        Arguments = ["/c", "exit", "1"],
                        WorkingDirectory = @"C:\",
                        Required = false, // Optional
                        TimeoutSeconds = 10,
                    },
                ],
            },
        };
        _detector.DetectProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetectedProject>>(projects));

        // Act
        var result = await _service.VerifyAsync(@"C:\project");

        // Assert
        Assert.True(result.Success); // Optional failure doesn't block
        Assert.Single(result.StepResults);
        Assert.False(result.StepResults[0].Success);
        Assert.False(result.StepResults[0].Required);
    }

    [Fact]
    public async Task VerifyAsync_RequiredStepFails_ReturnsFailure()
    {
        // Arrange
        var projects = new List<DetectedProject>
        {
            new()
            {
                ProjectType = "test",
                ProjectPath = @"C:\project\test.proj",
                ProjectName = "test",
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "fail",
                        Command = "cmd",
                        Arguments = ["/c", "exit", "1"],
                        WorkingDirectory = @"C:\",
                        Required = true,
                        TimeoutSeconds = 10,
                    },
                ],
            },
        };
        _detector.DetectProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetectedProject>>(projects));

        // Act
        var result = await _service.VerifyAsync(@"C:\project");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("1 required failures", result.Summary);
    }

    [Fact]
    public async Task RunStepAsync_CommandSucceeds_ReturnsSuccess()
    {
        // Arrange
        var step = new VerificationStep
        {
            StepType = "echo",
            Command = "cmd",
            Arguments = ["/c", "echo", "hello"],
            WorkingDirectory = @"C:\",
            Required = true,
            TimeoutSeconds = 10,
        };

        // Act
        var result = await _service.RunStepAsync(step);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunStepAsync_CommandFails_ReturnsFailureWithExitCode()
    {
        // Arrange
        var step = new VerificationStep
        {
            StepType = "fail",
            Command = "cmd",
            Arguments = ["/c", "exit", "42"],
            WorkingDirectory = @"C:\",
            Required = true,
            TimeoutSeconds = 10,
        };

        // Act
        var result = await _service.RunStepAsync(step);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(42, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunStepAsync_CommandNotFound_ReturnsFailure()
    {
        // Arrange
        var step = new VerificationStep
        {
            StepType = "missing",
            Command = "nonexistent-command-xyz123",
            Arguments = [],
            WorkingDirectory = @"C:\",
            Required = true,
            TimeoutSeconds = 10,
        };

        // Act
        var result = await _service.RunStepAsync(step);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public async Task VerificationResult_Summary_FormatsCorrectly()
    {
        // Arrange
        var projects = new List<DetectedProject>
        {
            new()
            {
                ProjectType = "test",
                ProjectPath = @"C:\test",
                ProjectName = "test",
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "pass1",
                        Command = "cmd",
                        Arguments = ["/c", "echo", "1"],
                        WorkingDirectory = @"C:\",
                        Required = true,
                        TimeoutSeconds = 5,
                    },
                    new VerificationStep
                    {
                        StepType = "pass2",
                        Command = "cmd",
                        Arguments = ["/c", "echo", "2"],
                        WorkingDirectory = @"C:\",
                        Required = true,
                        TimeoutSeconds = 5,
                    },
                ],
            },
        };
        _detector.DetectProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetectedProject>>(projects));

        // Act
        var result = await _service.VerifyAsync(@"C:\project");

        // Assert
        Assert.Equal("2/2 steps passed", result.Summary);
    }

    [Fact]
    public async Task VerificationStepResult_ErrorMessage_ReturnsAppropriateMessage()
    {
        // Arrange & Act
        var successResult = new VerificationStepResult
        {
            Step = new VerificationStep
            {
                StepType = "test",
                Command = "echo",
                Arguments = [],
                WorkingDirectory = @"C:\",
                TimeoutSeconds = 10,
            },
            Success = true,
            Required = true,
            ExitCode = 0,
        };

        var failResult = new VerificationStepResult
        {
            Step = new VerificationStep
            {
                StepType = "test",
                Command = "fail",
                Arguments = [],
                WorkingDirectory = @"C:\",
                TimeoutSeconds = 10,
            },
            Success = false,
            Required = true,
            ExitCode = 1,
            StandardError = "Build failed: missing reference",
        };

        var timeoutResult = new VerificationStepResult
        {
            Step = new VerificationStep
            {
                StepType = "test",
                Command = "sleep",
                Arguments = [],
                WorkingDirectory = @"C:\",
                TimeoutSeconds = 5,
            },
            Success = false,
            Required = true,
            ExitCode = -1,
            TimedOut = true,
        };

        // Assert
        Assert.Null(successResult.ErrorMessage);
        Assert.Equal("Build failed: missing reference", failResult.ErrorMessage);
        Assert.Contains("timed out", timeoutResult.ErrorMessage);
    }
}
