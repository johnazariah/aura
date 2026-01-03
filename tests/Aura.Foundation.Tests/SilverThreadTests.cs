using System.IO.Abstractions;
using Aura.Foundation.Git;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aura.Foundation.Tests;

/// <summary>
/// Silver thread tests - verify end-to-end flows work correctly.
/// </summary>
public class SilverThreadTests
{
    [Fact]
    public async Task ToolRegistry_WithProcessRunner_ExecutesShellCommands()
    {
        // Arrange - wire up real services
        var processLogger = Substitute.For<ILogger<ProcessRunner>>();
        var toolLogger = Substitute.For<ILogger<ToolRegistry>>();
        var builtinLogger = Substitute.For<ILogger>();

        var processRunner = new ProcessRunner(processLogger);
        var fileSystem = new FileSystem();
        var registry = new ToolRegistry(toolLogger);

        BuiltInTools.RegisterBuiltInTools(registry, fileSystem, processRunner, builtinLogger);

        // Act - execute a real shell command (use Write-Output for PowerShell compatibility)
        var isWindows = OperatingSystem.IsWindows();
        var shellCommand = isWindows ? "Write-Output 'silver-thread-test'" : "echo silver-thread-test";

        var input = new ToolInput
        {
            ToolId = "shell.execute",
            Parameters = new Dictionary<string, object?>
            {
                ["command"] = shellCommand,
                ["timeoutSeconds"] = 30
            }
        };

        var result = await registry.ExecuteAsync(input);

        // Assert
        Assert.True(result.Success, $"Tool failed: {result.Error}");
        Assert.True(result.Duration.TotalMilliseconds > 0);
        // Verify output contains our marker
        var outputStr = result.Output?.ToString() ?? "";
        Assert.Contains("silver-thread-test", outputStr);
    }

    [Fact]
    public async Task FileTools_ReadWriteRoundtrip_Works()
    {
        // Arrange
        var processLogger = Substitute.For<ILogger<ProcessRunner>>();
        var toolLogger = Substitute.For<ILogger<ToolRegistry>>();
        var builtinLogger = Substitute.For<ILogger>();

        var processRunner = new ProcessRunner(processLogger);
        var fileSystem = new FileSystem();
        var registry = new ToolRegistry(toolLogger);

        BuiltInTools.RegisterBuiltInTools(registry, fileSystem, processRunner, builtinLogger);

        var tempFile = Path.Combine(Path.GetTempPath(), $"aura-test-{Guid.NewGuid()}.txt");
        var testContent = $"Silver thread test content: {DateTime.UtcNow}";

        try
        {
            // Act - Write
            var writeInput = new ToolInput
            {
                ToolId = "file.write",
                Parameters = new Dictionary<string, object?>
                {
                    ["path"] = tempFile,
                    ["content"] = testContent
                }
            };
            var writeResult = await registry.ExecuteAsync(writeInput);
            Assert.True(writeResult.Success, $"Write failed: {writeResult.Error}");

            // Act - Read
            var readInput = new ToolInput
            {
                ToolId = "file.read",
                Parameters = new Dictionary<string, object?> { ["path"] = tempFile }
            };
            var readResult = await registry.ExecuteAsync(readInput);

            // Assert
            Assert.True(readResult.Success, $"Read failed: {readResult.Error}");
            Assert.Equal(testContent, readResult.Output);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessRunner_GitVersion_Works()
    {
        // This test verifies git is available (required for git worktree functionality)
        var logger = Substitute.For<ILogger<ProcessRunner>>();
        var runner = new ProcessRunner(logger);

        // Act
        var result = await runner.RunAsync("git", ["--version"]);

        // Assert
        Assert.True(result.Success, $"Git not available: {result.StandardError}");
        Assert.Contains("git version", result.StandardOutput);
    }

    [Fact]
    public async Task GitService_GetStatus_WorksOnRealRepo()
    {
        // Arrange - use the aura repo itself
        var processLogger = Substitute.For<ILogger<ProcessRunner>>();
        var gitLogger = Substitute.For<ILogger<GitService>>();

        var runner = new ProcessRunner(processLogger);
        var gitService = new GitService(runner, gitLogger);

        var repoPath = FindRepoRoot();
        if (repoPath == null)
        {
            // Skip if not running from a git repo
            return;
        }

        // Act
        var isRepo = await gitService.IsRepositoryAsync(repoPath);
        var statusResult = await gitService.GetStatusAsync(repoPath);

        // Assert
        Assert.True(isRepo, "Expected to find a git repository");
        Assert.True(statusResult.Success, $"Git status failed: {statusResult.Error}");
        Assert.NotNull(statusResult.Value);
        Assert.NotEmpty(statusResult.Value.CurrentBranch);
    }

    private static string? FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
