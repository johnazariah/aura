using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Provides built-in tools for file operations, shell commands, etc.
/// </summary>
public static class BuiltInTools
{
    /// <summary>
    /// Register all built-in tools with the registry.
    /// </summary>
    public static void RegisterBuiltInTools(
        IToolRegistry registry, 
        IFileSystem fileSystem,
        Shell.IProcessRunner processRunner,
        ILogger logger)
    {
        // File tools
        registry.RegisterTool(CreateFileReadTool(fileSystem, logger));
        registry.RegisterTool(CreateFileWriteTool(fileSystem, logger));
        registry.RegisterTool(CreateFileListTool(fileSystem, logger));
        registry.RegisterTool(CreateFileExistsTool(fileSystem, logger));
        registry.RegisterTool(CreateFileDeleteTool(fileSystem, logger));
        
        // Shell tools
        registry.RegisterTool(CreateShellExecuteTool(processRunner, logger));
        
        logger.LogInformation("Registered {Count} built-in tools", 6);
    }
    
    private static ToolDefinition CreateFileReadTool(IFileSystem fs, ILogger logger) => new()
    {
        ToolId = "file.read",
        Name = "Read File",
        Description = "Read the contents of a file",
        Categories = ["file", "io"],
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to the file to read" }
            },
            "required": ["path"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            
            if (!fs.File.Exists(path))
                return ToolResult.Fail($"File not found: {path}");
            
            var content = await fs.File.ReadAllTextAsync(path, ct);
            logger.LogDebug("Read {Length} chars from {Path}", content.Length, path);
            
            return ToolResult.Ok(content);
        }
    };
    
    private static ToolDefinition CreateFileWriteTool(IFileSystem fs, ILogger logger) => new()
    {
        ToolId = "file.write",
        Name = "Write File",
        Description = "Write content to a file",
        Categories = ["file", "io"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to the file to write" },
                "content": { "type": "string", "description": "Content to write" },
                "append": { "type": "boolean", "description": "Append instead of overwrite", "default": false }
            },
            "required": ["path", "content"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var content = input.GetRequiredParameter<string>("content");
            var append = input.GetParameter("append", false);
            
            // Ensure directory exists
            var dir = fs.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
                fs.Directory.CreateDirectory(dir);
            
            if (append)
                await fs.File.AppendAllTextAsync(path, content, ct);
            else
                await fs.File.WriteAllTextAsync(path, content, ct);
            
            logger.LogDebug("Wrote {Length} chars to {Path}", content.Length, path);
            
            return ToolResult.Ok(new { path, bytesWritten = content.Length });
        }
    };
    
    private static ToolDefinition CreateFileListTool(IFileSystem fs, ILogger logger) => new()
    {
        ToolId = "file.list",
        Name = "List Directory",
        Description = "List files and directories in a path",
        Categories = ["file", "io"],
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Directory path to list" },
                "pattern": { "type": "string", "description": "Search pattern (e.g., *.cs)", "default": "*" },
                "recursive": { "type": "boolean", "description": "Include subdirectories", "default": false }
            },
            "required": ["path"]
        }
        """,
        Handler = (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var pattern = input.GetParameter("pattern", "*")!;
            var recursive = input.GetParameter("recursive", false);
            
            if (!fs.Directory.Exists(path))
                return Task.FromResult(ToolResult.Fail($"Directory not found: {path}"));
            
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            var entries = fs.Directory.GetFileSystemEntries(path, pattern, searchOption)
                .Select(e => new 
                {
                    path = e,
                    name = fs.Path.GetFileName(e),
                    isDirectory = fs.Directory.Exists(e)
                })
                .ToList();
            
            logger.LogDebug("Listed {Count} entries in {Path}", entries.Count, path);
            
            return Task.FromResult(ToolResult.Ok(entries));
        }
    };
    
    private static ToolDefinition CreateFileExistsTool(IFileSystem fs, ILogger logger) => new()
    {
        ToolId = "file.exists",
        Name = "File Exists",
        Description = "Check if a file or directory exists",
        Categories = ["file", "io"],
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to check" }
            },
            "required": ["path"]
        }
        """,
        Handler = (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var fileExists = fs.File.Exists(path);
            var dirExists = fs.Directory.Exists(path);
            
            return Task.FromResult(ToolResult.Ok(new 
            { 
                exists = fileExists || dirExists,
                isFile = fileExists,
                isDirectory = dirExists
            }));
        }
    };
    
    private static ToolDefinition CreateFileDeleteTool(IFileSystem fs, ILogger logger) => new()
    {
        ToolId = "file.delete",
        Name = "Delete File",
        Description = "Delete a file or directory",
        Categories = ["file", "io"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Path to delete" },
                "recursive": { "type": "boolean", "description": "Delete directory recursively", "default": false }
            },
            "required": ["path"]
        }
        """,
        Handler = (input, ct) =>
        {
            var path = input.GetRequiredParameter<string>("path");
            var recursive = input.GetParameter("recursive", false);
            
            if (fs.File.Exists(path))
            {
                fs.File.Delete(path);
                logger.LogDebug("Deleted file: {Path}", path);
                return Task.FromResult(ToolResult.Ok(new { deleted = path, type = "file" }));
            }
            
            if (fs.Directory.Exists(path))
            {
                fs.Directory.Delete(path, recursive);
                logger.LogDebug("Deleted directory: {Path}", path);
                return Task.FromResult(ToolResult.Ok(new { deleted = path, type = "directory" }));
            }
            
            return Task.FromResult(ToolResult.Fail($"Path not found: {path}"));
        }
    };
    
    private static ToolDefinition CreateShellExecuteTool(Shell.IProcessRunner runner, ILogger logger) => new()
    {
        ToolId = "shell.execute",
        Name = "Execute Shell Command",
        Description = "Run a shell command and return output",
        Categories = ["shell", "system"],
        RequiresConfirmation = true,
        InputSchema = """
        {
            "type": "object",
            "properties": {
                "command": { "type": "string", "description": "Command to execute" },
                "args": { "type": "array", "items": { "type": "string" }, "description": "Command arguments" },
                "workingDirectory": { "type": "string", "description": "Working directory" },
                "timeout": { "type": "integer", "description": "Timeout in seconds", "default": 60 }
            },
            "required": ["command"]
        }
        """,
        Handler = async (input, ct) =>
        {
            var command = input.GetRequiredParameter<string>("command");
            var args = input.GetParameter<string[]>("args", []) ?? [];
            var workingDir = input.GetParameter<string?>("workingDirectory", input.WorkingDirectory);
            var timeout = input.GetParameter("timeout", 60);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            
            var result = await runner.RunAsync(command, args, new Shell.ProcessOptions
            {
                WorkingDirectory = workingDir
            }, cts.Token);
            
            logger.LogDebug("Shell command completed: {Command}, exit={ExitCode}", command, result.ExitCode);
            
            if (result.ExitCode == 0)
            {
                return ToolResult.Ok(new
                {
                    exitCode = result.ExitCode,
                    stdout = result.StandardOutput,
                    stderr = result.StandardError
                });
            }
            
            return ToolResult.Fail($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
        }
    };
}
