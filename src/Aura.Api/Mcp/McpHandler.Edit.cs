using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    // =========================================================================
    // aura_edit - Surgical text editing
    // =========================================================================
    /// <summary>
    /// Surgical text editing: insert, replace, or delete lines in any file.
    /// Uses 1-based line numbers. All writes normalize to LF line endings.
    /// </summary>
    private async Task<object> EditAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString() ?? throw new ArgumentException("operation is required");
        var filePath = args?.GetProperty("filePath").GetString() ?? throw new ArgumentException("filePath is required");
        var preview = args?.TryGetProperty("preview", out var previewProp) == true && previewProp.GetBoolean();
        if (!File.Exists(filePath))
        {
            return new
            {
                success = false,
                error = $"File not found: {filePath}",
                operation,
                filePath
            };
        }

        try
        {
            // Read file content preserving original for comparison
            var originalContent = await File.ReadAllTextAsync(filePath, ct);
            var lines = originalContent.Split('\n').Select(l => l.TrimEnd('\r')) // Normalize CRLF to LF
            .ToList();
            string modifiedContent;
            string description;
            switch (operation)
            {
                case "insert_lines":
                    (modifiedContent, description) = InsertLinesOperation(args, lines, filePath);
                    break;
                case "replace_lines":
                    (modifiedContent, description) = ReplaceLinesOperation(args, lines, filePath);
                    break;
                case "delete_lines":
                    (modifiedContent, description) = DeleteLinesOperation(args, lines, filePath);
                    break;
                case "append":
                    (modifiedContent, description) = AppendOperation(args, lines, filePath);
                    break;
                case "prepend":
                    (modifiedContent, description) = PrependOperation(args, lines, filePath);
                    break;
                default:
                    throw new ArgumentException($"Unknown edit operation: {operation}");
            }

            // Normalize to LF and ensure final newline
            modifiedContent = NormalizeLineEndings(modifiedContent);
            if (preview)
            {
                return new
                {
                    success = true,
                    preview = true,
                    operation,
                    filePath,
                    description,
                    originalLineCount = lines.Count,
                    modifiedLineCount = modifiedContent.Split('\n').Length,
                    content = modifiedContent
                };
            }

            // Write the modified content
            await File.WriteAllTextAsync(filePath, modifiedContent, ct);
            return new
            {
                success = true,
                preview = false,
                operation,
                filePath,
                description,
                originalLineCount = lines.Count,
                modifiedLineCount = modifiedContent.Split('\n').Length
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            return new
            {
                success = false,
                error = ex.Message,
                operation,
                filePath
            };
        }
    }

    private static (string content, string description) InsertLinesOperation(JsonElement? args, List<string> lines, string filePath)
    {
        var line = args?.TryGetProperty("line", out var lineProp) == true ? lineProp.GetInt32() : throw new ArgumentException("line is required for insert_lines operation");
        var content = args?.TryGetProperty("content", out var contentProp) == true ? contentProp.GetString() ?? "" : throw new ArgumentException("content is required for insert_lines operation");
        // Line 0 means insert at the very beginning
        // Line N means insert after line N
        if (line < 0 || line > lines.Count)
        {
            throw new ArgumentException($"line {line} is out of range. File has {lines.Count} lines. Use 0 to insert at beginning, or 1-{lines.Count} to insert after that line.");
        }

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        // Insert the new lines
        lines.InsertRange(line, newLines);
        var description = line == 0 ? $"Inserted {newLines.Count} line(s) at the beginning" : $"Inserted {newLines.Count} line(s) after line {line}";
        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) ReplaceLinesOperation(JsonElement? args, List<string> lines, string filePath)
    {
        var startLine = args?.TryGetProperty("startLine", out var startProp) == true ? startProp.GetInt32() : throw new ArgumentException("startLine is required for replace_lines operation");
        var endLine = args?.TryGetProperty("endLine", out var endProp) == true ? endProp.GetInt32() : throw new ArgumentException("endLine is required for replace_lines operation");
        var content = args?.TryGetProperty("content", out var contentProp) == true ? contentProp.GetString() ?? "" : throw new ArgumentException("content is required for replace_lines operation");
        // Validate range (1-based)
        if (startLine < 1 || startLine > lines.Count)
        {
            throw new ArgumentException($"startLine {startLine} is out of range. File has {lines.Count} lines (1-based).");
        }

        if (endLine < startLine || endLine > lines.Count)
        {
            throw new ArgumentException($"endLine {endLine} is invalid. Must be >= startLine ({startLine}) and <= {lines.Count}.");
        }

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        // Convert to 0-based index
        var startIndex = startLine - 1;
        var endIndex = endLine - 1;
        var countToRemove = endIndex - startIndex + 1;
        // Remove the old lines and insert new ones
        lines.RemoveRange(startIndex, countToRemove);
        lines.InsertRange(startIndex, newLines);
        var description = $"Replaced lines {startLine}-{endLine} ({countToRemove} line(s)) with {newLines.Count} line(s)";
        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) DeleteLinesOperation(JsonElement? args, List<string> lines, string filePath)
    {
        var startLine = args?.TryGetProperty("startLine", out var startProp) == true ? startProp.GetInt32() : throw new ArgumentException("startLine is required for delete_lines operation");
        var endLine = args?.TryGetProperty("endLine", out var endProp) == true ? endProp.GetInt32() : throw new ArgumentException("endLine is required for delete_lines operation");
        // Validate range (1-based)
        if (startLine < 1 || startLine > lines.Count)
        {
            throw new ArgumentException($"startLine {startLine} is out of range. File has {lines.Count} lines (1-based).");
        }

        if (endLine < startLine || endLine > lines.Count)
        {
            throw new ArgumentException($"endLine {endLine} is invalid. Must be >= startLine ({startLine}) and <= {lines.Count}.");
        }

        // Convert to 0-based index
        var startIndex = startLine - 1;
        var endIndex = endLine - 1;
        var countToRemove = endIndex - startIndex + 1;
        lines.RemoveRange(startIndex, countToRemove);
        var description = $"Deleted lines {startLine}-{endLine} ({countToRemove} line(s))";
        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) AppendOperation(JsonElement? args, List<string> lines, string filePath)
    {
        var content = args?.TryGetProperty("content", out var contentProp) == true ? contentProp.GetString() ?? "" : throw new ArgumentException("content is required for append operation");
        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.AddRange(newLines);
        var description = $"Appended {newLines.Count} line(s) at end of file";
        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) PrependOperation(JsonElement? args, List<string> lines, string filePath)
    {
        var content = args?.TryGetProperty("content", out var contentProp) == true ? contentProp.GetString() ?? "" : throw new ArgumentException("content is required for prepend operation");
        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.InsertRange(0, newLines);
        var description = $"Prepended {newLines.Count} line(s) at beginning of file";
        return (string.Join("\n", lines), description);
    }

    /// <summary>
    /// Normalizes content to LF line endings and ensures a trailing newline.
    /// </summary>
    private static string NormalizeLineEndings(string content)
    {
        // Replace any CRLF with LF
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        // Ensure trailing newline
        if (!content.EndsWith('\n'))
        {
            content += '\n';
        }

        return content;
    }
}
