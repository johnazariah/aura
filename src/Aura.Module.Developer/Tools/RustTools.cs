// <copyright file="RustTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rust-specific tools for the Developer module.
/// Uses cargo commands to interact with Rust tooling.
/// </summary>
public static class RustTools
{
    /// <summary>
    /// Registers all Rust tools with the registry.
    /// </summary>
    public static void RegisterRustTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(CreateBuildTool(processRunner, logger));
        registry.RegisterTool(CreateTestTool(processRunner, logger));
        registry.RegisterTool(CreateCheckTool(processRunner, logger));
        registry.RegisterTool(CreateClippyTool(processRunner, logger));
        registry.RegisterTool(CreateFmtTool(processRunner, logger));
        registry.RegisterTool(CreateRunTool(processRunner, logger));

        logger.LogInformation("Registered 6 Rust tools");
    }

    private static ToolDefinition CreateBuildTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "rust.build",
        Name = "Build Rust Code",
        Description = "Runs 'cargo build' to compile Rust code. " +
                      "Returns any compilation errors.",
        Categories = ["rust", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "release": { "type": "boolean", "description": "Build in release mode", "default": false },
                "package": { "type": "string", "description": "Package to build (for workspaces)" },
                "features": { "type": "string", "description": "Comma-separated features to enable" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var release = input.GetParameter("release", false);
            var package = input.GetParameter<string?>("package", null);
            var features = input.GetParameter<string?>("features", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "build" };
            if (release) args.Add("--release");
            if (!string.IsNullOrEmpty(package))
            {
                args.Add("-p");
                args.Add(package);
            }
            if (!string.IsNullOrEmpty(features))
            {
                args.Add("--features");
                args.Add(features);
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo build completed with exit code {ExitCode}", result.ExitCode);

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
        ToolId = "rust.test",
        Name = "Run Rust Tests",
        Description = "Runs 'cargo test' to execute Rust tests.",
        Categories = ["rust", "testing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package to test (for workspaces)" },
                "testName": { "type": "string", "description": "Run only tests containing this string" },
                "nocapture": { "type": "boolean", "description": "Show println! output", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter<string?>("package", null);
            var testName = input.GetParameter<string?>("testName", null);
            var nocapture = input.GetParameter("nocapture", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "test" };
            if (!string.IsNullOrEmpty(package))
            {
                args.Add("-p");
                args.Add(package);
            }
            if (!string.IsNullOrEmpty(testName))
            {
                args.Add(testName);
            }
            if (nocapture)
            {
                args.Add("--");
                args.Add("--nocapture");
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo test completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateCheckTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "rust.check",
        Name = "Check Rust Code",
        Description = "Runs 'cargo check' for fast syntax and type checking without full compilation.",
        Categories = ["rust", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package to check (for workspaces)" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter<string?>("package", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "check" };
            if (!string.IsNullOrEmpty(package))
            {
                args.Add("-p");
                args.Add(package);
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo check completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateClippyTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "rust.clippy",
        Name = "Run Clippy Linter",
        Description = "Runs 'cargo clippy' for comprehensive lint checks. " +
                      "Clippy catches common mistakes and suggests idiomatic Rust.",
        Categories = ["rust", "linting"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "package": { "type": "string", "description": "Package to lint (for workspaces)" },
                "warningsAsErrors": { "type": "boolean", "description": "Treat warnings as errors", "default": true },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var package = input.GetParameter<string?>("package", null);
            var warningsAsErrors = input.GetParameter("warningsAsErrors", true);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "clippy" };
            if (!string.IsNullOrEmpty(package))
            {
                args.Add("-p");
                args.Add(package);
            }
            if (warningsAsErrors)
            {
                args.Add("--");
                args.Add("-D");
                args.Add("warnings");
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo clippy completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                warnings = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateFmtTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "rust.fmt",
        Name = "Format Rust Code",
        Description = "Runs 'cargo fmt' to format Rust code using rustfmt.",
        Categories = ["rust", "formatting"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "check": { "type": "boolean", "description": "Check only, don't modify", "default": false },
                "package": { "type": "string", "description": "Package to format (for workspaces)" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var check = input.GetParameter("check", false);
            var package = input.GetParameter<string?>("package", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "fmt" };
            if (!string.IsNullOrEmpty(package))
            {
                args.Add("-p");
                args.Add(package);
            }
            if (check)
            {
                args.Add("--check");
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo fmt completed with exit code {ExitCode}", result.ExitCode);

            if (check)
            {
                return ToolResult.Ok(new
                {
                    success = result.ExitCode == 0,
                    needsFormatting = result.ExitCode != 0,
                    output = result.StandardOutput,
                });
            }

            return result.ExitCode == 0
                ? ToolResult.Ok(new { formatted = true })
                : ToolResult.Fail($"Format failed: {result.StandardError}");
        },
    };

    private static ToolDefinition CreateRunTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "rust.run",
        Name = "Run Rust Program",
        Description = "Runs 'cargo run' to compile and execute the Rust program.",
        Categories = ["rust", "execution"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "release": { "type": "boolean", "description": "Run in release mode", "default": false },
                "bin": { "type": "string", "description": "Binary to run (for multi-binary crates)" },
                "args": { "type": "array", "items": { "type": "string" }, "description": "Arguments to pass to the program" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var release = input.GetParameter("release", false);
            var bin = input.GetParameter<string?>("bin", null);
            var programArgs = input.GetParameter<string[]?>("args", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "run" };
            if (release) args.Add("--release");
            if (!string.IsNullOrEmpty(bin))
            {
                args.Add("--bin");
                args.Add(bin);
            }
            if (programArgs is { Length: > 0 })
            {
                args.Add("--");
                args.AddRange(programArgs);
            }

            var result = await runner.RunAsync("cargo", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("cargo run completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                exitCode = result.ExitCode,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };
}
