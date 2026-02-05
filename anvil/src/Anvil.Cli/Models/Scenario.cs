namespace Anvil.Cli.Models;

/// <summary>
/// Represents a test scenario loaded from a YAML file.
/// </summary>
public sealed record Scenario
{
    /// <summary>
    /// Unique name identifying the scenario.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what the scenario tests.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Programming language used in this scenario (e.g., "csharp", "python").
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Path to the repository where the story will be executed.
    /// </summary>
    public required string Repository { get; init; }

    /// <summary>
    /// The story definition to submit to Aura.
    /// </summary>
    public required StoryDefinition Story { get; init; }

    /// <summary>
    /// List of expectations to validate after story execution.
    /// </summary>
    public required IReadOnlyList<Expectation> Expectations { get; init; }

    /// <summary>
    /// Maximum time in seconds to wait for story completion.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Step executor to use: "internal" (Aura ReAct agents) or "copilot" (GitHub Copilot CLI).
    /// If not specified, uses Aura's default (typically "copilot").
    /// </summary>
    public string? Executor { get; init; }

    /// <summary>
    /// Optional tags for filtering scenarios.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Path to the YAML file this scenario was loaded from (set by loader).
    /// </summary>
    public string? FilePath { get; init; }
}

/// <summary>
/// Defines the story to be created in Aura.
/// </summary>
public sealed record StoryDefinition
{
    /// <summary>
    /// Title of the story.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description of the development task.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Defines an expectation to validate after story execution.
/// </summary>
public sealed record Expectation
{
    /// <summary>
    /// Type of expectation: compiles, tests_pass, file_exists, file_contains, index_usage.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Human-readable description of what this expectation validates.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// File path for file_exists and file_contains expectations (relative to worktree).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Regex pattern for file_contains expectation.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Minimum Aura semantic tool ratio for index_usage expectation (0.0 to 1.0).
    /// </summary>
    public double? MinAuraToolRatio { get; init; }

    /// <summary>
    /// Maximum steps to reach target code for index_usage expectation.
    /// </summary>
    public int? MaxStepsToTarget { get; init; }
}
