// <copyright file="QualityGateService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Result of running an external command.
/// </summary>
internal sealed record CommandResult
{
    /// <summary>Gets the exit code (0 = success, -1 = cancelled/error).</summary>
    public required int ExitCode { get; init; }

    /// <summary>Gets the combined stdout/stderr output.</summary>
    public required string Output { get; init; }

    /// <summary>Gets whether the command was cancelled before completion.</summary>
    public bool WasCancelled { get; init; }
}

/// <summary>
/// Runs quality gates (build, test) between orchestrator waves.
/// </summary>
public sealed partial class QualityGateService : IQualityGateService
{
    /// <summary>
    /// Shared NuGet package cache used by the service account.
    /// The service runs as a dedicated user (e.g., .\AuraService) whose profile
    /// may not have a populated NuGet cache. We use a shared location under ProgramData
    /// so that packages restored once are available to all worktree builds.
    /// </summary>
    private static readonly string SharedNuGetCache = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Aura", "nuget-cache");

    private readonly ILogger<QualityGateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityGateService"/> class.
    /// </summary>
    public QualityGateService(ILogger<QualityGateService> logger)
    {
        _logger = logger;
        // Ensure shared NuGet cache directory exists
        Directory.CreateDirectory(SharedNuGetCache);
    }

    /// <inheritdoc/>
    public async Task<QualityGateResult> RunBuildGateAsync(string worktreePath, int afterWave, CancellationToken ct = default)
    {
        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation("[{WorktreeName}] Running build gate after wave {Wave}", worktreeName, afterWave);

        // Normalize line endings (Copilot CLI creates CRLF, project requires LF)
        NormalizeLineEndings(worktreePath);

        // Detect project type and run appropriate build command
        var (command, args) = DetectBuildCommand(worktreePath);

        // For .NET projects, run restore first to ensure packages are available
        if (command.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[{WorktreeName}] Running restore before build (cache: {Cache})...", worktreeName, SharedNuGetCache);

            // Use shared NuGet cache so the service account doesn't need its own populated cache.
            // This also avoids re-downloading packages for every worktree.
            var restoreResult = await RunCommandAsync(
                command,
                $"restore --packages \"{SharedNuGetCache}\"",
                worktreePath,
                ct);

            if (restoreResult.WasCancelled)
            {
                _logger.LogWarning("[{WorktreeName}] Restore was cancelled", worktreeName);
                return new QualityGateResult
                {
                    Passed = false,
                    GateType = "build",
                    AfterWave = afterWave,
                    BuildOutput = restoreResult.Output,
                    Error = "Restore was cancelled",
                    WasCancelled = true,
                };
            }

            if (restoreResult.ExitCode != 0)
            {
                _logger.LogWarning("[{WorktreeName}] Restore failed:\n{Output}", worktreeName, restoreResult.Output);
                return new QualityGateResult
                {
                    Passed = false,
                    GateType = "build",
                    AfterWave = afterWave,
                    BuildOutput = restoreResult.Output,
                    Error = $"Restore failed with exit code {restoreResult.ExitCode}",
                };
            }
        }

        _logger.LogInformation("[{WorktreeName}] Running build command: {Command} {Args}", worktreeName, command, args);
        var buildResult = await RunCommandAsync(command, args, worktreePath, ct);

        if (buildResult.WasCancelled)
        {
            _logger.LogWarning("[{WorktreeName}] Build was cancelled", worktreeName);
            return new QualityGateResult
            {
                Passed = false,
                GateType = "build",
                AfterWave = afterWave,
                BuildOutput = buildResult.Output,
                Error = "Build was cancelled",
                WasCancelled = true,
            };
        }

        var passed = buildResult.ExitCode == 0;
        _logger.LogInformation("[{WorktreeName}] Build gate {Result} after wave {Wave}", worktreeName, passed ? "passed" : "failed", afterWave);
        if (!passed)
        {
            _logger.LogWarning("[{WorktreeName}] Build output:\n{Output}", worktreeName, buildResult.Output);
        }

        return new QualityGateResult
        {
            Passed = passed,
            GateType = "build",
            AfterWave = afterWave,
            BuildOutput = buildResult.Output,
            Error = passed ? null : $"Build failed with exit code {buildResult.ExitCode}",
        };
    }

    /// <inheritdoc/>
    public async Task<QualityGateResult> RunTestGateAsync(string worktreePath, int afterWave, CancellationToken ct = default)
    {
        var worktreeName = Path.GetFileName(worktreePath);
        _logger.LogInformation("[{WorktreeName}] Running test gate after wave {Wave}", worktreeName, afterWave);

        // Detect project type and run appropriate test command
        var (command, args) = DetectTestCommand(worktreePath);

        var testResult = await RunCommandAsync(command, args, worktreePath, ct);

        if (testResult.WasCancelled)
        {
            _logger.LogWarning("[{WorktreeName}] Tests were cancelled", worktreeName);
            return new QualityGateResult
            {
                Passed = false,
                GateType = "test",
                AfterWave = afterWave,
                TestOutput = testResult.Output,
                Error = "Tests were cancelled",
                WasCancelled = true,
            };
        }

        var passed = testResult.ExitCode == 0;

        // Try to parse test counts from output
        var (testsPassed, testsFailed) = ParseTestCounts(testResult.Output);

        _logger.LogInformation(
            "[{WorktreeName}] Test gate {Result} after wave {Wave}: {Passed} passed, {Failed} failed",
            worktreeName, passed ? "passed" : "failed", afterWave, testsPassed, testsFailed);

        return new QualityGateResult
        {
            Passed = passed,
            GateType = "test",
            AfterWave = afterWave,
            TestOutput = testResult.Output,
            TestsPassed = testsPassed,
            TestsFailed = testsFailed,
            Error = passed ? null : $"Tests failed with exit code {testResult.ExitCode}",
        };
    }

    /// <inheritdoc/>
    public async Task<QualityGateResult> RunFullGateAsync(string worktreePath, int afterWave, CancellationToken ct = default)
    {
        // Run build first
        var buildResult = await RunBuildGateAsync(worktreePath, afterWave, ct);
        if (!buildResult.Passed)
        {
            return buildResult;
        }

        // Then run tests
        var testResult = await RunTestGateAsync(worktreePath, afterWave, ct);

        // Combine results
        return new QualityGateResult
        {
            Passed = testResult.Passed,
            GateType = "full",
            AfterWave = afterWave,
            BuildOutput = buildResult.BuildOutput,
            TestOutput = testResult.TestOutput,
            TestsPassed = testResult.TestsPassed,
            TestsFailed = testResult.TestsFailed,
            Error = testResult.Error,
        };
    }

    private static (string Command, string Args) DetectBuildCommand(string worktreePath)
    {
        // Check for .NET project
        if (Directory.GetFiles(worktreePath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(worktreePath, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
        {
            // Use full path to dotnet since service may run as LocalSystem without PATH
            var dotnetPath = FindDotnetPath() ?? "dotnet";

            // Build with --no-restore since we do restore separately with proper cache config
            return (dotnetPath, "build --no-restore -v q");
        }

        // Check for Node.js project
        if (File.Exists(Path.Combine(worktreePath, "package.json")))
        {
            return ("npm", "run build");
        }

        // Check for Python project
        if (File.Exists(Path.Combine(worktreePath, "pyproject.toml")) ||
            File.Exists(Path.Combine(worktreePath, "setup.py")))
        {
            return ("python", "-m py_compile .");
        }

        // Check for Rust project
        if (File.Exists(Path.Combine(worktreePath, "Cargo.toml")))
        {
            return ("cargo", "build");
        }

        // Check for Go project
        if (File.Exists(Path.Combine(worktreePath, "go.mod")))
        {
            return ("go", "build ./...");
        }

        // Default: try make
        return ("make", "build");
    }

    private static (string Command, string Args) DetectTestCommand(string worktreePath)
    {
        // Check for .NET project
        if (Directory.GetFiles(worktreePath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(worktreePath, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
        {
            var dotnetPath = FindDotnetPath() ?? "dotnet";
            // Run only unit tests, exclude integration tests that require infrastructure
            return (dotnetPath, "test --no-build -v q --filter \"FullyQualifiedName!~IntegrationTests\"");
        }

        // Check for Node.js project
        if (File.Exists(Path.Combine(worktreePath, "package.json")))
        {
            return ("npm", "test");
        }

        // Check for Python project
        if (File.Exists(Path.Combine(worktreePath, "pyproject.toml")) ||
            File.Exists(Path.Combine(worktreePath, "setup.py")))
        {
            return ("pytest", "");
        }

        // Check for Rust project
        if (File.Exists(Path.Combine(worktreePath, "Cargo.toml")))
        {
            return ("cargo", "test");
        }

        // Check for Go project
        if (File.Exists(Path.Combine(worktreePath, "go.mod")))
        {
            return ("go", "test ./...");
        }

        // Default: try make
        return ("make", "test");
    }

    private static (int Passed, int Failed) ParseTestCounts(string output)
    {
        // Try to parse .NET test output: "Passed: X, Failed: Y"
        var dotnetMatch = DotNetTestResultsRegex().Match(output);
        if (dotnetMatch.Success)
        {
            int.TryParse(dotnetMatch.Groups[1].Value, out var passed);
            int.TryParse(dotnetMatch.Groups[2].Value, out var failed);
            return (passed, failed);
        }

        // Try to parse pytest output: "X passed, Y failed"
        var pytestMatch = PytestResultsRegex().Match(output);
        if (pytestMatch.Success)
        {
            int.TryParse(pytestMatch.Groups[1].Value, out var passed);
            int.TryParse(pytestMatch.Groups[2].Value, out var failed);
            return (passed, failed);
        }

        return (0, 0);
    }

    [GeneratedRegex(@"Passed:\s*(\d+).*Failed:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetTestResultsRegex();

    [GeneratedRegex(@"(\d+)\s+passed.*?(\d+)\s+failed", RegexOptions.IgnoreCase)]
    private static partial Regex PytestResultsRegex();

    private async Task<CommandResult> RunCommandAsync(
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Point all dotnet commands at the shared NuGet cache so build/test
        // find the packages that were restored earlier.
        psi.Environment["NUGET_PACKAGES"] = SharedNuGetCache;

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                Output = outputBuilder.ToString(),
                WasCancelled = false,
            };
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - kill the process and report cancellation
            _logger.LogWarning("Command cancelled: {Command} {Args}", command, arguments);

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogInformation("Killed process tree for {Command}", command);
                }
            }
            catch (Exception killEx)
            {
                _logger.LogWarning(killEx, "Failed to kill process {Command}", command);
            }

            return new CommandResult
            {
                ExitCode = -1,
                Output = outputBuilder.ToString() + "\n[Cancelled]",
                WasCancelled = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run {Command} {Args}", command, arguments);
            return new CommandResult
            {
                ExitCode = -1,
                Output = ex.Message,
                WasCancelled = false,
            };
        }
    }

    private static string? FindDotnetPath()
    {
        // Common .NET SDK installation paths
        string[] candidatePaths =
        [
            @"C:\Program Files\dotnet\dotnet.exe",
            @"C:\Program Files (x86)\dotnet\dotnet.exe",
            "/usr/local/share/dotnet/dotnet",
            "/usr/share/dotnet/dotnet",
            "/opt/dotnet/dotnet",
        ];

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void NormalizeLineEndings(string worktreePath)
    {
        // Normalize CRLF to LF for source files (Copilot CLI creates CRLF, project requires LF)
        var extensions = new[] { "*.cs", "*.json", "*.yaml", "*.yml", "*.md", "*.ts", "*.tsx", "*.js", "*.jsx" };
        var filesToNormalize = new List<string>();

        foreach (var ext in extensions)
        {
            filesToNormalize.AddRange(Directory.GetFiles(worktreePath, ext, SearchOption.AllDirectories));
        }

        var normalizedCount = 0;
        foreach (var file in filesToNormalize)
        {
            // Skip files in bin, obj, node_modules, .git
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("\r\n"))
                {
                    var normalized = content.Replace("\r\n", "\n");
                    File.WriteAllText(file, normalized);
                    normalizedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to normalize line endings in {File}", file);
            }
        }

        if (normalizedCount > 0)
        {
            _logger.LogInformation("Normalized line endings in {Count} files", normalizedCount);
        }
    }
}
