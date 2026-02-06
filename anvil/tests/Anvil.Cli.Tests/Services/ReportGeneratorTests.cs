using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Anvil.Cli.Models;
using Anvil.Cli.Services;
using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Anvil.Cli.Tests.Services;

public class ReportGeneratorTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly TestConsole _console;
    private readonly ReportGenerator _sut;

    public ReportGeneratorTests()
    {
        _fileSystem = new MockFileSystem();
        _console = new TestConsole();
        _sut = new ReportGenerator(_console, _fileSystem);
    }

    private static Scenario CreateScenario(string name) => new()
    {
        Name = name,
        Description = $"Test scenario {name}",
        Language = "csharp",
        Repository = "/repos/test",
        Story = new StoryDefinition
        {
            Title = $"Test Story {name}",
            Description = "Test description"
        },
        Expectations =
        [
            new Expectation { Type = "compiles", Description = "Should compile" }
        ]
    };

    private static SuiteResult CreateSuiteResult(params StoryResult[] results) => new()
    {
        Results = results,
        TotalDuration = TimeSpan.FromSeconds(10),
        StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
        CompletedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task WriteConsoleReport_WithPasses_ShowsGreen()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("test-1"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5)
            },
            new StoryResult
            {
                Scenario = CreateScenario("test-2"),
                Success = true,
                Duration = TimeSpan.FromSeconds(3)
            }
        );

        // Act
        await _sut.WriteConsoleReportAsync(suiteResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("test-1");
        output.Should().Contain("test-2");
        output.Should().Contain("PASSED");
        output.Should().Contain("2 passed");
    }

    [Fact]
    public async Task WriteConsoleReport_WithFailures_ShowsRed()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("passing-test"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5)
            },
            new StoryResult
            {
                Scenario = CreateScenario("failing-test"),
                Success = false,
                Duration = TimeSpan.FromSeconds(3),
                Error = "Build failed"
            }
        );

        // Act
        await _sut.WriteConsoleReportAsync(suiteResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("failing-test");
        output.Should().Contain("FAILED");
        output.Should().Contain("1 failed");
        output.Should().Contain("1 passed");
    }

    [Fact]
    public async Task WriteJsonReport_WritesValidJson()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("test-1"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5),
                StoryId = Guid.NewGuid()
            }
        );
        const string outputPath = "/reports/test-report.json";
        _fileSystem.AddDirectory("/reports");

        // Act
        await _sut.WriteJsonReportAsync(suiteResult, outputPath);

        // Assert
        _fileSystem.File.Exists(outputPath).Should().BeTrue();
        var json = await _fileSystem.File.ReadAllTextAsync(outputPath);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("results").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WriteJsonReport_IncludesAllResults()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("test-1"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5),
                StoryId = Guid.NewGuid()
            },
            new StoryResult
            {
                Scenario = CreateScenario("test-2"),
                Success = false,
                Duration = TimeSpan.FromSeconds(3),
                Error = "Test error"
            },
            new StoryResult
            {
                Scenario = CreateScenario("test-3"),
                Success = true,
                Duration = TimeSpan.FromSeconds(2)
            }
        );
        const string outputPath = "/reports/full-report.json";
        _fileSystem.AddDirectory("/reports");

        // Act
        await _sut.WriteJsonReportAsync(suiteResult, outputPath);

        // Assert
        var json = await _fileSystem.File.ReadAllTextAsync(outputPath);
        var parsed = JsonDocument.Parse(json);

        var results = parsed.RootElement.GetProperty("results");
        results.GetArrayLength().Should().Be(3);

        // Check summary
        parsed.RootElement.GetProperty("passed").GetInt32().Should().Be(2);
        parsed.RootElement.GetProperty("failed").GetInt32().Should().Be(1);
        parsed.RootElement.GetProperty("total").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task WriteJsonReport_CreatesDirectoryIfNeeded()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("test-1"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5)
            }
        );
        const string outputPath = "/new-reports/subdir/report.json";

        // Act
        await _sut.WriteJsonReportAsync(suiteResult, outputPath);

        // Assert
        _fileSystem.File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteConsoleReport_ShowsSummaryTable()
    {
        // Arrange
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = CreateScenario("test-1"),
                Success = true,
                Duration = TimeSpan.FromSeconds(5.5)
            }
        );

        // Act
        await _sut.WriteConsoleReportAsync(suiteResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("5.5");
        output.Should().Contain("total");
    }

    [Fact]
    public async Task WriteConsoleReport_ShowsExpectationResults()
    {
        // Arrange
        var scenario = CreateScenario("test-with-expectations");
        var suiteResult = CreateSuiteResult(
            new StoryResult
            {
                Scenario = scenario,
                Success = false,
                Duration = TimeSpan.FromSeconds(5),
                ExpectationResults =
                [
                    new ExpectationResult
                    {
                        Expectation = scenario.Expectations[0],
                        Passed = false,
                        Message = "Compilation failed"
                    }
                ]
            }
        );

        // Act
        await _sut.WriteConsoleReportAsync(suiteResult);

        // Assert
        var output = _console.Output;
        output.Should().Contain("Compilation failed");
    }
}
