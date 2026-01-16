// <copyright file="GuardianCheckResult.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Guardians;

/// <summary>
/// Result of a guardian check operation.
/// </summary>
public sealed record GuardianCheckResult
{
    /// <summary>Gets whether any violations were found.</summary>
    public bool HasViolations => Violations.Count > 0;

    /// <summary>Gets the violations found.</summary>
    public IReadOnlyList<GuardianViolation> Violations { get; init; } = [];

    /// <summary>Gets the check metrics.</summary>
    public GuardianMetrics Metrics { get; init; } = new();

    /// <summary>Creates a successful result with no violations.</summary>
    public static GuardianCheckResult Success(GuardianMetrics? metrics = null)
        => new() { Metrics = metrics ?? new() };

    /// <summary>Creates a result with violations.</summary>
    public static GuardianCheckResult WithViolations(
        IReadOnlyList<GuardianViolation> violations,
        GuardianMetrics? metrics = null)
        => new() { Violations = violations, Metrics = metrics ?? new() };
}

/// <summary>
/// A violation detected by a guardian.
/// </summary>
public sealed record GuardianViolation
{
    /// <summary>Gets the rule that was violated.</summary>
    public required string RuleId { get; init; }

    /// <summary>Gets the violation summary.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the file path (if applicable).</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets the line number (if applicable).</summary>
    public int? LineNumber { get; init; }

    /// <summary>Gets the severity.</summary>
    public ViolationSeverity Severity { get; init; } = ViolationSeverity.Warning;

    /// <summary>Gets additional context for the workflow template.</summary>
    public IReadOnlyDictionary<string, string> Context { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Severity of a violation.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>Informational, not an error.</summary>
    Info,

    /// <summary>Warning, should be addressed.</summary>
    Warning,

    /// <summary>Error, must be addressed.</summary>
    Error,

    /// <summary>Critical, blocking issue.</summary>
    Critical,
}

/// <summary>
/// Metrics from a guardian check.
/// </summary>
public sealed record GuardianMetrics
{
    /// <summary>Gets how long the check took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets the number of files scanned.</summary>
    public int FilesScanned { get; init; }

    /// <summary>Gets the number of violations found.</summary>
    public int ViolationsFound { get; init; }

    /// <summary>Gets when the check was performed.</summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
}
