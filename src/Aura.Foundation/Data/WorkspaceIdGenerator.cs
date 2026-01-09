// <copyright file="WorkspaceIdGenerator.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Data;

using System.Security.Cryptography;
using System.Text;
using Aura.Foundation.Rag;

/// <summary>
/// Generates deterministic workspace IDs from file paths.
/// </summary>
public static class WorkspaceIdGenerator
{
    /// <summary>
    /// The length of the generated workspace ID in characters.
    /// Using 16 hex characters = 64 bits, which has negligible collision probability.
    /// </summary>
    public const int IdLength = 16;

    /// <summary>
    /// Generates a deterministic workspace ID from a file path.
    /// The same path (after normalization) will always produce the same ID.
    /// </summary>
    /// <param name="path">The workspace path (can use any slash style or casing).</param>
    /// <returns>A 16-character lowercase hex string.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public static string GenerateId(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = PathNormalizer.Normalize(path);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);

        // Use first 16 hex chars (64 bits) - collision probability negligible for realistic workload
        return Convert.ToHexString(hash)[..IdLength].ToLowerInvariant();
    }

    /// <summary>
    /// Validates that a string is a valid workspace ID format.
    /// </summary>
    /// <param name="id">The ID to validate.</param>
    /// <returns>True if the ID is valid; otherwise false.</returns>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != IdLength)
        {
            return false;
        }

        // Must be all lowercase hex characters
        foreach (var c in id)
        {
            if (!char.IsAsciiHexDigitLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
