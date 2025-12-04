// <copyright file="Capabilities.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

/// <summary>
/// Fixed capability vocabulary for agent routing.
/// Capabilities determine which agents can handle which types of tasks.
/// </summary>
/// <remarks>
/// <para>
/// Capabilities are a closed vocabulary - only these values are valid.
/// For open-ended categorization, use Tags instead.
/// </para>
/// <para>
/// Design rationale: See ADR-011 (Two-Tier Capability Model).
/// </para>
/// </remarks>
public static class Capabilities
{
    // =====================
    // Foundation Capabilities (always required)
    // =====================

    /// <summary>
    /// General conversation and Q&amp;A.
    /// </summary>
    public const string Chat = "chat";

    /// <summary>
    /// Prefix for ingestion capabilities (e.g., "ingest:cs", "ingest:py", "ingest:*").
    /// </summary>
    public const string IngestPrefix = "ingest:";

    /// <summary>
    /// Wildcard ingestion capability for generic code parsing.
    /// </summary>
    public const string IngestWildcard = "ingest:*";

    // =====================
    // Developer Module Capabilities
    // =====================

    /// <summary>
    /// Enriching raw input with structured context, research, and codebase knowledge.
    /// </summary>
    public const string Enrichment = "enrichment";

    /// <summary>
    /// Requirements analysis and planning.
    /// </summary>
    public const string Analysis = "analysis";

    /// <summary>
    /// Writing or modifying code.
    /// </summary>
    public const string Coding = "coding";

    /// <summary>
    /// Fixing build errors, test failures, or other issues.
    /// </summary>
    public const string Fixing = "fixing";

    /// <summary>
    /// Writing documentation, READMEs, CHANGELOGs.
    /// </summary>
    public const string Documentation = "documentation";

    /// <summary>
    /// Code review and quality feedback.
    /// </summary>
    public const string Review = "review";

    /// <summary>
    /// Foundation capabilities required for basic operation.
    /// </summary>
    public static readonly IReadOnlyList<string> Foundation = new[]
    {
        Chat,
    };

    /// <summary>
    /// Developer module capabilities required for workflow execution.
    /// </summary>
    public static readonly IReadOnlyList<string> Developer = new[]
    {
        Enrichment,
        Analysis,
        Coding,
        Fixing,
        Documentation,
        Review,
    };

    /// <summary>
    /// All valid capability base names (not including parameterized capabilities like ingest:X).
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Chat,
        Enrichment,
        Analysis,
        Coding,
        Fixing,
        Documentation,
        Review,
    };

    /// <summary>
    /// Checks if a capability is valid (in the fixed vocabulary or a valid pattern).
    /// </summary>
    /// <param name="capability">The capability to check.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValid(string capability)
    {
        // Check if it's a base capability
        if (All.Contains(capability))
        {
            return true;
        }

        // Check if it's a parameterized capability (e.g., "ingest:cs", "ingest:*")
        if (capability.StartsWith(IngestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
