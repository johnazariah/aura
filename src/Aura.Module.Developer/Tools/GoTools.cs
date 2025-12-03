// <copyright file="GoTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Go-specific tools for the Developer module.
/// Uses shell commands to interact with Go tooling.
/// </summary>
public static class GoTools
{
    /// <summary>
    /// Registers all Go tools with the registry.
    /// </summary>
    public static void RegisterGoTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(CreateBuildTool(processRunner, logger));
        registry.RegisterTool(CreateTestTool(processRunner, logger));
        registry.RegisterTool(CreateVetTool(processRunner, logger));
        registry.RegisterTool(CreateFmtTool(processRunner, logger));
        registry.RegisterTool(CreateModTidyTool(processRunner, logger));

        logger.LogInformation("Registered 5 Go tools");
    }

    private static ToolDefinition CreateBuildTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "go.build",
        Name = "Build Go Code",
        Description = "Runs 'go build' to compile Go code. " +
                      "Returns any compilation errors.",
        Categories = ["go", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package path to build (default: ./...)" },
                "output": { "type": "string", "description": "Output file path (optional)" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter("package", "./...")!;
            var output = input.GetParameter<string?>("output", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "build" };
            if (!string.IsNullOrEmpty(output))
            {
                args.Add("-o");
                args.Add(output);
            }
            args.Add(package);

            var result = await runner.RunAsync("go", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("go build completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateTestTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "go.test",
        Name = "Run Go Tests",
        Description = "Runs 'go test' to execute Go tests. " +
                      "Optionally specify a package or use -v for verbose output.",
        Categories = ["go", "testing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package path to test (default: ./...)" },
                "verbose": { "type": "boolean", "description": "Enable verbose output", "default": true },
                "coverage": { "type": "boolean", "description": "Enable coverage", "default": false },
                "run": { "type": "string", "description": "Run only tests matching this regex" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter("package", "./...")!;
            var verbose = input.GetParameter("verbose", true);
            var coverage = input.GetParameter("coverage", false);
            var run = input.GetParameter<string?>("run", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "test" };
            if (verbose) args.Add("-v");
            if (coverage) args.Add("-cover");
            if (!string.IsNullOrEmpty(run))
            {
                args.Add("-run");
                args.Add(run);
            }
            args.Add(package);

            var result = await runner.RunAsync("go", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("go test completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateVetTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "go.vet",
        Name = "Vet Go Code",
        Description = "Runs 'go vet' to check for suspicious constructs. " +
                      "Catches common mistakes that the compiler doesn't.",
        Categories = ["go", "linting"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package path to vet (default: ./...)" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter("package", "./...")!;
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var result = await runner.RunAsync("go", ["vet", package], new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("go vet completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                issues = result.StandardOutput + result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateFmtTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "go.fmt",
        Name = "Format Go Code",
        Description = "Runs 'gofmt' or 'goimports' to format Go code. " +
                      "Go requires specific formatting - always run this after changes.",
        Categories = ["go", "formatting"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or directory to format" },
                "check": { "type": "boolean", "description": "Check only, don't modify (list files that would change)", "default": false },
                "imports": { "type": "boolean", "description": "Also organize imports (use goimports)", "default": true },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            },
            "required": ["path"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var check = input.GetParameter("check", false);
            var imports = input.GetParameter("imports", true);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            string command;
            string[] args;

            if (imports)
            {
                // goimports also formats
                command = "goimports";
                args = check ? ["-l", path] : ["-w", path];
            }
            else
            {
                command = "gofmt";
                args = check ? ["-l", path] : ["-w", path];
            }

            var result = await runner.RunAsync(command, args, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            // If goimports not found, fall back to gofmt
            if (result.ExitCode != 0 && imports && result.StandardError.Contains("not found"))
            {
                args = check ? ["-l", path] : ["-w", path];
                result = await runner.RunAsync("gofmt", args, new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            logger.LogDebug("Go format completed with exit code {ExitCode}", result.ExitCode);

            if (check)
            {
                var filesNeedFormatting = result.StandardOutput.Trim();
                return ToolResult.Ok(new
                {
                    success = string.IsNullOrEmpty(filesNeedFormatting),
                    filesNeedFormatting,
                });
            }

            return result.ExitCode == 0
                ? ToolResult.Ok(new { formatted = true })
                : ToolResult.Fail($"Format failed: {result.StandardError}");
        },
    };

    private static ToolDefinition CreateModTidyTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "go.mod_tidy",
        Name = "Tidy Go Modules",
        Description = "Runs 'go mod tidy' to add missing and remove unused dependencies.",
        Categories = ["go", "dependencies"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "workingDirectory": { "type": "string", "description": "Working directory (must contain go.mod)" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var result = await runner.RunAsync("go", ["mod", "tidy"], new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("go mod tidy completed with exit code {ExitCode}", result.ExitCode);

            return result.ExitCode == 0
                ? ToolResult.Ok(new { success = true, output = result.StandardOutput })
                : ToolResult.Fail($"go mod tidy failed: {result.StandardError}");
        },
    };
}
