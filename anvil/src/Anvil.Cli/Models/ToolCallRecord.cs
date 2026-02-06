namespace Anvil.Cli.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A record of a tool call made during story step execution.
/// </summary>
/// <param name="Tool">The name of the tool that was called.</param>
/// <param name="Arguments">Arguments passed to the tool (typically JSON).</param>
/// <param name="Result">Result returned by the tool, if any.</param>
public sealed record ToolCallRecord(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("arguments")] string Arguments,
    [property: JsonPropertyName("result")] string? Result)
{
    /// <summary>
    /// Gets the tool name (alias for Tool property for backwards compatibility).
    /// </summary>
    [JsonIgnore]
    public string ToolName => Tool;
}
