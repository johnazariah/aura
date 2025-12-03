// <copyright file="TypeScriptTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// TypeScript/JavaScript-specific tools for the Developer module.
/// Uses shell commands to interact with Node.js tooling.
/// </summary>
public static class TypeScriptTools
{
    /// <summary>
    /// Registers all TypeScript tools with the registry.
    /// </summary>
    public static void RegisterTypeScriptTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        ILogger logger)
    {
        registry.RegisterTool(CreateCompileTool(processRunner, logger));
        registry.RegisterTool(CreateTypeCheckTool(processRunner, logger));
        registry.RegisterTool(CreateRunTestsTool(processRunner, logger));
        registry.RegisterTool(CreateLintTool(processRunner, logger));
        registry.RegisterTool(CreateFormatTool(processRunner, logger));

        logger.LogInformation("Registered 5 TypeScript tools");
    }

    private static ToolDefinition CreateCompileTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "typescript.compile",
        Name = "Compile TypeScript",
        Description = "Runs the TypeScript compiler (tsc) to compile TypeScript to JavaScript. " +
                      "Returns any compilation errors.",
        Categories = ["typescript", "compilation"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "project": { "type": "string", "description": "Path to tsconfig.json (optional)" },
                "noEmit": { "type": "boolean", "description": "Don't emit output files", "default": true },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var project = input.GetParameter<string?>("project", null);
            var noEmit = input.GetParameter("noEmit", true);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string>();
            if (!string.IsNullOrEmpty(project))
            {
                args.Add("--project");
                args.Add(project);
            }
            if (noEmit) args.Add("--noEmit");

            // Try npx tsc first
            var result = await runner.RunAsync("npx", ["tsc", .. args], new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("tsc completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateTypeCheckTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "typescript.type_check",
        Name = "Type Check TypeScript",
        Description = "Runs TypeScript compiler in type-check only mode (--noEmit). " +
                      "Faster than full compilation.",
        Categories = ["typescript", "typing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "project": { "type": "string", "description": "Path to tsconfig.json (optional)" },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var project = input.GetParameter<string?>("project", null);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "tsc", "--noEmit" };
            if (!string.IsNullOrEmpty(project))
            {
                args.Add("--project");
                args.Add(project);
            }

            var result = await runner.RunAsync("npx", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("TypeScript type check completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                issues = result.StandardOutput + result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateRunTestsTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "typescript.run_tests",
        Name = "Run TypeScript/JavaScript Tests",
        Description = "Runs tests using the project's test runner (jest, vitest, or npm test). " +
                      "Automatically detects the test framework.",
        Categories = ["typescript", "testing"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to test file or pattern (optional)" },
                "watch": { "type": "boolean", "description": "Run in watch mode", "default": false },
                "coverage": { "type": "boolean", "description": "Collect coverage", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetParameter<string?>("path", null);
            var watch = input.GetParameter("watch", false);
            var coverage = input.GetParameter("coverage", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            // Try vitest first, then jest, then npm test
            var args = new List<string> { "vitest", "run" };
            if (!string.IsNullOrEmpty(path)) args.Add(path);
            if (coverage) args.Add("--coverage");

            var result = await runner.RunAsync("npx", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            // If vitest not found, try jest
            if (result.StandardError.Contains("vitest") && result.StandardError.Contains("not found"))
            {
                args = ["jest"];
                if (!string.IsNullOrEmpty(path)) args.Add(path);
                if (coverage) args.Add("--coverage");
                if (!watch) args.Add("--watchAll=false");

                result = await runner.RunAsync("npx", args.ToArray(), new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            // If jest not found, try npm test
            if (result.ExitCode != 0 && result.StandardError.Contains("not found"))
            {
                result = await runner.RunAsync("npm", ["test", "--", "--passWithNoTests"], new ProcessOptions
                {
                    WorkingDirectory = workingDir,
                }, ct);
            }

            logger.LogDebug("TypeScript tests completed with exit code {ExitCode}", result.ExitCode);

            return ToolResult.Ok(new
            {
                success = result.ExitCode == 0,
                output = result.StandardOutput,
                errors = result.StandardError,
            });
        },
    };

    private static ToolDefinition CreateLintTool(IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "typescript.lint",
        Name = "Lint TypeScript/JavaScript",
        Description = "Runs ESLint to check code for issues. " +
                      "Returns any linting errors or warnings.",
        Categories = ["typescript", "linting"],
        RequiresConfirmation = false,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or directory to lint", "default": "." },
                "fix": { "type": "boolean", "description": "Automatically fix problems", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetParameter("path", ".")!;
            var fix = input.GetParameter("fix", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "eslint", path };
            if (fix) args.Add("--fix");
            args.Add("--format");
            args.Add("stylish");

            var result = await runner.RunAsync("npx", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("ESLint completed with exit code {ExitCode}", result.ExitCode);

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
        ToolId = "typescript.format",
        Name = "Format TypeScript/JavaScript",
        Description = "Runs Prettier to format code. " +
                      "Modifies files in place unless check mode is enabled.",
        Categories = ["typescript", "formatting"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to file or glob pattern", "default": "." },
                "check": { "type": "boolean", "description": "Check only, don't modify", "default": false },
                "workingDirectory": { "type": "string", "description": "Working directory" }
            }
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetParameter("path", ".")!;
            var check = input.GetParameter("check", false);
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

            var args = new List<string> { "prettier" };
            if (check)
            {
                args.Add("--check");
            }
            else
            {
                args.Add("--write");
            }
            args.Add(path);

            var result = await runner.RunAsync("npx", args.ToArray(), new ProcessOptions
            {
                WorkingDirectory = workingDir,
            }, ct);

            logger.LogDebug("Prettier completed with exit code {ExitCode}", result.ExitCode);

            return result.ExitCode == 0
                ? ToolResult.Ok(new { formatted = !check || result.ExitCode == 0, output = result.StandardOutput })
                : ToolResult.Fail($"Format failed: {result.StandardError}");
        },
    };
}
