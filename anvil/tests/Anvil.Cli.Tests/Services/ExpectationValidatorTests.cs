using System.IO.Abstractions.TestingHelpers;
using Anvil.Cli.Models;
using Anvil.Cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Anvil.Cli.Tests.Services;

public class ExpectationValidatorTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IIndexEffectivenessAnalyzer _indexAnalyzer;
    private readonly ExpectationValidator _sut;

    public ExpectationValidatorTests()
    {
        _fileSystem = new MockFileSystem();
        _indexAnalyzer = Substitute.For<IIndexEffectivenessAnalyzer>();
        _sut = new ExpectationValidator(_fileSystem, _indexAnalyzer, NullLogger<ExpectationValidator>.Instance);
    }

    private static Scenario CreateScenario(params Expectation[] expectations) => new()
    {
        Name = "test",
        Description = "Test scenario",
        Language = "csharp",
        Repository = "/repos/test",
        Story = new StoryDefinition
        {
            Title = "Test",
            Description = "Test story"
        },
        Expectations = expectations
    };

    [Fact]
    public async Task ValidateAsync_Compiles_WhenCompleted_Passes()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "compiles",
            Description = "Code should compile"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeTrue();
        results[0].Message.Should().Contain("Completed");
    }

    [Fact]
    public async Task ValidateAsync_Compiles_WhenFailed_Fails()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "compiles",
            Description = "Code should compile"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Failed",
            Error = "Build failed"
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("Build failed");
    }

    [Fact]
    public async Task ValidateAsync_TestsPass_WhenNoFailures_Passes()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "tests_pass",
            Description = "All tests should pass"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            Steps =
            [
                new StepResponse { Id = Guid.NewGuid(), Order = 1, Name = "Run tests", Status = "Completed" }
            ]
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_TestsPass_WhenStepFailed_Fails()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "tests_pass",
            Description = "All tests should pass"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            Steps =
            [
                new StepResponse
                {
                    Id = Guid.NewGuid(),
                    Order = 1,
                    Name = "Run tests",
                    Status = "Failed",
                    Error = "Test failed: MyTest"
                }
            ]
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("Test failed");
    }

    [Fact]
    public async Task ValidateAsync_FileExists_WhenPresent_Passes()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "file_exists",
            Description = "File should exist",
            Path = "src/NewFile.cs"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };
        _fileSystem.AddFile("/worktrees/test/src/NewFile.cs", new MockFileData("// content"));

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_FileExists_WhenMissing_Fails()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "file_exists",
            Description = "File should exist",
            Path = "src/MissingFile.cs"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateAsync_FileContains_WhenMatches_Passes()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "file_contains",
            Description = "Method should exist",
            Path = "src/Service.cs",
            Pattern = @"public void DoSomething\("
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };
        _fileSystem.AddFile("/worktrees/test/src/Service.cs", new MockFileData("""
            public class Service
            {
                public void DoSomething()
                {
                }
            }
            """));

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_FileContains_WhenNoMatch_Fails()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "file_contains",
            Description = "Method should exist",
            Path = "src/Service.cs",
            Pattern = @"public void MissingMethod\("
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };
        _fileSystem.AddFile("/worktrees/test/src/Service.cs", new MockFileData("""
            public class Service
            {
                public void DoSomething()
                {
                }
            }
            """));

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("Pattern not found");
    }

    [Fact]
    public async Task ValidateAsync_FileContains_WhenFileMissing_Fails()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "file_contains",
            Description = "Method should exist",
            Path = "src/MissingFile.cs",
            Pattern = @"anything"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateAsync_MultipleExpectations_ValidatesAll()
    {
        // Arrange
        var scenario = CreateScenario(
            new Expectation { Type = "compiles", Description = "Should compile" },
            new Expectation { Type = "file_exists", Description = "File exists", Path = "src/File.cs" }
        );
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed",
            WorktreePath = "/worktrees/test"
        };
        _fileSystem.AddFile("/worktrees/test/src/File.cs", new MockFileData("// content"));

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Passed.Should().BeTrue());
    }

    [Fact]
    public async Task ValidateAsync_UnknownExpectationType_FailsWithMessage()
    {
        // Arrange
        var scenario = CreateScenario(new Expectation
        {
            Type = "unknown_type",
            Description = "Unknown expectation"
        });
        var story = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Status = "Completed"
        };

        // Act
        var results = await _sut.ValidateAsync(scenario, story);

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Message.Should().Contain("Unknown expectation type");
    }
}
