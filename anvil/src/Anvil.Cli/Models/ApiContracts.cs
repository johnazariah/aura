namespace Anvil.Cli.Models;

/// <summary>
/// Request to create a new story in Aura.
/// </summary>
public sealed record CreateStoryRequest
{
    /// <summary>
    /// Title of the story.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Description of the development task.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Path to the repository where the story will be executed.
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Automation mode for story execution.
    /// </summary>
    public string AutomationMode { get; init; } = "Autonomous";

    /// <summary>
    /// Preferred step executor: "internal" (Aura ReAct agents) or "copilot" (GitHub Copilot CLI).
    /// </summary>
    public string? PreferredExecutor { get; init; }
}

/// <summary>
/// Response from Aura API for story operations.
/// </summary>
public sealed record StoryResponse
{
    /// <summary>
    /// Unique identifier of the story.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Title of the story (not always returned by all endpoints).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Description of the story.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Current status of the story (Created, Analyzing, Planning, Running, Completed, Failed).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Path to the git worktree for this story.
    /// </summary>
    public string? WorktreePath { get; init; }

    /// <summary>
    /// Git branch created for this story.
    /// </summary>
    public string? GitBranch { get; init; }

    /// <summary>
    /// Current wave being executed (1-based).
    /// </summary>
    public int CurrentWave { get; init; }

    /// <summary>
    /// Total number of waves in the execution plan.
    /// </summary>
    public int WaveCount { get; init; }

    /// <summary>
    /// Gate mode for wave transitions (AutoProceed, ManualApproval).
    /// </summary>
    public string? GateMode { get; init; }

    /// <summary>
    /// Steps generated for this story.
    /// </summary>
    public IReadOnlyList<StepResponse>? Steps { get; init; }

    /// <summary>
    /// Error message if the story failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Response for a single step within a story.
/// </summary>
public sealed record StepResponse
{
    /// <summary>
    /// Unique identifier of the step.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Order of this step within the story.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Name/title of the step.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Current status of the step (may not be present in all responses).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Output produced by the step execution.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error message if the step failed.
    /// </summary>
    public string? Error { get; init; }
}
