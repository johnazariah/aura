// <copyright file="QualityGateService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs quality gates (build, test) between orchestrator waves.
/// </summary>
public sealed partial class QualityGateService : IQualityGateService
{
    private readonly ILogger<QualityGateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityGateService"/> class.
    /// </summary>
    public QualityGateService(ILogger<QualityGateService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QualityGateResult> RunBuildGateAsync(string worktreePath, int afterWave, CancellationToken ct = default)
    {
        _logger.LogInformation("Running build gate after wave {Wave} in {Path}", afterWave, worktreePath);

        // Normalize line endings (Copilot CLI creates CRLF, project requires LF)
        NormalizeLineEndings(worktreePath);

        // Detect project type and run appropriate build command
        var (command, args) = DetectBuildCommand(worktreePath);

        // For .NET projects, run restore first (LocalSystem may not have package cache access)
        if (command.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Running restore before build...");
            // Don't use --source flag as it interferes with SDK workload resolution (e.g., Aspire SDK)
            var (restoreCode, restoreOutput) = await RunCommandAsync(
                command,
                "restore",
                worktreePath,
                ct);
            if (restoreCode != 0)
            {
                _logger.LogWarning("Restore failed:\n{Output}", restoreOutput);
                return new QualityGateResult
                {
                    Passed = false,
                    GateType = "build",
                    AfterWave = afterWave,
                    BuildOutput = restoreOutput,
                    Error = $"Restore failed with exit code {restoreCode}",
                };
            }
        }

        _logger.LogInformation("Running build command: {Command} {Args}", command, args);
        var (exitCode, output) = await RunCommandAsync(command, args, worktreePath, ct);

        var passed = exitCode == 0;
        _logger.LogInformation("Build gate {Result} after wave {Wave}", passed ? "passed" : "failed", afterWave);
        if (!passed)
        {
            _logger.LogWarning("Build output:\n{Output}", output);
        }

        return new QualityGateResult
        {
            Passed = passed,
            GateType = "build",
            AfterWave = afterWave,
            BuildOutput = output,
            Error = passed ? null : $"Build failed with exit code {exitCode}",
        };
    }

    /// <inheritdoc/>
    public async Task<QualityGateResult> RunTestGateAsync(string worktreePath, int afterWave, CancellationToken ct = default)
    {
        _logger.LogInformation("Running test gate after wave {Wave} in {Path}", afterWave, worktreePath);

        // Detect project type and run appropriate test command
        var (command, args) = DetectTestCommand(worktreePath);

        var (exitCode, output) = await RunCommandAsync(command, args, worktreePath, ct);

        var passed = exitCode == 0;

        // Try to parse test counts from output
        var (testsPassed, testsFailed) = ParseTestCounts(output);

        _logger.LogInformation(
            "Test gate {Result} after wave {Wave}: {Passed} passed, {Failed} failed",
            passed ? "passed" : "failed", afterWave, testsPassed, testsFailed);

        return new QualityGateResult
        {
            Passed = passed,
            GateType = "test",
            AfterWave = afterWave,
            TestOutput = output,
            TestsPassed = testsPassed,
            TestsFailed = testsFailed,
            Error = passed ? null : $"Tests failed with exit code {exitCode}",
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
            // Include restore since worktree may not have packages restored
            return (dotnetPath, "build -v q");
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
            return (dotnetPath, "test --no-build -v q");
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

    private async Task<(int ExitCode, string Output)> RunCommandAsync(
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

            return (process.ExitCode, outputBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run {Command} {Args}", command, arguments);
            return (-1, ex.Message);
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
