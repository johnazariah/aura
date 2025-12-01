using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests.Shell;

public class ProcessRunnerTests
{
    private readonly ILogger<ProcessRunner> _logger;
    private readonly ProcessRunner _runner;

    public ProcessRunnerTests()
    {
        _logger = Substitute.For<ILogger<ProcessRunner>>();
        _runner = new ProcessRunner(_logger);
    }

    [Fact]
    public void GetDefaultShell_ReturnsValidShell()
    {
        // Act
        var shell = _runner.GetDefaultShell();

        // Assert
        Assert.NotNull(shell);
        Assert.NotEmpty(shell.Path);
        Assert.NotEmpty(shell.CommandArg);
        Assert.NotEmpty(shell.Name);
    }

    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsOutput()
    {
        // Arrange - use cross-platform echo
        var isWindows = OperatingSystem.IsWindows();
        var command = isWindows ? "cmd.exe" : "echo";
        var args = isWindows ? new[] { "/c", "echo", "hello" } : new[] { "hello" };

        // Act
        var result = await _runner.RunAsync(command, args);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_InvalidCommand_ReturnsFailure()
    {
        // Act
        var result = await _runner.RunAsync("nonexistent-command-12345", []);

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_WithTimeout_TimesOutLongProcess()
    {
        // Arrange - command that takes a while
        var isWindows = OperatingSystem.IsWindows();
        var command = isWindows ? "cmd.exe" : "sleep";
        var args = isWindows ? new[] { "/c", "ping", "localhost", "-n", "10" } : new[] { "10" };

        // Act
        var result = await _runner.RunAsync(command, args, new ProcessOptions
        {
            Timeout = TimeSpan.FromMilliseconds(100)
        });

        // Assert
        Assert.True(result.TimedOut);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RunAsync_WithWorkingDirectory_UsesCorrectDirectory()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var isWindows = OperatingSystem.IsWindows();
        var command = isWindows ? "cmd.exe" : "pwd";
        var args = isWindows ? new[] { "/c", "cd" } : Array.Empty<string>();

        // Act
        var result = await _runner.RunAsync(command, args, new ProcessOptions
        {
            WorkingDirectory = tempDir
        });

        // Assert
        Assert.True(result.Success);
        // Normalize paths for comparison
        var outputPath = result.StandardOutput.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPath = tempDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.Equal(expectedPath, outputPath, ignoreCase: isWindows);
    }

    [Fact]
    public async Task RunShellAsync_ExecutesShellCommand()
    {
        // Arrange
        var isWindows = OperatingSystem.IsWindows();
        var shellCommand = isWindows ? "echo hello" : "echo hello";

        // Act
        var result = await _runner.RunShellAsync(shellCommand);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_CapturesStderr()
    {
        // Arrange - command that writes to stderr
        var isWindows = OperatingSystem.IsWindows();
        var command = isWindows ? "cmd.exe" : "sh";
        var args = isWindows 
            ? new[] { "/c", "echo error message 1>&2" }
            : new[] { "-c", "echo error message >&2" };

        // Act
        var result = await _runner.RunAsync(command, args);

        // Assert (exit code is 0 even with stderr output)
        Assert.Contains("error", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
