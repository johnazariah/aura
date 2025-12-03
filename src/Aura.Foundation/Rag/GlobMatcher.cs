// <copyright file="GlobMatcher.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Simple glob pattern matcher for file paths.
/// Supports common patterns like *.cs, **/dir/**, **/file.txt
/// </summary>
public static class GlobMatcher
{
    /// <summary>
    /// Checks if a file path matches a glob pattern.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="pattern">The glob pattern (e.g., "*.cs", "**/bin/**", "**/node_modules/**").</param>
    /// <returns>True if the path matches the pattern.</returns>
    public static bool Matches(string filePath, string pattern)
    {
        // Normalize path separators for consistent matching
        var normalizedPath = filePath.Replace('\\', '/');

        // Handle **/dir/** patterns (match directory anywhere in path)
        if (pattern.StartsWith("**/"))
        {
            var suffix = pattern[3..];
            if (suffix.EndsWith("/**"))
            {
                var dirName = "/" + suffix[..^3] + "/";
                return normalizedPath.Contains(dirName, StringComparison.OrdinalIgnoreCase);
            }

            // Handle **/pattern (match anywhere in path)
            return normalizedPath.Contains(suffix, StringComparison.OrdinalIgnoreCase);
        }

        // Handle *.ext patterns (match file extension)
        if (pattern.StartsWith("*."))
        {
            return filePath.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        // Simple contains match
        return normalizedPath.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a file path matches any of the given patterns.
    /// </summary>
    public static bool MatchesAny(string filePath, IEnumerable<string> patterns)
    {
        return patterns.Any(p => Matches(filePath, p));
    }

    /// <summary>
    /// Checks if a file path should be included based on include/exclude patterns.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="includePatterns">Patterns that must match for inclusion.</param>
    /// <param name="excludePatterns">Patterns that exclude if matched.</param>
    /// <returns>True if the file should be included.</returns>
    public static bool ShouldInclude(
        string filePath,
        IEnumerable<string> includePatterns,
        IEnumerable<string> excludePatterns)
    {
        // First check exclusions
        if (MatchesAny(filePath, excludePatterns))
        {
            return false;
        }

        // Then check inclusions
        return MatchesAny(filePath, includePatterns);
    }
}
