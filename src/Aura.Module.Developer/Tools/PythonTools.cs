// <copyright file="PythonTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Python-specific tools for the Developer module.
/// Uses shell commands to interact with Python tooling.
/// </summary>
public static class PythonTools
{
    /// <summary>
    /// Registers all Python tools with the registry.
    /// </summary>
    public static void RegisterPythonTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(CreateRunScriptTool(processRunner, logger));
        registry.RegisterTool(CreateRunTestsTool(processRunner, logger));
        registry.RegisterTool(CreateLintTool(processRunner, logger));
        registry.RegisterTool(CreateFormatTool(processRunner, logger));
        registry.RegisterTool(CreateTypeCheckTool(processRunner, logger));

        logger.LogInformation("Registered 5 Python tools");
    }

    private static ToolDefinition CreateRunScriptTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "python.run_script",
        Name = "Run Python Script",
        Description = "Executes a Python script and returns the output. " +
                      "Use this to test Python code or run utilities.",
        Categories = ["python", "execution"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "script": { "type": "string", "description": "Path to the Python script to run" },
                "args": { "type": "array", "items": { "type": "string" }, "description": "Arguments to pass to the script" },
                "workingDirectory": { "type": "string", "description": "Working directory for execution" }
            },
            "required": ["script"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var script = input.GetRequiredParameter<string>("script");
            var args = input.GetParameter<string[]>("args", []) ?? [];
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var allArgs = new[] { script }.Concat(args).ToArray();

            var result = await runner.RunAsync("python", allArgs, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("Python script {Script} completed with exit code {ExitCode}", script, result.ExitCode);

            return result.ExitCode == 0
                ? ToolResult.Ok(new { stdout = result.StandardOutput, stderr = result.StandardError })
                : ToolResult.Fail($"Script failed with exit code {result.ExitCode}: {result.StandardError}");
        },
    };

    private static ToolDefinition CreateRunTestsTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "python.run_tests",
        Name = "Run Python Tests",
        Description = "Runs pytest to execute Python tests. " +
                      "Optionally specify a path to run specific tests.",
        Categories = ["python", "testing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to test file or directory (optional)" },
                "verbose": { "type": "boolean", "description": "Enable verbose output", "default": true },
                "failFast": { "type": "boolean", "description": "Stop on first failure", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetParameter<string?>("path", null);
            var verbose = input.GetParameter("verbose", true);
            var failFast = input.GetParameter("failFast", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "-m", "pytest" };
            if (verbose) args.Add("-v");
            if (failFast) args.Add("-x");
            if (!string.IsNullOrEmpty(path)) args.Add(path);

            var result = await runner.RunAsync("python", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("pytest completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                exitCode = result.ExitCode,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateLintTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "python.lint",
        Name = "Lint Python Code",
        Description = "Runs ruff or flake8 to check Python code for issues. " +
                      "Returns any linting errors or warnings.",
        Categories = ["python", "linting"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or directory to lint" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            },
            "required": ["path"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            // Try ruff first (faster), fall back to flake8
            var result = await runner.RunAsync("ruff", ["check", path], new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            if (result.ExitCode == -1) // ruff not found
            {
                result = await runner.RunAsync("python", ["-m", "flake8", path], new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            logger.LogDebug("Python lint completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                issues = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateFormatTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "python.format",
        Name = "Format Python Code",
        Description = "Runs ruff format or black to format Python code. " +
                      "Modifies files in place.",
        Categories = ["python", "formatting"],
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

            var args = check ? new[] { "format", "--check", path } : new[] { "format", path };

            // Try ruff first, fall back to black
            var result = await runner.RunAsync("ruff", args, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            if (result.ExitCode == -1) // ruff not found
            {
                var blackArgs = check ? new[] { "-m", "black", "--check", path } : new[] { "-m", "black", path };
                result = await runner.RunAsync("python", blackArgs, new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            logger.LogDebug("Python format completed with exit code {ExitCode}", result.ExitCode);

            return result.ExitCode == 0
                ? ToolResult.Ok(new { formatted = true, output = result.StandardOutput })
                : ToolResult.Fail($"Format failed: {result.StandardError}");
        },
    };

    private static ToolDefinition CreateTypeCheckTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "python.type_check",
        Name = "Type Check Python Code",
        Description = "Runs mypy or pyright to check Python type annotations. " +
                      "Returns any type errors.",
        Categories = ["python", "typing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or directory to type check" },
                "strict": { "type": "boolean", "description": "Enable strict mode", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            },
            "required": ["path"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var strict = input.GetParameter("strict", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = strict ? new[] { "--strict", path } : new[] { path };

            var result = await runner.RunAsync("mypy", args, new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("mypy completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                issues = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };
}
