// <copyright file="WriteFileTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the write_file tool.
/// </summary>
public record WriteFileInput
{
    /// <summary>Path to the file to write (relative or absolute)</summary>
    public required string FilePath { get; init; }

    /// <summary>Content to write to the file</summary>
    public required string Content { get; init; }

    /// <summary>Whether to overwrite if file exists (default: false for safety)</summary>
    public bool Overwrite { get; init; }

    /// <summary>Create directories if they don't exist</summary>
    public bool CreateDirectories { get; init; } = true;
}

/// <summary>
/// Output from the write_file tool.
/// </summary>
public record WriteFileOutput
{
    /// <summary>Full path to the written file</summary>
    public required string FilePath { get; init; }

    /// <summary>Whether a new file was created (vs. overwritten)</summary>
    public bool WasCreated { get; init; }

    /// <summary>Number of bytes written</summary>
    public long BytesWritten { get; init; }

    /// <summary>Number of lines in the content</summary>
    public int LineCount { get; init; }
}

/// <summary>
/// Creates or overwrites a file with the specified content.
/// Requires confirmation since it modifies the filesystem.
/// </summary>
public class WriteFileTool : TypedToolBase<WriteFileInput, WriteFileOutput>
{
    private readonly ILogger<WriteFileTool> _logger;

    public WriteFileTool(ILogger<WriteFileTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "file.write";

    /// <inheritdoc/>
    public override string Name => "Write File";

    /// <inheritdoc/>
    public override string Description =>
        "Creates or overwrites a file with the specified content. " +
        "By default, will not overwrite existing files unless 'overwrite' is true. " +
        "Use this to create new source files, test files, or configuration. " +
        "CAUTION: This modifies the filesystem.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["file", "io"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => true; // Modifies filesystem

    /// <inheritdoc/>
    public override async Task<ToolResult<WriteFileOutput>> ExecuteAsync(
        WriteFileInput input,
        CancellationToken ct = default)
    {
        var filePath = Path.GetFullPath(input.FilePath);
        _logger.LogInformation("Writing file: {FilePath}", filePath);

        var fileExists = File.Exists(filePath);

        if (fileExists && !input.Overwrite)
        {
            return ToolResult<WriteFileOutput>.Fail(
                $"File already exists: {filePath}. Set 'overwrite' to true to replace.");
        }

        try
        {
            // Create directory if needed
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                if (input.CreateDirectories)
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation("Created directory: {Directory}", directory);
                }
                else
                {
                    return ToolResult<WriteFileOutput>.Fail(
                        $"Directory does not exist: {directory}. Set 'create_directories' to true to create.");
                }
            }

            // Write the file
            await File.WriteAllTextAsync(filePath, input.Content, ct);

            var fileInfo = new FileInfo(filePath);
            var lineCount = input.Content.Split('\n').Length;

            var output = new WriteFileOutput
            {
                FilePath = filePath,
                WasCreated = !fileExists,
                BytesWritten = fileInfo.Length,
                LineCount = lineCount,
            };

            _logger.LogInformation(
                "{Action} file {FilePath} ({Bytes} bytes, {Lines} lines)",
                fileExists ? "Overwrote" : "Created",
                filePath,
                fileInfo.Length,
                lineCount);

            return ToolResult<WriteFileOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file: {FilePath}", filePath);
            return ToolResult<WriteFileOutput>.Fail($"Failed to write file: {ex.Message}");
        }
    }
}
