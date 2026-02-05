using System.IO.Abstractions.TestingHelpers;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Anvil.Cli.Tests.Services;

public class ScenarioLoaderTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ScenarioLoader _sut;

    public ScenarioLoaderTests()
    {
        _fileSystem = new MockFileSystem();
        _sut = new ScenarioLoader(_fileSystem, NullLogger<ScenarioLoader>.Instance);
    }

    [Fact]
    public async Task LoadAsync_WithValidYaml_ReturnsScenario()
    {
        // Arrange
        const string yaml = """
            name: add-validation
            description: Add input validation to a service
            language: csharp
            repository: c:/repos/sample-app
            story:
              title: Add input validation
              description: Add null checks and validation to the UserService
            expectations:
              - type: compiles
                description: Code should compile
              - type: tests_pass
                description: All tests should pass
            tags:
              - validation
              - beginner
            """;
        _fileSystem.AddFile("/scenarios/test.yaml", new MockFileData(yaml));

        // Act
        var result = await _sut.LoadAsync("/scenarios/test.yaml");

        // Assert
        result.Name.Should().Be("add-validation");
        result.Description.Should().Be("Add input validation to a service");
        result.Language.Should().Be("csharp");
        result.Repository.Should().Be("c:/repos/sample-app");
        result.Story.Title.Should().Be("Add input validation");
        result.Story.Description.Should().Be("Add null checks and validation to the UserService");
        result.Expectations.Should().HaveCount(2);
        result.Expectations[0].Type.Should().Be("compiles");
        result.Expectations[1].Type.Should().Be("tests_pass");
        result.Tags.Should().BeEquivalentTo(["validation", "beginner"]);
        result.FilePath.Should().Be("/scenarios/test.yaml");
    }

    [Fact]
    public async Task LoadAsync_WithMissingName_ThrowsValidationException()
    {
        // Arrange
        const string yaml = """
            description: Missing name field
            language: csharp
            repository: c:/repos/sample-app
            story:
              title: Test
              description: Test description
            expectations:
              - type: compiles
                description: Code should compile
            """;
        _fileSystem.AddFile("/scenarios/invalid.yaml", new MockFileData(yaml));

        // Act
        var act = () => _sut.LoadAsync("/scenarios/invalid.yaml");

        // Assert
        await act.Should().ThrowAsync<ScenarioValidationException>()
            .WithMessage("*name*");
    }

    [Fact]
    public async Task LoadAsync_WithInvalidYaml_ThrowsParseException()
    {
        // Arrange
        const string yaml = """
            name: test
            description: [invalid yaml
            """;
        _fileSystem.AddFile("/scenarios/malformed.yaml", new MockFileData(yaml));

        // Act
        var act = () => _sut.LoadAsync("/scenarios/malformed.yaml");

        // Assert
        await act.Should().ThrowAsync<ScenarioParseException>();
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ThrowsNotFoundException()
    {
        // Act
        var act = () => _sut.LoadAsync("/scenarios/nonexistent.yaml");

        // Assert
        await act.Should().ThrowAsync<ScenarioNotFoundException>()
            .WithMessage("*nonexistent.yaml*");
    }

    [Fact]
    public async Task LoadAllAsync_WithDirectory_ReturnsAllScenarios()
    {
        // Arrange
        const string yaml1 = """
            name: scenario-one
            description: First scenario
            language: csharp
            repository: c:/repos/app
            story:
              title: First
              description: First story
            expectations:
              - type: compiles
                description: Should compile
            """;
        const string yaml2 = """
            name: scenario-two
            description: Second scenario
            language: python
            repository: c:/repos/app2
            story:
              title: Second
              description: Second story
            expectations:
              - type: tests_pass
                description: Tests pass
            """;
        _fileSystem.AddFile("/scenarios/one.yaml", new MockFileData(yaml1));
        _fileSystem.AddFile("/scenarios/two.yaml", new MockFileData(yaml2));

        // Act
        var results = await _sut.LoadAllAsync("/scenarios");

        // Assert
        results.Should().HaveCount(2);
        results.Select(s => s.Name).Should().BeEquivalentTo(["scenario-one", "scenario-two"]);
    }

    [Fact]
    public async Task LoadAllAsync_WithNestedDirectories_ReturnsAllScenarios()
    {
        // Arrange
        const string yaml1 = """
            name: csharp-test
            description: C# test
            language: csharp
            repository: c:/repos/app
            story:
              title: CS Test
              description: C# story
            expectations:
              - type: compiles
                description: Should compile
            """;
        const string yaml2 = """
            name: python-test
            description: Python test
            language: python
            repository: c:/repos/app
            story:
              title: Py Test
              description: Python story
            expectations:
              - type: tests_pass
                description: Tests pass
            """;
        _fileSystem.AddFile("/scenarios/csharp/test.yaml", new MockFileData(yaml1));
        _fileSystem.AddFile("/scenarios/python/test.yaml", new MockFileData(yaml2));

        // Act
        var results = await _sut.LoadAllAsync("/scenarios");

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAllAsync_WithEmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        _fileSystem.AddDirectory("/scenarios");

        // Act
        var results = await _sut.LoadAllAsync("/scenarios");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithFileExistsExpectation_ParsesPath()
    {
        // Arrange
        const string yaml = """
            name: file-check
            description: Check file exists
            language: csharp
            repository: c:/repos/app
            story:
              title: Create file
              description: Create a new file
            expectations:
              - type: file_exists
                description: File should exist
                path: src/NewFile.cs
            """;
        _fileSystem.AddFile("/scenarios/test.yaml", new MockFileData(yaml));

        // Act
        var result = await _sut.LoadAsync("/scenarios/test.yaml");

        // Assert
        result.Expectations[0].Type.Should().Be("file_exists");
        result.Expectations[0].Path.Should().Be("src/NewFile.cs");
    }

    [Fact]
    public async Task LoadAsync_WithFileContainsExpectation_ParsesPattern()
    {
        // Arrange
        const string yaml = """
            name: pattern-check
            description: Check file contains pattern
            language: csharp
            repository: c:/repos/app
            story:
              title: Add method
              description: Add a method to class
            expectations:
              - type: file_contains
                description: Method should exist
                path: src/Service.cs
                pattern: public void DoSomething\(
            """;
        _fileSystem.AddFile("/scenarios/test.yaml", new MockFileData(yaml));

        // Act
        var result = await _sut.LoadAsync("/scenarios/test.yaml");

        // Assert
        result.Expectations[0].Type.Should().Be("file_contains");
        result.Expectations[0].Path.Should().Be("src/Service.cs");
        result.Expectations[0].Pattern.Should().Be(@"public void DoSomething\(");
    }

    [Fact]
    public async Task LoadAsync_WithCustomTimeout_ParsesTimeout()
    {
        // Arrange
        const string yaml = """
            name: slow-test
            description: A slow running test
            language: csharp
            repository: c:/repos/app
            timeoutSeconds: 600
            story:
              title: Complex refactor
              description: A complex refactoring task
            expectations:
              - type: compiles
                description: Should compile
            """;
        _fileSystem.AddFile("/scenarios/test.yaml", new MockFileData(yaml));

        // Act
        var result = await _sut.LoadAsync("/scenarios/test.yaml");

        // Assert
        result.TimeoutSeconds.Should().Be(600);
    }

    [Fact]
    public async Task LoadAsync_WithDefaultTimeout_Returns300()
    {
        // Arrange
        const string yaml = """
            name: normal-test
            description: Normal test
            language: csharp
            repository: c:/repos/app
            story:
              title: Simple task
              description: A simple task
            expectations:
              - type: compiles
                description: Should compile
            """;
        _fileSystem.AddFile("/scenarios/test.yaml", new MockFileData(yaml));

        // Act
        var result = await _sut.LoadAsync("/scenarios/test.yaml");

        // Assert
        result.TimeoutSeconds.Should().Be(300);
    }
}
