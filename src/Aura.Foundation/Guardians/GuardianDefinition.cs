// <copyright file="Guardian.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Guardians;

/// <summary>
/// Represents a guardian definition loaded from YAML.
/// Guardians monitor repository health and create workflows when issues are detected.
/// </summary>
public sealed record GuardianDefinition
{
    /// <summary>Gets the unique identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the human-readable name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the version.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the triggers that activate this guardian.</summary>
    public IReadOnlyList<GuardianTrigger> Triggers { get; init; } = [];

    /// <summary>Gets the detection configuration.</summary>
    public GuardianDetection? Detection { get; init; }

    /// <summary>Gets the workflow template for created workflows.</summary>
    public GuardianWorkflowTemplate? Workflow { get; init; }
}

/// <summary>
/// A trigger that activates a guardian.
/// </summary>
public sealed record GuardianTrigger
{
    /// <summary>Gets the trigger type.</summary>
    public required GuardianTriggerType Type { get; init; }

    /// <summary>Gets the cron expression (for schedule triggers).</summary>
    public string? Cron { get; init; }

    /// <summary>Gets the file patterns to watch (for file_changed triggers).</summary>
    public IReadOnlyList<string>? Patterns { get; init; }

    /// <summary>Gets the debounce time in seconds (for file_changed triggers).</summary>
    public int? Debounce { get; init; }

    /// <summary>Gets the webhook events to listen for (for webhook triggers).</summary>
    public IReadOnlyList<string>? Events { get; init; }
}

/// <summary>
/// The type of guardian trigger.
/// </summary>
public enum GuardianTriggerType
{
    /// <summary>Triggered on a schedule (cron expression).</summary>
    Schedule,

    /// <summary>Triggered when files change.</summary>
    FileChanged,

    /// <summary>Triggered by webhook events.</summary>
    Webhook,

    /// <summary>Triggered manually via API.</summary>
    Manual,
}

/// <summary>
/// Detection configuration for a guardian.
/// </summary>
public sealed record GuardianDetection
{
    /// <summary>Gets the sources to check.</summary>
    public IReadOnlyList<GuardianSource>? Sources { get; init; }

    /// <summary>Gets the detection rules.</summary>
    public IReadOnlyList<GuardianRule>? Rules { get; init; }

    /// <summary>Gets the commands to run per language/tool.</summary>
    public IReadOnlyDictionary<string, string>? Commands { get; init; }

    /// <summary>Gets the thresholds for violation detection.</summary>
    public IReadOnlyDictionary<string, int>? Thresholds { get; init; }
}

/// <summary>
/// A detection source (e.g., GitHub Actions, Azure Pipelines).
/// </summary>
public sealed record GuardianSource
{
    /// <summary>Gets the source type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the branches to monitor.</summary>
    public IReadOnlyList<string>? Branches { get; init; }
}

/// <summary>
/// A detection rule.
/// </summary>
public sealed record GuardianRule
{
    /// <summary>Gets the rule identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the rule description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the languages this rule applies to.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>Gets the check to perform.</summary>
    public string? Check { get; init; }
}

/// <summary>
/// Template for workflows created by a guardian.
/// </summary>
public sealed record GuardianWorkflowTemplate
{
    /// <summary>Gets the workflow title template (supports placeholders).</summary>
    public required string Title { get; init; }

    /// <summary>Gets the workflow description template.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the suggested agent capability.</summary>
    public string? SuggestedCapability { get; init; }

    /// <summary>Gets the priority for created workflows.</summary>
    public string Priority { get; init; } = "medium";

    /// <summary>Gets the execution mode for created workflows.</summary>
    public string Mode { get; init; } = "structured";

    /// <summary>Gets the context to gather for the workflow.</summary>
    public IReadOnlyList<string>? ContextGathering { get; init; }
}
