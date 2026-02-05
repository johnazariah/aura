namespace Anvil.Cli.Models;

/// <summary>
/// Metrics measuring how effectively agents use semantic indexing vs file-level tools.
/// </summary>
public sealed record IndexEffectivenessMetrics
{
    /// <summary>
    /// Total number of tool calls made during story execution.
    /// </summary>
    public required int TotalToolCalls { get; init; }

    /// <summary>
    /// Number of calls to Aura semantic tools (aura_search, aura_navigate, aura_inspect).
    /// </summary>
    public required int AuraSemanticToolCalls { get; init; }

    /// <summary>
    /// Number of calls to file-level tools (read_file, grep_search, list_dir).
    /// </summary>
    public required int FileLevelToolCalls { get; init; }

    /// <summary>
    /// Ratio of semantic tool calls to total tool calls. Target: ≥0.6 (60%).
    /// </summary>
    public required double AuraToolRatio { get; init; }

    /// <summary>
    /// Number of tool calls before finding relevant code for the task.
    /// Lower is better - indicates efficient discovery.
    /// </summary>
    public required int StepsToFirstRelevantCode { get; init; }

    /// <summary>
    /// Number of times files were read then abandoned without changes.
    /// High values indicate inefficient exploration.
    /// </summary>
    public required int BacktrackingEvents { get; init; }

    /// <summary>
    /// Behavioral patterns detected: "fishing", "guessing", "direct".
    /// </summary>
    public required IReadOnlyList<string> DetectedPatterns { get; init; }
}
