namespace Anvil.Cli.Models;

/// <summary>
/// Result of executing a single story scenario.
/// </summary>
public sealed record StoryResult
{
    /// <summary>
    /// The scenario that was executed.
    /// </summary>
    public required Scenario Scenario { get; init; }

    /// <summary>
    /// Whether the scenario passed all expectations.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Time taken to execute the story.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// ID of the story created in Aura (if created).
    /// </summary>
    public Guid? StoryId { get; init; }

    /// <summary>
    /// Path to the worktree where the story was executed.
    /// </summary>
    public string? WorktreePath { get; init; }

    /// <summary>
    /// Error message if the story failed to execute.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Results of individual expectation validations.
    /// </summary>
    public IReadOnlyList<ExpectationResult> ExpectationResults { get; init; } = [];
}

/// <summary>
/// Result of validating a single expectation.
/// </summary>
public sealed record ExpectationResult
{
    /// <summary>
    /// The expectation that was validated.
    /// </summary>
    public required Expectation Expectation { get; init; }

    /// <summary>
    /// Whether the expectation passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Additional message describing the result.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Index effectiveness metrics (for index_usage expectations).
    /// </summary>
    public IndexEffectivenessMetrics? IndexMetrics { get; init; }
}

/// <summary>
/// Result of executing an entire test suite.
/// </summary>
public sealed record SuiteResult
{
    /// <summary>
    /// Results of all story executions.
    /// </summary>
    public required IReadOnlyList<StoryResult> Results { get; init; }

    /// <summary>
    /// Total time taken to execute all scenarios.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// When the suite execution started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the suite execution completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Number of scenarios that passed.
    /// </summary>
    public int Passed => Results.Count(r => r.Success);

    /// <summary>
    /// Number of scenarios that failed.
    /// </summary>
    public int Failed => Results.Count(r => !r.Success);

    /// <summary>
    /// Total number of scenarios executed.
    /// </summary>
    public int Total => Results.Count;
}
