// <copyright file="ReadFileTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the read_file tool.
/// </summary>
public record ReadFileInput
{
    /// <summary>Path to the file to read (relative or absolute)</summary>
    public required string FilePath { get; init; }

    /// <summary>Starting line number (1-indexed, default is 1)</summary>
    public int? StartLine { get; init; }

    /// <summary>Ending line number (inclusive, default is end of file)</summary>
    public int? EndLine { get; init; }

    /// <summary>Include line numbers in output</summary>
    public bool IncludeLineNumbers { get; init; } = true;
}

/// <summary>
/// Output from the read_file tool.
/// </summary>
public record ReadFileOutput
{
    /// <summary>Full path to the file</summary>
    public required string FilePath { get; init; }

    /// <summary>File content (possibly truncated to requested lines)</summary>
    public required string Content { get; init; }

    /// <summary>Total lines in file</summary>
    public int TotalLines { get; init; }

    /// <summary>Lines returned (may be subset)</summary>
    public int LinesReturned { get; init; }

    /// <summary>Starting line of returned content</summary>
    public int StartLine { get; init; }

    /// <summary>Ending line of returned content</summary>
    public int EndLine { get; init; }

    /// <summary>File encoding detected</summary>
    public string? Encoding { get; init; }
}

/// <summary>
/// Reads content from a file in the workspace.
/// Supports reading specific line ranges.
/// </summary>
public class ReadFileTool : TypedToolBase<ReadFileInput, ReadFileOutput>
{
    private readonly ILogger<ReadFileTool> _logger;

    public ReadFileTool(ILogger<ReadFileTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "file.read";

    /// <inheritdoc/>
    public override string Name => "Read File";

    /// <inheritdoc/>
    public override string Description =>
        "Reads content from a file. Supports reading specific line ranges with start_line and end_line. " +
        "Returns file content with optional line numbers. Use this to examine existing code " +
        "or understand file structure.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["file", "io"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<ReadFileOutput>> ExecuteAsync(
        ReadFileInput input,
        CancellationToken ct = default)
    {
        var filePath = Path.GetFullPath(input.FilePath);
        _logger.LogInformation("Reading file: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            return ToolResult<ReadFileOutput>.Fail($"File not found: {filePath}");
        }

        try
        {
            var allLines = await File.ReadAllLinesAsync(filePath, ct);
            var totalLines = allLines.Length;

            // Calculate line range
            var startLine = Math.Max(1, input.StartLine ?? 1);
            var endLine = Math.Min(totalLines, input.EndLine ?? totalLines);

            if (startLine > totalLines)
            {
                return ToolResult<ReadFileOutput>.Fail(
                    $"Start line {startLine} is beyond file length ({totalLines} lines)");
            }

            // Extract requested lines (convert to 0-indexed)
            var selectedLines = allLines
                .Skip(startLine - 1)
                .Take(endLine - startLine + 1)
                .ToArray();

            // Build content string
            string content;
            if (input.IncludeLineNumbers)
            {
                var lineWidth = endLine.ToString().Length;
                content = string.Join(Environment.NewLine,
                    selectedLines.Select((line, idx) =>
                        $"{(startLine + idx).ToString().PadLeft(lineWidth)}: {line}"));
            }
            else
            {
                content = string.Join(Environment.NewLine, selectedLines);
            }

            var output = new ReadFileOutput
            {
                FilePath = filePath,
                Content = content,
                TotalLines = totalLines,
                LinesReturned = selectedLines.Length,
                StartLine = startLine,
                EndLine = endLine,
                Encoding = "UTF-8", // Simplified
            };

            _logger.LogInformation("Read {LinesReturned} lines from {FilePath}",
                selectedLines.Length, filePath);

            return ToolResult<ReadFileOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file: {FilePath}", filePath);
            return ToolResult<ReadFileOutput>.Fail($"Failed to read file: {ex.Message}");
        }
    }
}
