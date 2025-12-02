// <copyright file="ModifyFileTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the modify_file tool.
/// </summary>
public record ModifyFileInput
{
    /// <summary>Path to the file to modify</summary>
    public required string FilePath { get; init; }

    /// <summary>Text to find (must match exactly)</summary>
    public required string OldText { get; init; }

    /// <summary>Text to replace with</summary>
    public required string NewText { get; init; }

    /// <summary>Whether to replace all occurrences (default: false, replaces first only)</summary>
    public bool ReplaceAll { get; init; }

    /// <summary>Create backup before modifying</summary>
    public bool CreateBackup { get; init; }
}

/// <summary>
/// Output from the modify_file tool.
/// </summary>
public record ModifyFileOutput
{
    /// <summary>Full path to the modified file</summary>
    public required string FilePath { get; init; }

    /// <summary>Number of replacements made</summary>
    public int ReplacementsMade { get; init; }

    /// <summary>Path to backup file if created</summary>
    public string? BackupPath { get; init; }

    /// <summary>Whether the file was modified</summary>
    public bool WasModified => ReplacementsMade > 0;
}

/// <summary>
/// Modifies a file by replacing text.
/// Requires confirmation since it modifies existing files.
/// </summary>
public class ModifyFileTool : TypedToolBase<ModifyFileInput, ModifyFileOutput>
{
    private readonly ILogger<ModifyFileTool> _logger;

    public ModifyFileTool(ILogger<ModifyFileTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "file.modify";

    /// <inheritdoc/>
    public override string Name => "Modify File";

    /// <inheritdoc/>
    public override string Description =>
        "Modifies an existing file by finding and replacing text. " +
        "Requires exact match of 'old_text' to ensure precision. " +
        "By default replaces first occurrence only. Use 'replace_all' for multiple replacements. " +
        "CAUTION: This modifies existing code. Consider creating a backup.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["file", "io"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => true; // Modifies filesystem

    /// <inheritdoc/>
    public override async Task<ToolResult<ModifyFileOutput>> ExecuteAsync(
        ModifyFileInput input,
        CancellationToken ct = default)
    {
        var filePath = Path.GetFullPath(input.FilePath);
        _logger.LogInformation("Modifying file: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            return ToolResult<ModifyFileOutput>.Fail($"File not found: {filePath}");
        }

        if (string.IsNullOrEmpty(input.OldText))
        {
            return ToolResult<ModifyFileOutput>.Fail("old_text cannot be empty");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);

            // Check if old text exists
            if (!content.Contains(input.OldText))
            {
                return ToolResult<ModifyFileOutput>.Fail(
                    $"Could not find the specified text to replace in {filePath}. " +
                    "Make sure 'old_text' matches exactly including whitespace and line endings.");
            }

            // Create backup if requested
            string? backupPath = null;
            if (input.CreateBackup)
            {
                backupPath = filePath + ".bak";
                await File.WriteAllTextAsync(backupPath, content, ct);
                _logger.LogInformation("Created backup: {BackupPath}", backupPath);
            }

            // Perform replacement
            string newContent;
            int replacementCount;

            if (input.ReplaceAll)
            {
                replacementCount = CountOccurrences(content, input.OldText);
                newContent = content.Replace(input.OldText, input.NewText);
            }
            else
            {
                // Replace only first occurrence
                var index = content.IndexOf(input.OldText);
                if (index >= 0)
                {
                    newContent = content[..index] + input.NewText + content[(index + input.OldText.Length)..];
                    replacementCount = 1;
                }
                else
                {
                    newContent = content;
                    replacementCount = 0;
                }
            }

            // Write modified content
            await File.WriteAllTextAsync(filePath, newContent, ct);

            var output = new ModifyFileOutput
            {
                FilePath = filePath,
                ReplacementsMade = replacementCount,
                BackupPath = backupPath,
            };

            _logger.LogInformation(
                "Modified file {FilePath}: {ReplacementCount} replacement(s) made",
                filePath,
                replacementCount);

            return ToolResult<ModifyFileOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to modify file: {FilePath}", filePath);
            return ToolResult<ModifyFileOutput>.Fail($"Failed to modify file: {ex.Message}");
        }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
