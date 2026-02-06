namespace Anvil.Cli.Services;

using Anvil.Cli.Models;

/// <summary>
/// Analyzes tool call traces to measure index effectiveness.
/// </summary>
public interface IIndexEffectivenessAnalyzer
{
    /// <summary>
    /// Analyzes tool call trace and returns effectiveness metrics.
    /// </summary>
    /// <param name="toolTrace">The tool calls to analyze.</param>
    /// <returns>Metrics describing how effectively the index was used.</returns>
    IndexEffectivenessMetrics Analyze(IReadOnlyList<ToolCallRecord> toolTrace);
}
