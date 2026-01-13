using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Registers built-in tools for file operations and shell commands.
/// These are the canonical implementations used across all modules.
/// </summary>
public static class BuiltInTools
{
    /// <summary>
    /// Registers all built-in tools with the tool registry.
    /// </summary>
    public static void RegisterBuiltInTools(IToolRegistry registry, IFileSystem fileSystem, IProcessRunner processRunner, ILogger logger)
    {
        RegisterFileTools(registry, fileSystem, logger);
        RegisterShellTools(registry, processRunner, logger);
    }

    private static void RegisterFileTools(IToolRegistry registry, IFileSystem fileSystem, ILogger logger)
    {
        // file.read - Read file contents with optional line range
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.read",
            Name = "Read File",
            Description = "Read the contents of a file. Supports optional line range selection.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the file (relative or absolute)" },
                        "startLine": { "type": "integer", "description": "Optional starting line number (1-based, inclusive)" },
                        "endLine": { "type": "integer", "description": "Optional ending line number (1-based, inclusive)" },
                        "includeLineNumbers": { "type": "boolean", "description": "If true, prefix each line with its line number" }
                    },
                    "required": ["path"]
                }
                """,
            Categories = ["file"],
            Handler = async (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");
                var startLine = input.GetParameter<int?>("startLine");
                var endLine = input.GetParameter<int?>("endLine");
                var includeLineNumbers = input.GetParameter<bool>("includeLineNumbers");

                logger.LogWarning(
                    "[FILE.READ-DEBUG] path={Path}, startLine={StartLine}, endLine={EndLine}, includeLineNumbers={IncludeLineNumbers}",
                    path, startLine, endLine, includeLineNumbers);

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                if (!fileSystem.File.Exists(resolvedPath))
                {
                    return ToolResult.Fail($"File not found: {path}");
                }

                var content = await fileSystem.File.ReadAllTextAsync(resolvedPath, ct);

                // Apply line range if specified
                if (startLine.HasValue || endLine.HasValue)
                {
                    var lines = content.Split('\n');
                    var start = Math.Max(1, startLine ?? 1) - 1; // Convert to 0-based
                    var end = Math.Min(lines.Length, endLine ?? lines.Length);

                    logger.LogWarning(
                        "[FILE.READ-DEBUG] applying range: start={Start} (0-based), end={End}, totalLines={TotalLines}",
                        start, end, lines.Length);

                    if (start >= lines.Length)
                    {
                        return ToolResult.Fail($"Start line {startLine} is beyond file length ({lines.Length} lines)");
                    }

                    var selectedLines = lines.Skip(start).Take(end - start).ToArray();

                    logger.LogWarning("[FILE.READ-DEBUG] selected {SelectedCount} lines, first line: {FirstLine}",
                        selectedLines.Length, selectedLines.FirstOrDefault()?.Substring(0, Math.Min(50, selectedLines.FirstOrDefault()?.Length ?? 0)));

                    if (includeLineNumbers)
                    {
                        var sb = new StringBuilder();
                        for (int i = 0; i < selectedLines.Length; i++)
                        {
                            sb.AppendLine($"{start + i + 1}: {selectedLines[i].TrimEnd('\r')}");
                        }
                        content = sb.ToString();
                    }
                    else
                    {
                        content = string.Join("\n", selectedLines);
                    }
                }
                else if (includeLineNumbers)
                {
                    var lines = content.Split('\n');
                    var sb = new StringBuilder();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        sb.AppendLine($"{i + 1}: {lines[i].TrimEnd('\r')}");
                    }
                    content = sb.ToString();
                }

                return ToolResult.Ok(content);
            }
        });

        // file.write - Write content to a file with overwrite protection
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.write",
            Name = "Write File",
            Description = "Write content to a file. Creates parent directories if needed.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the file (relative or absolute)" },
                        "content": { "type": "string", "description": "Content to write to the file" },
                        "overwrite": { "type": "boolean", "description": "If false, fails when file exists. Default is true." },
                        "createDirectories": { "type": "boolean", "description": "If true, create parent directories. Default is true." }
                    },
                    "required": ["path", "content"]
                }
                """,
            Categories = ["file"],
            Handler = async (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");
                var content = input.GetRequiredParameter<string>("content");
                var overwrite = input.GetParameter("overwrite", true);
                var createDirectories = input.GetParameter("createDirectories", true);

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                // Check overwrite protection
                if (!overwrite && fileSystem.File.Exists(resolvedPath))
                {
                    return ToolResult.Fail($"File already exists and overwrite is false: {path}");
                }

                // Create parent directories if needed
                if (createDirectories)
                {
                    var directory = fileSystem.Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
                    {
                        fileSystem.Directory.CreateDirectory(directory);
                    }
                }

                await fileSystem.File.WriteAllTextAsync(resolvedPath, content ?? string.Empty, ct);
                return ToolResult.Ok($"File written: {path}");
            }
        });

        // file.modify - Find and replace text in a file
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.modify",
            Name = "Modify File",
            Description = "Modify a file by replacing text. Use for targeted edits to existing files. CRITICAL: The newText MUST have the EXACT same indentation as the oldText - count the leading spaces and replicate them on every line of your replacement.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the file (relative or absolute)" },
                        "oldText": { "type": "string", "description": "The EXACT text to find, including all whitespace and leading indentation" },
                        "newText": { "type": "string", "description": "The replacement text. CRITICAL: Every line must have the SAME leading spaces as oldText. If oldText starts with 12 spaces, newText lines must also start with 12 spaces." },
                        "replaceAll": { "type": "boolean", "description": "If true, replace all occurrences. Default is false (first only)." }
                    },
                    "required": ["path", "oldText", "newText"]
                }
                """,
            Categories = ["file"],
            Handler = async (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");
                var oldText = input.GetRequiredParameter<string>("oldText");
                var newText = input.GetRequiredParameter<string>("newText");
                var replaceAll = input.GetParameter("replaceAll", false);

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                if (!fileSystem.File.Exists(resolvedPath))
                {
                    return ToolResult.Fail($"File not found: {path}");
                }

                if (string.IsNullOrEmpty(oldText))
                {
                    return ToolResult.Fail("oldText cannot be empty");
                }

                var content = await fileSystem.File.ReadAllTextAsync(resolvedPath, ct);

                if (!content.Contains(oldText))
                {
                    var preview = oldText.Length > 50 ? oldText.Substring(0, 50) + "..." : oldText;
                    return ToolResult.Fail($"Text not found in file: {preview}");
                }

                // Perform replacement
                string modifiedContent;
                int replacementCount;

                if (replaceAll)
                {
                    replacementCount = CountOccurrences(content, oldText);
                    modifiedContent = content.Replace(oldText, newText);
                }
                else
                {
                    replacementCount = 1;
                    var index = content.IndexOf(oldText, StringComparison.Ordinal);
                    modifiedContent = content.Substring(0, index) + newText + content.Substring(index + oldText.Length);
                }

                await fileSystem.File.WriteAllTextAsync(resolvedPath, modifiedContent, ct);
                return ToolResult.Ok($"Modified {path}: replaced {replacementCount} occurrence(s)");
            }
        });

        // file.list - List directory contents
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.list",
            Name = "List Files",
            Description = "List files and directories in a path. Returns names with / suffix for directories.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the directory (relative or absolute)" },
                        "recursive": { "type": "boolean", "description": "If true, list recursively. Default is false." },
                        "pattern": { "type": "string", "description": "Optional glob pattern to filter results (e.g., '*.cs')" }
                    },
                    "required": ["path"]
                }
                """,
            Categories = ["file"],
            Handler = (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");
                var recursive = input.GetParameter("recursive", false);
                var pattern = input.GetParameter("pattern", "*") ?? "*";

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);
                var workingDir = input.WorkingDirectory ?? Environment.CurrentDirectory;

                if (!fileSystem.Directory.Exists(resolvedPath))
                {
                    return Task.FromResult(ToolResult.Fail($"Directory not found: {path}"));
                }

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var entries = new List<string>();

                // Return paths relative to WorkingDirectory so they can be used directly with file.read
                // Return paths relative to WorkingDirectory so they can be used directly with file.read
                // Only list directories when pattern is "*" (no specific file filter)
                if (pattern == "*")
                {
                    foreach (var dir in fileSystem.Directory.GetDirectories(resolvedPath, "*", searchOption))
                    {
                        var relativePath = fileSystem.Path.GetRelativePath(workingDir, dir);
                        // Skip hidden/ignored directories
                        if (relativePath.StartsWith('.') || relativePath.Contains("/.") ||
                            relativePath.StartsWith("node_modules") || relativePath.Contains("/node_modules"))
                        {
                            continue;
                        }
                        entries.Add(relativePath.Replace('\\', '/') + "/");
                    }
                }

                // Get files matching pattern
                foreach (var file in fileSystem.Directory.GetFiles(resolvedPath, pattern, searchOption))
                {
                    var relativePath = fileSystem.Path.GetRelativePath(workingDir, file);
                    // Skip files in hidden/ignored directories
                    if (relativePath.StartsWith('.') || relativePath.Contains("/.") ||
                        relativePath.StartsWith("node_modules") || relativePath.Contains("/node_modules"))
                    {
                        continue;
                    }
                    entries.Add(relativePath.Replace('\\', '/'));
                }

                entries.Sort();

                // Limit output to prevent token explosion
                const int maxEntries = 200;
                if (entries.Count > maxEntries)
                {
                    var truncated = entries.Take(maxEntries).ToList();
                    truncated.Add($"... ({entries.Count - maxEntries} more entries, use a more specific pattern)");
                    return Task.FromResult(ToolResult.Ok(string.Join("\n", truncated)));
                }

                return Task.FromResult(ToolResult.Ok(string.Join("\n", entries)));
            }
        });

        // file.exists - Check if a file or directory exists
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.exists",
            Name = "Check File Exists",
            Description = "Check if a file or directory exists at the given path.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to check (relative or absolute)" }
                    },
                    "required": ["path"]
                }
                """,
            Categories = ["file"],
            Handler = (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                var fileExists = fileSystem.File.Exists(resolvedPath);
                var dirExists = fileSystem.Directory.Exists(resolvedPath);

                if (fileExists)
                {
                    return Task.FromResult(ToolResult.Ok($"File exists: {path}"));
                }
                else if (dirExists)
                {
                    return Task.FromResult(ToolResult.Ok($"Directory exists: {path}"));
                }
                else
                {
                    return Task.FromResult(ToolResult.Ok($"Does not exist: {path}"));
                }
            }
        });

        // search.grep - Search for text in files
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "search.grep",
            Name = "Search Files",
            Description = "Search for a pattern in files. Returns matching lines with file paths and line numbers.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "pattern": { "type": "string", "description": "Text or regex pattern to search for" },
                        "path": { "type": "string", "description": "Directory or file to search in (defaults to working directory)" },
                        "filePattern": { "type": "string", "description": "File pattern to match (e.g., '*.cs', '*.ts'). Default is all files." },
                        "recursive": { "type": "boolean", "description": "Search recursively in subdirectories. Default is true." },
                        "caseSensitive": { "type": "boolean", "description": "Case sensitive search. Default is false." },
                        "maxResults": { "type": "integer", "description": "Maximum number of results to return. Default is 50." }
                    },
                    "required": ["pattern"]
                }
                """,
            Categories = ["search", "file"],
            Handler = async (input, ct) =>
            {
                var pattern = input.GetRequiredParameter<string>("pattern");
                var path = input.GetParameter<string>("path") ?? ".";
                var filePattern = input.GetParameter<string>("filePattern") ?? "*";
                var recursive = input.GetParameter("recursive", true);
                var caseSensitive = input.GetParameter("caseSensitive", false);
                var maxResults = input.GetParameter("maxResults", 50);

                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                if (!fileSystem.Directory.Exists(resolvedPath) && !fileSystem.File.Exists(resolvedPath))
                {
                    return ToolResult.Fail($"Path not found: {path}");
                }

                var results = new List<string>();
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                IEnumerable<string> filesToSearch;
                if (fileSystem.File.Exists(resolvedPath))
                {
                    filesToSearch = [resolvedPath];
                }
                else
                {
                    filesToSearch = fileSystem.Directory.GetFiles(resolvedPath, filePattern, searchOption)
                        .Where(f => !f.Contains("/.") && !f.Contains("\\.") &&
                                   !f.Contains("/node_modules/") && !f.Contains("\\node_modules\\") &&
                                   !f.Contains("/bin/") && !f.Contains("\\bin\\") &&
                                   !f.Contains("/obj/") && !f.Contains("\\obj\\"));
                }

                foreach (var file in filesToSearch)
                {
                    if (ct.IsCancellationRequested) break;
                    if (results.Count >= maxResults) break;

                    try
                    {
                        var lines = await fileSystem.File.ReadAllLinesAsync(file, ct);
                        for (var lineNum = 0; lineNum < lines.Length && results.Count < maxResults; lineNum++)
                        {
                            if (lines[lineNum].Contains(pattern, comparison))
                            {
                                var relativePath = fileSystem.Path.GetRelativePath(resolvedPath, file).Replace('\\', '/');
                                var linePreview = lines[lineNum].Trim();
                                if (linePreview.Length > 120)
                                {
                                    linePreview = linePreview.Substring(0, 120) + "...";
                                }
                                results.Add($"{relativePath}:{lineNum + 1}: {linePreview}");
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read (binary, locked, etc.)
                    }
                }

                if (results.Count == 0)
                {
                    return ToolResult.Ok($"No matches found for '{pattern}'");
                }

                var output = string.Join("\n", results);
                if (results.Count >= maxResults)
                {
                    output += $"\n... (truncated at {maxResults} results, use more specific pattern)";
                }

                return ToolResult.Ok(output);
            }
        });

        // file.delete - Delete a file
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "file.delete",
            Name = "Delete File",
            Description = "Delete a file. Does not delete directories.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "path": { "type": "string", "description": "Path to the file to delete (relative or absolute)" }
                    },
                    "required": ["path"]
                }
                """,
            Categories = ["file"],
            Handler = (input, ct) =>
            {
                var path = input.GetRequiredParameter<string>("path");

                // Resolve relative paths against WorkingDirectory
                var resolvedPath = ResolvePath(path, input.WorkingDirectory);

                if (!fileSystem.File.Exists(resolvedPath))
                {
                    return Task.FromResult(ToolResult.Fail($"File not found: {path}"));
                }

                fileSystem.File.Delete(resolvedPath);
                return Task.FromResult(ToolResult.Ok($"File deleted: {path}"));
            }
        });
    }

    private static void RegisterShellTools(IToolRegistry registry, IProcessRunner processRunner, ILogger logger)
    {
        // shell.execute - Run a shell command
        registry.RegisterTool(new ToolDefinition
        {
            ToolId = "shell.execute",
            Name = "Execute Shell Command",
            Description = "Execute a shell command and return the output. Use for builds, tests, git, etc.",
            InputSchema = """
                {
                    "type": "object",
                    "properties": {
                        "command": { "type": "string", "description": "The command to execute" },
                        "workingDirectory": { "type": "string", "description": "Optional working directory for the command" },
                        "timeoutSeconds": { "type": "integer", "description": "Timeout in seconds. Default is 60." }
                    },
                    "required": ["command"]
                }
                """,
            Categories = ["shell"],
            Handler = async (input, ct) =>
            {
                var command = input.GetRequiredParameter<string>("command");
                var workingDirectory = input.GetParameter<string?>("workingDirectory");
                var timeoutSeconds = input.GetParameter("timeoutSeconds", 60);

                // Use input.WorkingDirectory if no explicit workingDirectory provided
                if (string.IsNullOrEmpty(workingDirectory) && !string.IsNullOrEmpty(input.WorkingDirectory))
                {
                    workingDirectory = input.WorkingDirectory;
                }

                if (string.IsNullOrEmpty(command))
                {
                    return ToolResult.Fail("Command cannot be empty");
                }

                var options = new ProcessOptions
                {
                    WorkingDirectory = workingDirectory,
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                var result = await processRunner.RunShellAsync(command, options, ct);

                var output = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    output.AppendLine(result.StandardOutput);
                }
                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    output.AppendLine("STDERR:");
                    output.AppendLine(result.StandardError);
                }

                if (result.Success)
                {
                    return ToolResult.Ok(output.ToString().Trim());
                }
                else if (result.TimedOut)
                {
                    return ToolResult.Fail($"Command timed out after {timeoutSeconds} seconds");
                }
                else
                {
                    return ToolResult.Fail($"Command exited with code {result.ExitCode}:\n{output}".Trim());
                }
            }
        });
    }

    /// <summary>
    /// Resolves a path against a working directory. If path is absolute, returns it as-is.
    /// If path is relative and workingDirectory is provided, combines them.
    /// </summary>
    private static string ResolvePath(string? path, string? workingDirectory)
    {
        // DEBUG: Trace path resolution
        Console.WriteLine($"[BUILTIN-TOOL-DEBUG] ResolvePath: path='{path}', workingDirectory='{workingDirectory}'");

        if (string.IsNullOrEmpty(path))
        {
            return path ?? string.Empty;
        }

        // If path is already absolute, return as-is
        if (Path.IsPathRooted(path))
        {
            Console.WriteLine($"[BUILTIN-TOOL-DEBUG] Path is absolute, returning as-is: '{path}'");
            return path;
        }

        // If we have a working directory, combine with relative path
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            var resolved = Path.Combine(workingDirectory, path);
            Console.WriteLine($"[BUILTIN-TOOL-DEBUG] Combined with workingDirectory: '{resolved}'");
            return resolved;
        }

        // Fall back to resolving against current directory
        var fallback = Path.GetFullPath(path);
        Console.WriteLine($"[BUILTIN-TOOL-DEBUG] WARNING: Falling back to current directory: '{fallback}'");
        return fallback;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
