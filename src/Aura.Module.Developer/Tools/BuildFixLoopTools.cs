// <copyright file="BuildFixLoopTools.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using System.Text;
using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Build-fix loop tools that automate the build → analyze → fix → rebuild cycle.
/// Inspired by Copilot CLI's autonomous build fixing capability.
/// </summary>
public static class BuildFixLoopTools
{
    /// <summary>
    /// Registers all build-fix loop tools with the registry.
    /// </summary>
    public static void RegisterBuildFixLoopTools(
        IToolRegistry registry,
        IProcessRunner processRunner,
        IAgentRegistry agentRegistry,
        ILogger logger)
    {
        registry.RegisterTool(CreateDotnetBuildUntilSuccessTool(processRunner, agentRegistry, logger));
        registry.RegisterTool(CreateCargoBuildUntilSuccessTool(processRunner, agentRegistry, logger));
        registry.RegisterTool(CreateNpmBuildUntilSuccessTool(processRunner, agentRegistry, logger));

        logger.LogInformation("Registered 3 build-fix loop tools");
    }

    private static ToolDefinition CreateDotnetBuildUntilSuccessTool(
        IProcessRunner runner,
        IAgentRegistry agentRegistry,
        ILogger logger) => new()
        {
            ToolId = "dotnet.build_until_success",
            Name = "Build Until Success (.NET)",
            Description = """
            Iteratively builds a .NET project, analyzes errors, applies fixes, and rebuilds
            until success or max iterations reached. Uses the build-fixer-agent to suggest fixes.
            
            This is a "build-fix loop" that automates the tedious cycle of:
            1. Build → see errors
            2. Analyze errors → understand cause
            3. Fix code → apply changes
            4. Rebuild → check if fixed
            5. Repeat until success
            """,
            Categories = ["dotnet", "build", "automation"],
            RequiresConfirmation = true, // Modifies files automatically
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "projectPath": { 
                    "type": "string", 
                    "description": "Path to .csproj, .fsproj, or .sln file"
                },
                "maxIterations": { 
                    "type": "integer", 
                    "description": "Maximum fix attempts before giving up",
                    "default": 5
                },
                "workingDirectory": { 
                    "type": "string", 
                    "description": "Working directory for build commands"
                }
            }
        }
        """,
            Handler = async (input, ct) =>
            {
                var projectPath = input.GetParameter<string?>("projectPath", null);
                var maxIterations = input.GetParameter("maxIterations", 5);
                var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

                return await RunBuildFixLoop(
                    runner,
                    agentRegistry,
                    logger,
                    buildCommand: "dotnet",
                    buildArgs: BuildDotnetArgs(projectPath),
                    workingDir: workingDir,
                    maxIterations: maxIterations,
                    language: "csharp",
                    ct);
            },
        };

    private static ToolDefinition CreateCargoBuildUntilSuccessTool(
        IProcessRunner runner,
        IAgentRegistry agentRegistry,
        ILogger logger) => new()
        {
            ToolId = "cargo.build_until_success",
            Name = "Build Until Success (Rust)",
            Description = """
            Iteratively builds a Rust project using cargo, analyzes errors, applies fixes, and rebuilds
            until success or max iterations reached.
            """,
            Categories = ["rust", "build", "automation"],
            RequiresConfirmation = true,
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "maxIterations": { 
                    "type": "integer", 
                    "description": "Maximum fix attempts before giving up",
                    "default": 5
                },
                "workingDirectory": { 
                    "type": "string", 
                    "description": "Working directory (must contain Cargo.toml)"
                }
            }
        }
        """,
            Handler = async (input, ct) =>
            {
                var maxIterations = input.GetParameter("maxIterations", 5);
                var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

                return await RunBuildFixLoop(
                    runner,
                    agentRegistry,
                    logger,
                    buildCommand: "cargo",
                    buildArgs: ["build", "--message-format=short"],
                    workingDir: workingDir,
                    maxIterations: maxIterations,
                    language: "rust",
                    ct);
            },
        };

    private static ToolDefinition CreateNpmBuildUntilSuccessTool(
        IProcessRunner runner,
        IAgentRegistry agentRegistry,
        ILogger logger) => new()
        {
            ToolId = "npm.build_until_success",
            Name = "Build Until Success (TypeScript/Node)",
            Description = """
            Iteratively builds a TypeScript/Node project using npm run build, analyzes errors, 
            applies fixes, and rebuilds until success or max iterations reached.
            """,
            Categories = ["typescript", "nodejs", "build", "automation"],
            RequiresConfirmation = true,
            InputSchema = """
        {
            "type": "object",
            "properties": {
                "buildScript": { 
                    "type": "string", 
                    "description": "npm script to run",
                    "default": "build"
                },
                "maxIterations": { 
                    "type": "integer", 
                    "description": "Maximum fix attempts before giving up",
                    "default": 5
                },
                "workingDirectory": { 
                    "type": "string", 
                    "description": "Working directory (must contain package.json)"
                }
            }
        }
        """,
            Handler = async (input, ct) =>
            {
                var buildScript = input.GetParameter("buildScript", "build")!;
                var maxIterations = input.GetParameter("maxIterations", 5);
                var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);

                return await RunBuildFixLoop(
                    runner,
                    agentRegistry,
                    logger,
                    buildCommand: "npm",
                    buildArgs: ["run", buildScript],
                    workingDir: workingDir,
                    maxIterations: maxIterations,
                    language: "typescript",
                    ct);
            },
        };

    private static string[] BuildDotnetArgs(string? projectPath)
    {
        var args = new List<string> { "build", "--no-restore" };
        if (!string.IsNullOrEmpty(projectPath))
        {
            args.Insert(1, projectPath);
        }
        return [.. args];
    }

    private static async Task<ToolResult> RunBuildFixLoop(
        IProcessRunner runner,
        IAgentRegistry agentRegistry,
        ILogger logger,
        string buildCommand,
        string[] buildArgs,
        string? workingDir,
        int maxIterations,
        string language,
        CancellationToken ct)
    {
        var iterations = new List<object>();
        var fixedFiles = new List<string>();
        var startTime = DateTime.UtcNow;

        for (var i = 1; i <= maxIterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            logger.LogInformation("Build-fix loop iteration {Iteration}/{Max}", i, maxIterations);

            // Step 1: Run build
            var buildResult = await runner.RunAsync(
                buildCommand,
                buildArgs,
                new ProcessOptions { WorkingDirectory = workingDir },
                ct);

            var buildOutput = buildResult.StandardOutput + "\n" + buildResult.StandardError;

            // Step 2: Check if build succeeded
            if (buildResult.ExitCode == 0)
            {
                logger.LogInformation("Build succeeded on iteration {Iteration}", i);

                return ToolResult.Ok(new
                {
                    success = true,
                    message = $"Build succeeded after {i} iteration(s)",
                    iterations = i,
                    fixedFiles,
                    totalDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    history = iterations,
                });
            }

            // Step 3: Parse errors
            var errors = ParseBuildErrors(buildOutput, language);
            if (errors.Count == 0)
            {
                logger.LogWarning("Build failed but no parseable errors found");
                iterations.Add(new
                {
                    iteration = i,
                    status = "failed",
                    reason = "Build failed but could not parse errors",
                    rawOutput = buildOutput.Length > 2000 ? buildOutput[..2000] + "..." : buildOutput,
                });
                continue;
            }

            logger.LogInformation("Found {ErrorCount} errors, attempting fix", errors.Count);

            // Step 4: Get fixer agent
            var fixerAgent = agentRegistry.GetBestForCapability("fixing");
            if (fixerAgent is null)
            {
                return ToolResult.Fail("No 'fixing' capability agent found (build-fixer-agent)");
            }

            // Step 5: Read the files that need fixing
            var filesToFix = errors
                .Select(e => e.FilePath)
                .Where(f => !string.IsNullOrEmpty(f) && File.Exists(f))
                .Distinct()
                .Take(3) // Limit to 3 files per iteration to avoid overwhelming the agent
                .ToList();

            if (filesToFix.Count == 0)
            {
                iterations.Add(new
                {
                    iteration = i,
                    status = "failed",
                    reason = "Could not locate files mentioned in errors",
                    errors,
                });
                continue;
            }

            var codeContext = new StringBuilder();
            foreach (var file in filesToFix)
            {
                var content = await File.ReadAllTextAsync(file, ct);
                codeContext.AppendLine($"=== {Path.GetFileName(file)} ===");
                codeContext.AppendLine(content);
                codeContext.AppendLine();
            }

            // Step 6: Call the fixer agent
            // Build a prompt with the build output and code context
            var fixerPrompt = $"""
                Fix the following build errors.

                ## Build Output
                {buildOutput}

                ## Current Code
                {codeContext}

                ## Instructions
                1. Analyze the error messages carefully
                2. Identify the root cause
                3. Provide the MINIMAL fix - don't refactor unrelated code
                4. Return the fixed code in a code block
                """;

            var context = new AgentContext(
                Prompt: fixerPrompt,
                WorkspacePath: workingDir);

            try
            {
                var fixerOutput = await fixerAgent.ExecuteAsync(context, ct);

                // Step 7: Parse and apply fixes from agent response
                var appliedFixes = await ApplyFixesFromAgentResponse(
                    fixerOutput.Content,
                    filesToFix,
                    workingDir,
                    logger,
                    ct);

                fixedFiles.AddRange(appliedFixes);

                iterations.Add(new
                {
                    iteration = i,
                    status = "fixed",
                    errorCount = errors.Count,
                    filesFixed = appliedFixes,
                    agentResponse = fixerOutput.Content.Length > 1000
                        ? fixerOutput.Content[..1000] + "..."
                        : fixerOutput.Content,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fixer agent failed on iteration {Iteration}", i);
                iterations.Add(new
                {
                    iteration = i,
                    status = "agent_error",
                    error = ex.Message,
                });
            }
        }

        // Max iterations reached
        return ToolResult.Ok(new
        {
            success = false,
            message = $"Build still failing after {maxIterations} iterations",
            iterations = maxIterations,
            fixedFiles,
            totalDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
            history = iterations,
        });
    }

    internal static List<BuildError> ParseBuildErrors(string output, string language)
    {
        var errors = new List<BuildError>();

        // MSBuild/dotnet error format: File(line,col): error CODE: message
        var msbuildPattern = new Regex(
            @"^(.+?)\((\d+),(\d+)\):\s*(error|warning)\s+(\w+):\s*(.+)$",
            RegexOptions.Multiline);

        // Rust/Cargo error format: error[E0XXX]: message --> file:line:col
        var rustPattern = new Regex(
            @"error\[(\w+)\]:\s*(.+?)\s+-->\s+(.+?):(\d+):(\d+)",
            RegexOptions.Multiline);

        // TypeScript error format: file(line,col): error TSXXXX: message
        var tsPattern = new Regex(
            @"^(.+?)\((\d+),(\d+)\):\s*error\s+(\w+):\s*(.+)$",
            RegexOptions.Multiline);

        foreach (Match match in msbuildPattern.Matches(output))
        {
            errors.Add(new BuildError
            {
                FilePath = match.Groups[1].Value.Trim(),
                Line = int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                Column = int.TryParse(match.Groups[3].Value, out var c) ? c : 0,
                Severity = match.Groups[4].Value,
                Code = match.Groups[5].Value,
                Message = match.Groups[6].Value.Trim(),
            });
        }

        foreach (Match match in rustPattern.Matches(output))
        {
            errors.Add(new BuildError
            {
                Code = match.Groups[1].Value,
                Message = match.Groups[2].Value.Trim(),
                FilePath = match.Groups[3].Value.Trim(),
                Line = int.TryParse(match.Groups[4].Value, out var l) ? l : 0,
                Column = int.TryParse(match.Groups[5].Value, out var c) ? c : 0,
                Severity = "error",
            });
        }

        foreach (Match match in tsPattern.Matches(output))
        {
            errors.Add(new BuildError
            {
                FilePath = match.Groups[1].Value.Trim(),
                Line = int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                Column = int.TryParse(match.Groups[3].Value, out var c) ? c : 0,
                Code = match.Groups[4].Value,
                Message = match.Groups[5].Value.Trim(),
                Severity = "error",
            });
        }

        return errors;
    }

    private static async Task<List<string>> ApplyFixesFromAgentResponse(
        string agentResponse,
        List<string> candidateFiles,
        string? workingDir,
        ILogger logger,
        CancellationToken ct)
    {
        var appliedFixes = new List<string>();

        // Try to extract code blocks from the agent response
        // Pattern: ```language\ncode\n``` or ## Fix\n```language\ncode\n```
        var codeBlockPattern = new Regex(
            @"```(?:csharp|fsharp|rust|typescript|javascript|cs|fs|rs|ts|js)?\s*\n([\s\S]*?)```",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var matches = codeBlockPattern.Matches(agentResponse);
        if (matches.Count == 0)
        {
            logger.LogWarning("No code blocks found in agent response");
            return appliedFixes;
        }

        // Simple heuristic: if there's exactly one file and one code block, apply it
        // This is conservative - a more sophisticated approach would parse file indicators
        if (candidateFiles.Count == 1 && matches.Count == 1)
        {
            var file = candidateFiles[0];
            var fixedCode = matches[0].Groups[1].Value.Trim();

            if (!string.IsNullOrEmpty(fixedCode))
            {
                try
                {
                    await File.WriteAllTextAsync(file, fixedCode, ct);
                    appliedFixes.Add(file);
                    logger.LogInformation("Applied fix to {File}", file);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to write fix to {File}", file);
                }
            }
        }
        else
        {
            // Multiple files or blocks - log but don't auto-apply (too risky)
            logger.LogInformation(
                "Found {BlockCount} code blocks for {FileCount} files - skipping auto-apply (requires review)",
                matches.Count, candidateFiles.Count);
        }

        return appliedFixes;
    }

    internal record BuildError
    {
        public string FilePath { get; init; } = "";
        public int Line { get; init; }
        public int Column { get; init; }
        public string Severity { get; init; } = "error";
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
    }
}
