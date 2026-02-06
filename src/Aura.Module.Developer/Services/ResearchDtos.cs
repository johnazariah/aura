// <copyright file="ResearchDtos.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Represents an open question that should be answered during analysis.
/// </summary>
public sealed record OpenQuestion
{
    /// <summary>Gets or sets the question text.</summary>
    public required string Question { get; init; }

    /// <summary>Gets or sets whether the question has been answered.</summary>
    public bool Answered { get; init; }

    /// <summary>Gets or sets the answer if available.</summary>
    public string? Answer { get; init; }

    /// <summary>Gets or sets the source of the answer.</summary>
    public string? Source { get; init; }
}

/// <summary>
/// Represents a risk identified during analysis or planning.
/// </summary>
public sealed record IdentifiedRisk
{
    /// <summary>Gets or sets the risk description.</summary>
    public required string Risk { get; init; }

    /// <summary>Gets or sets the likelihood level (High/Medium/Low).</summary>
    public required string Likelihood { get; init; }

    /// <summary>Gets or sets the impact level (High/Medium/Low).</summary>
    public required string Impact { get; init; }

    /// <summary>Gets or sets the mitigation strategy.</summary>
    public string? Mitigation { get; init; }
}
