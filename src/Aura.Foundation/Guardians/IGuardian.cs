// <copyright file="IGuardian.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Guardians;

/// <summary>
/// Interface for guardian implementations.
/// Guardians monitor repository health and create workflows when issues are detected.
/// </summary>
public interface IGuardian
{
    /// <summary>Gets the guardian definition.</summary>
    GuardianDefinition Definition { get; }

    /// <summary>Gets the guardian ID.</summary>
    string Id => Definition.Id;

    /// <summary>Gets the guardian name.</summary>
    string Name => Definition.Name;

    /// <summary>
    /// Check for violations.
    /// </summary>
    /// <param name="context">The check context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The check result containing any violations.</returns>
    Task<GuardianCheckResult> CheckAsync(GuardianContext context, CancellationToken ct = default);
}

/// <summary>
/// Context for a guardian check operation.
/// </summary>
public sealed record GuardianContext
{
    /// <summary>Gets the repository root path.</summary>
    public required string RepositoryPath { get; init; }

    /// <summary>Gets what triggered this check.</summary>
    public GuardianTriggerType TriggerType { get; init; } = GuardianTriggerType.Manual;

    /// <summary>Gets the files changed since last check (for incremental checks).</summary>
    public IReadOnlyList<string>? ChangedFiles { get; init; }

    /// <summary>Gets external data (webhook payload, CI logs, etc.).</summary>
    public IReadOnlyDictionary<string, object>? ExternalData { get; init; }

    /// <summary>Gets the workspace ID (if indexed).</summary>
    public Guid? WorkspaceId { get; init; }
}
