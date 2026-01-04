// <copyright file="LanguageToolFactory.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Creates tools from YAML language configuration.
/// </summary>
public static class LanguageToolFactory
{
    /// <summary>
    /// Registers tools from a language configuration with the tool registry.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="processRunner">Process runner for CLI commands.</param>
    /// <param name="config">The language configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>List of registered tool IDs.</returns>
    public static IReadOnlyList<string> RegisterToolsFromConfig(
        IToolRegistry registry,
        IProcessRunner processRunner,
        LanguageConfig config,
        ILogger logger)
    {
        var registeredTools = new List<string>();

        foreach (var (name, toolDef) in config.Tools)
        {
            // Skip if tool already exists (e.g., from hardcoded registration)
            if (registry.HasTool(toolDef.Id))
            {
                logger.LogDebug(
                    "Tool {ToolId} already registered, skipping YAML definition",
                    toolDef.Id);
                continue;
            }

            var tool = CreateToolFromConfig(toolDef, processRunner, logger);
            registry.RegisterTool(tool);
            registeredTools.Add(toolDef.Id);

            logger.LogDebug(
                "Registered tool {ToolId} from {Language} config",
                toolDef.Id,
                config.Language.Name);
        }

        if (registeredTools.Count > 0)
        {
            logger.LogInformation(
                "Registered {Count} {Language} tools from config: {Tools}",
                registeredTools.Count,
                config.Language.Name,
                string.Join(", ", registeredTools));
        }

        return registeredTools;
    }

    /// <summary>
    /// Creates a tool definition from YAML configuration.
    /// </summary>
    private static ToolDefinition CreateToolFromConfig(
        ToolConfig toolDef,
        IProcessRunner processRunner,
        ILogger logger)
    {
        return new ToolDefinition
        {
            ToolId = toolDef.Id,
            Name = toolDef.Name ?? toolDef.Id,
            Description = toolDef.Description ?? $"Execute {toolDef.Command}",
            Categories = toolDef.Categories.ToList(),
            RequiresConfirmation = toolDef.RequiresConfirmation,
            InputSchema = GenerateInputSchema(toolDef),
            Handler = async (input, ct) =>
            {
                var startTime = DateTime.UtcNow;

                try
                {
                    var result = await ExecuteToolAsync(
                        toolDef,
                        input,
                        processRunner,
                        logger,
                        ct).ConfigureAwait(false);

                    var duration = DateTime.UtcNow - startTime;

                    if (result.ExitCode == 0)
                    {
                        var output = ParseOutput(toolDef, result);
                        return ToolResult.Ok(output, duration);
                    }

                    // Try fallback if configured
                    if (toolDef.Fallback is not null)
                    {
                        logger.LogDebug(
                            "Tool {ToolId} failed with exit code {ExitCode}, trying fallback",
                            toolDef.Id,
                            result.ExitCode);

                        result = await ExecuteFallbackAsync(
                            toolDef.Fallback,
                            input,
                            processRunner,
                            logger,
                            ct).ConfigureAwait(false);

                        duration = DateTime.UtcNow - startTime;

                        if (result.ExitCode == 0)
                        {
                            var output = ParseOutput(toolDef, result);
                            return ToolResult.Ok(output, duration);
                        }
                    }

                    // Both primary and fallback failed
                    var error = !string.IsNullOrEmpty(result.StandardError)
                        ? result.StandardError
                        : result.StandardOutput;
                    return ToolResult.Fail(error, duration);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var duration = DateTime.UtcNow - startTime;
                    logger.LogError(ex, "Tool {ToolId} execution failed", toolDef.Id);
                    return ToolResult.Fail(ex.Message, duration);
                }
            },
        };
    }

    /// <summary>
    /// Executes the primary command for a tool.
    /// </summary>
    private static async Task<ProcessResult> ExecuteToolAsync(
        ToolConfig toolDef,
        ToolInput input,
        IProcessRunner processRunner,
        ILogger logger,
        CancellationToken ct)
    {
        var args = BuildArguments(toolDef, input);
        var argsString = string.Join(" ", args);

        logger.LogDebug(
            "Executing {Command} {Args} in {WorkingDirectory}",
            toolDef.Command,
            argsString,
            input.WorkingDirectory ?? "(current)");

        return await processRunner.RunAsync(
            toolDef.Command,
            args.ToArray(),
            new ProcessOptions { WorkingDirectory = input.WorkingDirectory },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the fallback command.
    /// </summary>
    private static async Task<ProcessResult> ExecuteFallbackAsync(
        FallbackConfig fallback,
        ToolInput input,
        IProcessRunner processRunner,
        ILogger logger,
        CancellationToken ct)
    {
        var args = fallback.Args.ToList();

        // Add path if provided
        var path = input.GetParameter<string>("path");
        if (!string.IsNullOrEmpty(path))
        {
            args.Add(path);
        }

        var argsString = string.Join(" ", args);

        logger.LogDebug(
            "Executing fallback {Command} {Args}",
            fallback.Command,
            argsString);

        return await processRunner.RunAsync(
            fallback.Command,
            args.ToArray(),
            new ProcessOptions { WorkingDirectory = input.WorkingDirectory },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds command arguments from tool config and input.
    /// </summary>
    private static List<string> BuildArguments(ToolConfig toolDef, ToolInput input)
    {
        var args = toolDef.Args.ToList();

        // Get optional parameters from input
        var path = input.GetParameter<string>("path");
        var project = input.GetParameter<string>("project");
        var script = input.GetParameter<string>("script");
        var configuration = input.GetParameter<string>("configuration");

        // Insert path at specified position
        if (!string.IsNullOrEmpty(path) && toolDef.PathArg.HasValue)
        {
            var pos = toolDef.PathArg.Value;
            if (pos == -1)
            {
                args.Add(path);
            }
            else if (pos >= 0 && pos <= args.Count)
            {
                args.Insert(pos, path);
            }
        }

        // Insert project at specified position
        if (!string.IsNullOrEmpty(project) && toolDef.ProjectArg.HasValue)
        {
            var pos = toolDef.ProjectArg.Value;
            if (pos >= 0 && pos <= args.Count)
            {
                args.Insert(pos, project);
            }
        }

        // Insert script at specified position
        if (!string.IsNullOrEmpty(script) && toolDef.ScriptArg.HasValue)
        {
            var pos = toolDef.ScriptArg.Value;
            if (pos >= 0 && pos <= args.Count)
            {
                args.Insert(pos, script);
            }
        }

        // Add configuration argument if specified
        if (!string.IsNullOrEmpty(configuration) && toolDef.ConfigArg is not null)
        {
            args.AddRange(toolDef.ConfigArg);
            args.Add(configuration);
        }

        return args;
    }

    /// <summary>
    /// Parses tool output according to configured parsers.
    /// </summary>
    private static object ParseOutput(ToolConfig toolDef, ProcessResult result)
    {
        var output = new Dictionary<string, object?>
        {
            ["stdout"] = result.StandardOutput,
            ["stderr"] = result.StandardError,
            ["exitCode"] = result.ExitCode,
        };

        if (toolDef.OutputParsers is null)
        {
            return output;
        }

        foreach (var (name, parser) in toolDef.OutputParsers)
        {
            var parsed = ParseWithParser(parser, result.StandardOutput + result.StandardError);
            if (parsed is not null)
            {
                output[name] = parsed;
            }
        }

        return output;
    }

    /// <summary>
    /// Parses output using a specific parser configuration.
    /// </summary>
    private static object? ParseWithParser(OutputParserConfig parser, string output)
    {
        return parser.Type.ToLowerInvariant() switch
        {
            "linematch" => ParseLineMatch(parser, output),
            "regex" => ParseRegex(parser, output),
            "json" => ParseJson(parser, output),
            "exitcode" => null, // Exit code is already in output
            _ => null,
        };
    }

    /// <summary>
    /// Extracts lines matching a pattern.
    /// </summary>
    private static object? ParseLineMatch(OutputParserConfig parser, string output)
    {
        if (string.IsNullOrEmpty(parser.Pattern))
        {
            return null;
        }

        var comparison = parser.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var matchingLines = output
            .Split('\n')
            .Where(line => line.Contains(parser.Pattern, comparison))
            .Select(line => line.Trim())
            .ToList();

        return matchingLines;
    }

    /// <summary>
    /// Extracts named groups using regex.
    /// </summary>
    private static object? ParseRegex(OutputParserConfig parser, string output)
    {
        if (string.IsNullOrEmpty(parser.Pattern))
        {
            return null;
        }

        var options = parser.IgnoreCase
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        var match = Regex.Match(output, parser.Pattern, options);
        if (!match.Success)
        {
            return null;
        }

        if (parser.Groups is null || parser.Groups.Count == 0)
        {
            return match.Value;
        }

        var result = new Dictionary<string, string?>();
        for (int i = 0; i < parser.Groups.Count && i < match.Groups.Count - 1; i++)
        {
            result[parser.Groups[i]] = match.Groups[i + 1].Success
                ? match.Groups[i + 1].Value
                : null;
        }

        return result;
    }

    /// <summary>
    /// Parses JSON output.
    /// </summary>
    private static object? ParseJson(OutputParserConfig parser, string output)
    {
        try
        {
            // Find JSON content (may be embedded in other output)
            var jsonStart = output.IndexOf('{');
            var jsonEnd = output.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                return null;
            }

            var jsonContent = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
            return JsonSerializer.Deserialize<object>(jsonContent);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a JSON schema for tool input.
    /// </summary>
    private static string GenerateInputSchema(ToolConfig toolDef)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        // Add common optional parameters based on config
        if (toolDef.PathArg.HasValue)
        {
            properties["path"] = new { type = "string", description = "File or directory path" };
        }

        if (toolDef.ProjectArg.HasValue)
        {
            properties["project"] = new { type = "string", description = "Project file path" };
        }

        if (toolDef.ScriptArg.HasValue)
        {
            properties["script"] = new { type = "string", description = "Script file path" };
        }

        if (toolDef.ConfigArg is not null)
        {
            properties["configuration"] = new { type = "string", description = "Build configuration (e.g., Debug, Release)" };
        }

        var schema = new
        {
            type = "object",
            properties,
            required,
        };

        return JsonSerializer.Serialize(schema);
    }
}
