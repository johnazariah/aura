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
        Assert.True(_registry.HasTool("file.modify"));
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
    public async Task FileRead_WithLineRange_ReturnsSelectedLines()
    {
        // Arrange
        _fileSystem.AddFile("/test/lines.txt", new MockFileData("Line 1\nLine 2\nLine 3\nLine 4\nLine 5"));
        var input = new ToolInput
        {
            ToolId = "file.read",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/lines.txt",
                ["startLine"] = 2,
                ["endLine"] = 4
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        var output = result.Output?.ToString() ?? "";
        Assert.Contains("Line 2", output);
        Assert.Contains("Line 3", output);
        Assert.Contains("Line 4", output);
        Assert.DoesNotContain("Line 1", output);
        Assert.DoesNotContain("Line 5", output);
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
    public async Task FileWrite_OverwritesExistingFile()
    {
        // Arrange
        _fileSystem.AddFile("/test/existing.txt", new MockFileData("Original content"));
        var input = new ToolInput
        {
            ToolId = "file.write",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/existing.txt",
                ["content"] = "New content",
                ["overwrite"] = true
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("New content", _fileSystem.File.ReadAllText("/test/existing.txt"));
    }

    [Fact]
    public async Task FileWrite_FailsWhenOverwriteFalse()
    {
        // Arrange
        _fileSystem.AddFile("/test/existing.txt", new MockFileData("Original content"));
        var input = new ToolInput
        {
            ToolId = "file.write",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/existing.txt",
                ["content"] = "New content",
                ["overwrite"] = false
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task FileModify_ReplacesText()
    {
        // Arrange
        _fileSystem.AddFile("/test/modify.txt", new MockFileData("Hello World!"));
        var input = new ToolInput
        {
            ToolId = "file.modify",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/modify.txt",
                ["oldText"] = "World",
                ["newText"] = "Universe"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Hello Universe!", _fileSystem.File.ReadAllText("/test/modify.txt"));
    }

    [Fact]
    public async Task FileModify_FailsWhenTextNotFound()
    {
        // Arrange
        _fileSystem.AddFile("/test/modify.txt", new MockFileData("Hello World!"));
        var input = new ToolInput
        {
            ToolId = "file.modify",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "/test/modify.txt",
                ["oldText"] = "NotFound",
                ["newText"] = "Replacement"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
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
            WorkingDirectory = "/mydir",  // Set working directory so relative paths work
            Parameters = new Dictionary<string, object?> { ["path"] = "." }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success);
        var output = result.Output?.ToString() ?? "";
        Assert.Contains("file1.txt", output);
        Assert.Contains("file2.txt", output);
        Assert.Contains("subdir/", output);
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
        var outputStr = result.Output.ToString() ?? "";
        Assert.Contains("File exists", outputStr);
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
        var outputStr = result.Output.ToString() ?? "";
        Assert.Contains("Does not exist", outputStr);
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
        _processRunner.RunShellAsync(
            Arg.Any<string>(),
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
                ["command"] = "echo hello"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error}");
        await _processRunner.Received(1).RunShellAsync(
            "echo hello",
            Arg.Any<ProcessOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShellExecute_ReturnsFailureOnNonZeroExit()
    {
        // Arrange
        _processRunner.RunShellAsync(
            Arg.Any<string>(),
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

    [Fact]
    public async Task FileRead_UsesWorkingDirectory()
    {
        // Arrange
        _fileSystem.AddFile("/workspace/README.md", new MockFileData("# My Project"));
        var input = new ToolInput
        {
            ToolId = "file.read",
            WorkingDirectory = "/workspace",
            Parameters = new Dictionary<string, object?> { ["path"] = "README.md" }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Error}");
        Assert.Equal("# My Project", result.Output);
    }

    [Fact]
    public async Task FileWrite_UsesWorkingDirectory()
    {
        // Arrange
        _fileSystem.AddDirectory("/workspace");
        var input = new ToolInput
        {
            ToolId = "file.write",
            WorkingDirectory = "/workspace",
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = "output.txt",
                ["content"] = "Written via relative path"
            }
        };

        // Act
        var result = await _registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Error}");
        Assert.True(_fileSystem.File.Exists("/workspace/output.txt"));
    }
}
