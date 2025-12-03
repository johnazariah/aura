// <copyright file="FSharpTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// F#-specific tools for the Developer module.
/// Uses shell commands to interact with F# tooling (dotnet, fantomas).
/// </summary>
public static class FSharpTools
{
    /// <summary>
    /// Registers all F# tools with the registry.
    /// </summary>
    public static void RegisterFSharpTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(CreateCheckProjectTool(processRunner, logger));
        registry.RegisterTool(CreateBuildTool(processRunner, logger));
        registry.RegisterTool(CreateFormatTool(processRunner, logger));
        registry.RegisterTool(CreateTestTool(processRunner, logger));
        registry.RegisterTool(CreateFsiTool(processRunner, logger));

        logger.LogInformation("Registered 5 F# tools");
    }

    private static ToolDefinition CreateCheckProjectTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "fsharp.check_project",
        Name = "Check F# Project",
        Description = "Runs 'dotnet build --no-restore' on an F# project to check for compilation errors. " +
                      "Faster than full build when packages are already restored.",
        Categories = ["fsharp", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "projectPath": { "type": "string", "description": "Path to the .fsproj file" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var projectPath = input.GetParameter<string?>("projectPath", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "build", "--no-restore", "--verbosity", "quiet" };
            if (!string.IsNullOrEmpty(projectPath))
            {
                args.Insert(1, projectPath);
            }

            var result = await runner.RunAsync("dotnet", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("F# project check completed with exit code {ExitCode}", result.ExitCode);

            var errors = ParseMsBuildErrors(result.StandardError + result.StandardOutput);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                errorCount = errors.Count,
                errors,
                output = result.StandardOutput,
            });
        },
    };

    private static ToolDefinition CreateBuildTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "fsharp.build",
        Name = "Build F# Project",
        Description = "Runs 'dotnet build' on an F# project. " +
                      "Returns compilation errors and warnings.",
        Categories = ["fsharp", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "projectPath": { "type": "string", "description": "Path to the .fsproj file" },
                "configuration": { "type": "string", "description": "Build configuration", "default": "Debug" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var projectPath = input.GetParameter<string?>("projectPath", null);
            var configuration = input.GetParameter("configuration", "Debug")!;
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "build" };
            if (!string.IsNullOrEmpty(projectPath))
            {
                args.Add(projectPath);
            }

            args.Add("--configuration");
            args.Add(configuration);

            var result = await runner.RunAsync("dotnet", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("F# build completed with exit code {ExitCode}", result.ExitCode);

            var errors = ParseMsBuildErrors(result.StandardError + result.StandardOutput);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                errorCount = errors.Count,
                errors,
                output = result.StandardOutput,
            });
        },
    };

    private static ToolDefinition CreateFormatTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "fsharp.format",
        Name = "Format F# Code",
        Description = "Runs Fantomas to format F# code. Fantomas is the standard F# formatter. " +
                      "F# is whitespace-sensitive, so proper formatting is critical.",
        Categories = ["fsharp", "formatting"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or directory to format" },
                "check": { "type": "boolean", "description": "Check only, don't modify", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            },
            "required": ["path"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var check = input.GetParameter("check", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            // Try fantomas as a dotnet tool first
            var args = check
                ? new[] { "fantomas", "--check", path }
                : new[] { "fantomas", path };

            var result = await runner.RunAsync("dotnet", args, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            // If dotnet tool not found, try global fantomas
            if (result.ExitCode != 0 && result.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                args = check ? ["--check", path] : [path];
                result = await runner.RunAsync("fantomas", args, new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            logger.LogDebug("Fantomas completed with exit code {ExitCode}", result.ExitCode);

            if (check)
            {
                return ToolResult.Ok(new
                {
                    needsFormatting = result.ExitCode != 0,
                    output = result.StandardOutput + result.StandardError,
                });
            }

            return result.ExitCode == 0
                ? ToolResult.Ok(new { formatted = true, output = result.StandardOutput })
                : ToolResult.Fail($"Format failed: {result.StandardError}");
        },
    };

    private static ToolDefinition CreateTestTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "fsharp.test",
        Name = "Run F# Tests",
        Description = "Runs 'dotnet test' on an F# test project. " +
                      "Supports common F# test frameworks: Expecto, FsUnit, Unquote.",
        Categories = ["fsharp", "testing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "projectPath": { "type": "string", "description": "Path to test project" },
                "filter": { "type": "string", "description": "Test filter expression" },
                "verbose": { "type": "boolean", "description": "Verbose output", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var projectPath = input.GetParameter<string?>("projectPath", null);
            var filter = input.GetParameter<string?>("filter", null);
            var verbose = input.GetParameter("verbose", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "test" };
            if (!string.IsNullOrEmpty(projectPath))
            {
                args.Add(projectPath);
            }

            if (!string.IsNullOrEmpty(filter))
            {
                args.Add("--filter");
                args.Add(filter);
            }

            if (verbose)
            {
                args.Add("--verbosity");
                args.Add("normal");
            }

            var result = await runner.RunAsync("dotnet", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("F# tests completed with exit code {ExitCode}", result.ExitCode);

            // Parse test results
            var (passed, failed, skipped) = ParseTestResults(result.StandardOutput);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                passed,
                failed,
                skipped,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateFsiTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "fsharp.fsi",
        Name = "Run F# Interactive",
        Description = "Runs F# code in F# Interactive (fsi). Useful for testing snippets, " +
                      "exploring types, and quick prototyping.",
        Categories = ["fsharp", "repl"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "script": { "type": "string", "description": "F# script code to execute" },
                "scriptPath": { "type": "string", "description": "Path to .fsx script file" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var script = input.GetParameter<string?>("script", null);
            var scriptPath = input.GetParameter<string?>("scriptPath", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            if (string.IsNullOrEmpty(script) && string.IsNullOrEmpty(scriptPath))
            {
                return ToolResult.Fail("Either 'script' or 'scriptPath' must be provided");
            }

            string[] args;
            if (!string.IsNullOrEmpty(scriptPath))
            {
                args = ["fsi", scriptPath];
            }
            else
            {
                // Write script to temp file and execute
                var tempFile = Path.GetTempFileName() + ".fsx";
                await File.WriteAllTextAsync(tempFile, script!, ct);
                args = ["fsi", tempFile];
            }

            var result = await runner.RunAsync("dotnet", args, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("F# Interactive completed with exit code {ExitCode}", result.ExitCode);

            return result.ExitCode == 0
                ? ToolResult.Ok(new { output = result.StandardOutput })
                : ToolResult.Fail($"FSI error: {result.StandardError}\n{result.StandardOutput}");
        },
    };

    #region Output Parsing

    private static List<string> ParseMsBuildErrors(string output)
    {
        var errors = new List<string>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (line.Contains(": error ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains(": error:", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(line.Trim());
            }
        }

        return errors;
    }

    private static (int passed, int failed, int skipped) ParseTestResults(string output)
    {
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        // Parse dotnet test output: "Passed: X, Failed: Y, Skipped: Z"
        var match = System.Text.RegularExpressions.Regex.Match(
            output,
            @"Passed:\s*(\d+).*Failed:\s*(\d+).*Skipped:\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            int.TryParse(match.Groups[1].Value, out passed);
            int.TryParse(match.Groups[2].Value, out failed);
            int.TryParse(match.Groups[3].Value, out skipped);
        }

        return (passed, failed, skipped);
    }

    #endregion
}
