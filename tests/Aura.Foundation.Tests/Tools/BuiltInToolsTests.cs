using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class BuiltInToolsTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;
    private readonly ToolRegistry _registry;

    public BuiltInToolsTests()
    {
        _fileSystem = new MockFileSystem();
        _processRunner = Substitute.For<IProcessRunner>();
        _logger = Substitute.For<ILogger>();
        _registry = new ToolRegistry(Substitute.For<ILogger<ToolRegistry>>());
        
        BuiltInTools.RegisterBuiltInTools(_registry, _fileSystem, _processRunner, _logger);
    }

    [Fact]
    public void RegisterBuiltInTools_RegistersAllTools()
    {
        // Assert
        Assert.True(_registry.HasTool("file.read"));
        Assert.True(_registry.HasTool("file.write"));
        Assert.True(_registry.HasTool("file.list"));
        Assert.True(_registry.HasTool("file.exists"));
        Assert.True(_registry.HasTool("file.delete"));
        Assert.True(_registry.HasTool("shell.execute"));
    }

    [Fact]
    public async Task FileRead_ReturnsFileContent()
    {
        // Arrange
        _fileSystem.AddFile("/test/file.txt", new MockFileData("Hello, World!"));
        var input = new ToolInput
        {
            ToolId = "file.read",
            Parameters = new Dictionary<string, object?> { ["path"] = "/test/file.txt" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Hello, World!", result.Output);
    }

    [Fact]
    public async Task FileRead_FailsForMissingFile()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "file.read",
            Parameters = new Dictionary<string, object?> { ["path"] = "/nonexistent.txt" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task FileWrite_CreatesFile()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "file.write",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/new-file.txt",
                ["content"] = "New content"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.True(_fileSystem.File.Exists("/test/new-file.txt"));
        Assert.Equal("New content", _fileSystem.File.ReadAllText("/test/new-file.txt"));
    }

    [Fact]
    public async Task FileWrite_AppendsContent()
    {
        // Arrange
        _fileSystem.AddFile("/test/append.txt", new MockFileData("Line 1\n"));
        var input = new ToolInput
        {
            ToolId = "file.write",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/append.txt",
                ["content"] = "Line 2",
                ["append"] = true
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        var content = _fileSystem.File.ReadAllText("/test/append.txt");
        Assert.Contains("Line 1", content);
        Assert.Contains("Line 2", content);
    }

    [Fact]
    public async Task FileList_ListsDirectoryContents()
    {
        // Arrange
        _fileSystem.AddFile("/mydir/file1.txt", new MockFileData(""));
        _fileSystem.AddFile("/mydir/file2.txt", new MockFileData(""));
        _fileSystem.AddDirectory("/mydir/subdir");
        
        var input = new ToolInput
        {
            ToolId = "file.list",
            Parameters = new Dictionary<string, object?> { ["path"] = "/mydir" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        var entries = result.Output as IEnumerable<object>;
        Assert.NotNull(entries);
        Assert.Equal(3, entries.Count());
    }

    [Fact]
    public async Task FileExists_ReturnsTrueForExistingFile()
    {
        // Arrange
        _fileSystem.AddFile("/exists.txt", new MockFileData(""));
        var input = new ToolInput
        {
            ToolId = "file.exists",
            Parameters = new Dictionary<string, object?> { ["path"] = "/exists.txt" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        // The output is an anonymous type, check via reflection or string representation
        var outputStr = result.Output.ToString();
        Assert.Contains("exists = True", outputStr);
        Assert.Contains("isFile = True", outputStr);
    }

    [Fact]
    public async Task FileExists_ReturnsFalseForMissing()
    {
        // Arrange
        var input = new ToolInput
        {
            ToolId = "file.exists",
            Parameters = new Dictionary<string, object?> { ["path"] = "/missing.txt" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        var outputStr = result.Output.ToString();
        Assert.Contains("exists = False", outputStr);
    }

    [Fact]
    public async Task FileDelete_DeletesFile()
    {
        // Arrange
        _fileSystem.AddFile("/delete-me.txt", new MockFileData(""));
        var input = new ToolInput
        {
            ToolId = "file.delete",
            Parameters = new Dictionary<string, object?> { ["path"] = "/delete-me.txt" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.False(_fileSystem.File.Exists("/delete-me.txt"));
    }

    [Fact]
    public async Task ShellExecute_CallsProcessRunner()
    {
        // Arrange
        _processRunner.RunAsync(
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<ProcessOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "command output",
                StandardError = ""
            });

        var input = new ToolInput
        {
            ToolId = "shell.execute",
            Parameters = new Dictionary<string, object?>
            {
                ["command"] = "echo",
                ["args"] = new[] { "hello" }
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        await _processRunner.Received(1).RunAsync(
            "echo",
            Arg.Is<string[]>(a => a.Contains("hello")),
            Arg.Any<ProcessOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShellExecute_ReturnsFailureOnNonZeroExit()
    {
        // Arrange
        _processRunner.RunAsync(
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<ProcessOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = "",
                StandardError = "command failed"
            });

        var input = new ToolInput
        {
            ToolId = "shell.execute",
            Parameters = new Dictionary<string, object?> { ["command"] = "failing-command" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("failed", result.Error);
    }
}
