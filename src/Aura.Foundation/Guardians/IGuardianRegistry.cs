// <copyright file="IGuardianRegistry.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Guardians;

/// <summary>
/// Registry for guardian definitions with hot-reload support.
/// </summary>
public interface IGuardianRegistry
{
    /// <summary>Gets all registered guardians.</summary>
    IReadOnlyList<GuardianDefinition> Guardians { get; }

    /// <summary>Gets a guardian by ID.</summary>
    /// <param name="guardianId">The guardian ID.</param>
    /// <returns>The guardian definition, or null if not found.</returns>
    GuardianDefinition? GetGuardian(string guardianId);

    /// <summary>Gets guardians that match a trigger type.</summary>
    /// <param name="triggerType">The trigger type to match.</param>
    /// <returns>List of matching guardians.</returns>
    IReadOnlyList<GuardianDefinition> GetByTriggerType(GuardianTriggerType triggerType);

    /// <summary>Reloads guardian definitions from disk.</summary>
    /// <returns>A task representing the async operation.</returns>
    Task ReloadAsync();

    /// <summary>
    /// Adds a directory to watch for guardian files.
    /// </summary>
    /// <param name="directory">The directory path.</param>
    void AddWatchDirectory(string directory);

    /// <summary>Event raised when guardians are added, updated, or removed.</summary>
    event EventHandler<GuardianRegistryChangedEventArgs>? GuardiansChanged;
}

/// <summary>
/// Event args for guardian registry changes.
/// </summary>
public sealed class GuardianRegistryChangedEventArgs : EventArgs
{
    /// <summary>Gets the type of change.</summary>
    public required GuardianChangeType ChangeType { get; init; }

    /// <summary>Gets the guardian ID affected.</summary>
    public required string GuardianId { get; init; }

    /// <summary>Gets the guardian definition (for Add/Update).</summary>
    public GuardianDefinition? Guardian { get; init; }
}

/// <summary>
/// Type of guardian registry change.
/// </summary>
public enum GuardianChangeType
{
    /// <summary>Guardian was added.</summary>
    Added,

    /// <summary>Guardian was updated.</summary>
    Updated,

    /// <summary>Guardian was removed.</summary>
    Removed,
}
