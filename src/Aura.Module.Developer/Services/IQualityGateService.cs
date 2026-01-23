// <copyright file="IQualityGateService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Runs quality gates (build, test) between orchestrator waves.
/// </summary>
public interface IQualityGateService
{
    /// <summary>
    /// Runs a build quality gate.
    /// </summary>
    /// <param name="worktreePath">The worktree path to build.</param>
    /// <param name="afterWave">The wave number this gate follows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gate result.</returns>
    Task<QualityGateResult> RunBuildGateAsync(string worktreePath, int afterWave, CancellationToken ct = default);

    /// <summary>
    /// Runs a test quality gate.
    /// </summary>
    /// <param name="worktreePath">The worktree path to test.</param>
    /// <param name="afterWave">The wave number this gate follows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gate result.</returns>
    Task<QualityGateResult> RunTestGateAsync(string worktreePath, int afterWave, CancellationToken ct = default);

    /// <summary>
    /// Runs both build and test quality gates.
    /// </summary>
    /// <param name="worktreePath">The worktree path.</param>
    /// <param name="afterWave">The wave number this gate follows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The combined gate result.</returns>
    Task<QualityGateResult> RunFullGateAsync(string worktreePath, int afterWave, CancellationToken ct = default);
}
