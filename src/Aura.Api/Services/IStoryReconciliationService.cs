// <copyright file="IStoryReconciliationService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Services;

/// <summary>
/// Reconciles story step statuses based on git commit messages, used to recover
/// step status after a service crash during story execution.
/// </summary>
public interface IStoryReconciliationService
{
    /// <summary>
    /// Reconciles step statuses for the specified story by inspecting git commit messages,
    /// recovering any steps that were left in an inconsistent state due to a service crash.
    /// </summary>
    /// <param name="storyId">The unique identifier of the story to reconcile.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The number of steps reconciled.</returns>
    Task<int> ReconcileStepStatusesAsync(Guid storyId, CancellationToken ct);
}
