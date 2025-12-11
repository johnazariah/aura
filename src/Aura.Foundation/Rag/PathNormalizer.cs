// <copyright file="PathNormalizer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Utility for normalizing file paths for consistent storage and comparison.
/// </summary>
public static class PathNormalizer
{
    /// <summary>
    /// Normalizes a file path for consistent storage and comparison.
    /// Converts backslashes to forward slashes, handles escaped backslashes
    /// from JSON serialization, and lowercases for case-insensitive matching.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>A normalized path with forward slashes and lowercase.</returns>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Handle escaped backslashes first (JSON serialization can cause \\), then regular backslashes
        var normalized = path
            .Replace("\\\\", "/")
            .Replace('\\', '/')
            .ToLowerInvariant();

        // Collapse multiple slashes but preserve URI scheme (e.g., file://)
        var schemeEnd = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            // Preserve scheme, normalize rest
            var scheme = normalized[..(schemeEnd + 3)];
            var rest = CollapseSlashes(normalized[(schemeEnd + 3)..]);
            return scheme + rest;
        }

        return CollapseSlashes(normalized);
    }

    /// <summary>
    /// Collapses multiple consecutive forward slashes into single slashes.
    /// </summary>
    private static string CollapseSlashes(string path)
    {
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }

        return path;
    }
}
