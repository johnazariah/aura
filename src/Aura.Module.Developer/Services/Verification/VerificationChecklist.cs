// <copyright file="VerificationChecklist.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Structured verification checklist with categories and findings.
/// </summary>
public sealed record VerificationChecklist
{
    /// <summary>Gets or sets the overall summary.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets or sets the review decision.</summary>
    public required ReviewDecision Decision { get; init; }

    /// <summary>Gets or sets the functional requirements checklist.</summary>
    public ChecklistCategory? Functional { get; init; }

    /// <summary>Gets or sets the code quality checklist.</summary>
    public ChecklistCategory? CodeQuality { get; init; }

    /// <summary>Gets or sets the testing checklist.</summary>
    public ChecklistCategory? Testing { get; init; }

    /// <summary>Gets or sets the architecture checklist.</summary>
    public ChecklistCategory? Architecture { get; init; }

    /// <summary>Gets or sets the findings from verification.</summary>
    public VerificationFindings? Findings { get; init; }

    /// <summary>Gets or sets build verification results.</summary>
    public BuildResult? Build { get; init; }

    /// <summary>Gets or sets test verification results.</summary>
    public TestResult? Tests { get; init; }

    /// <summary>Gets or sets lint verification results.</summary>
    public LintResult? Lint { get; init; }
}

/// <summary>
/// Review decision options.
/// </summary>
public enum ReviewDecision
{
    /// <summary>Changes are approved.</summary>
    Approved,

    /// <summary>Changes requested before approval.</summary>
    ChangesRequested,

    /// <summary>Changes are rejected.</summary>
    Rejected
}

/// <summary>
/// A category of checklist items.
/// </summary>
public sealed record ChecklistCategory
{
    /// <summary>Gets or sets individual checklist items.</summary>
    public required IReadOnlyDictionary<string, ChecklistItem> Items { get; init; }

    /// <summary>Gets or sets notes for this category.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// A single checklist item.
/// </summary>
public sealed record ChecklistItem
{
    /// <summary>Gets or sets whether this item passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Gets or sets optional notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Findings from the verification process.
/// </summary>
public sealed record VerificationFindings
{
    /// <summary>Gets or sets issues that must be fixed.</summary>
    public IReadOnlyList<string>? MustFix { get; init; }

    /// <summary>Gets or sets issues that should be fixed.</summary>
    public IReadOnlyList<string>? ShouldFix { get; init; }

    /// <summary>Gets or sets suggestions for improvement.</summary>
    public IReadOnlyList<string>? Suggestions { get; init; }
}

/// <summary>
/// Build verification result.
/// </summary>
public sealed record BuildResult
{
    /// <summary>Gets or sets whether the build passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Gets or sets the build output.</summary>
    public string? Output { get; init; }

    /// <summary>Gets or sets error count.</summary>
    public int Errors { get; init; }

    /// <summary>Gets or sets warning count.</summary>
    public int Warnings { get; init; }
}

/// <summary>
/// Test verification result.
/// </summary>
public sealed record TestResult
{
    /// <summary>Gets or sets whether tests passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Gets or sets total test count.</summary>
    public int Total { get; init; }

    /// <summary>Gets or sets passed test count.</summary>
    public int PassedCount { get; init; }

    /// <summary>Gets or sets failed test count.</summary>
    public int Failed { get; init; }

    /// <summary>Gets or sets skipped test count.</summary>
    public int Skipped { get; init; }

    /// <summary>Gets or sets test output.</summary>
    public string? Output { get; init; }
}

/// <summary>
/// Lint verification result.
/// </summary>
public sealed record LintResult
{
    /// <summary>Gets or sets whether lint passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Gets or sets error count.</summary>
    public int Errors { get; init; }

    /// <summary>Gets or sets warning count.</summary>
    public int Warnings { get; init; }

    /// <summary>Gets or sets lint output.</summary>
    public string? Output { get; init; }
}
