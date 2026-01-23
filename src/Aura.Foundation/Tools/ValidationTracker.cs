// <copyright file="ValidationTracker.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

/// <summary>
/// Tracks code file modifications and validation state during ReAct execution.
/// Ensures agents cannot finish without validating their code changes.
/// </summary>
public sealed class ValidationTracker
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csx",           // C#
        ".ts", ".tsx", ".mts",   // TypeScript
        ".js", ".jsx", ".mjs",   // JavaScript
        ".py", ".pyw",           // Python
        ".go",                   // Go
        ".rs",                   // Rust
        ".java",                 // Java
        ".fs", ".fsx",           // F#
        ".rb",                   // Ruby
        ".cpp", ".hpp", ".c", ".h", ".cc", ".cxx",  // C/C++
        ".swift",                // Swift
        ".kt", ".kts",           // Kotlin
    };

    private readonly HashSet<string> _modifiedFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _consecutiveFailures;

    /// <summary>
    /// Gets a value indicating whether there are code files modified since last successful validation.
    /// </summary>
    public bool HasUnvalidatedChanges => _modifiedFiles.Count > 0;

    /// <summary>
    /// Gets the number of consecutive validation failures.
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Gets the list of modified files that haven't been validated.
    /// </summary>
    public IReadOnlyCollection<string> ModifiedFiles => _modifiedFiles;

    /// <summary>
    /// Gets the maximum number of validation failures before force-failing.
    /// </summary>
    public int MaxFailures { get; init; } = 5;

    /// <summary>
    /// Track a file modification. Only code files are tracked.
    /// Called by file.write, file.modify, code.generate, code.refactor tools.
    /// </summary>
    /// <param name="filePath">Path to the modified file.</param>
    public void TrackFileChange(string filePath)
    {
        if (IsCodeFile(filePath))
        {
            _modifiedFiles.Add(NormalizePath(filePath));
        }
    }

    /// <summary>
    /// Track multiple file modifications.
    /// </summary>
    /// <param name="filePaths">Paths to the modified files.</param>
    public void TrackFileChanges(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            TrackFileChange(path);
        }
    }

    /// <summary>
    /// Record the result of a validation attempt.
    /// On success, clears tracked files and resets failure count.
    /// On failure, increments failure count.
    /// </summary>
    /// <param name="success">Whether validation passed.</param>
    public void RecordValidationResult(bool success)
    {
        if (success)
        {
            _modifiedFiles.Clear();
            _consecutiveFailures = 0;
        }
        else
        {
            _consecutiveFailures++;
        }
    }

    /// <summary>
    /// Check if the maximum number of validation failures has been reached.
    /// </summary>
    public bool HasExceededMaxFailures => _consecutiveFailures >= MaxFailures;

    /// <summary>
    /// Reset the tracker to initial state.
    /// </summary>
    public void Reset()
    {
        _modifiedFiles.Clear();
        _consecutiveFailures = 0;
    }

    /// <summary>
    /// Check if a file path is a code file that should be tracked.
    /// </summary>
    private static bool IsCodeFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && CodeExtensions.Contains(extension);
    }

    /// <summary>
    /// Normalize file path for consistent tracking.
    /// </summary>
    private static string NormalizePath(string filePath)
    {
        // Convert to forward slashes and remove leading ./ or .\
        var normalized = filePath.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }
}
