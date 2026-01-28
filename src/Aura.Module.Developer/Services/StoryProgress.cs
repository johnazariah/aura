// <copyright file="StoryProgress.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data.Entities;

/// <summary>
/// Types of story progress events for SSE streaming.
/// </summary>
public enum StoryProgressEventType
{
    /// <summary>Story execution started.</summary>
    Started,

    /// <summary>Wave execution started.</summary>
    WaveStarted,

    /// <summary>Step execution started.</summary>
    StepStarted,

    /// <summary>Step produced output (agent streaming text).</summary>
    StepOutput,

    /// <summary>Step completed successfully.</summary>
    StepCompleted,

    /// <summary>Step failed.</summary>
    StepFailed,

    /// <summary>Wave completed.</summary>
    WaveCompleted,

    /// <summary>Quality gate started.</summary>
    GateStarted,

    /// <summary>Quality gate passed.</summary>
    GatePassed,

    /// <summary>Quality gate failed.</summary>
    GateFailed,

    /// <summary>All steps complete, ready for finalization.</summary>
    ReadyToComplete,

    /// <summary>Story completed and finalized.</summary>
    Completed,

    /// <summary>Story failed.</summary>
    Failed,

    /// <summary>Story cancelled.</summary>
    Cancelled,
}

/// <summary>
/// A progress event emitted during story execution.
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="StoryId">The story ID.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="Wave">Current wave number (if applicable).</param>
/// <param name="TotalWaves">Total wave count (if known).</param>
/// <param name="StepId">Step ID (if applicable).</param>
/// <param name="StepName">Step name (if applicable).</param>
/// <param name="Output">Output content (for StepOutput events).</param>
/// <param name="Error">Error message (if applicable).</param>
/// <param name="GateResult">Quality gate result (if applicable).</param>
public sealed record StoryProgressEvent(
    StoryProgressEventType Type,
    Guid StoryId,
    DateTimeOffset Timestamp,
    int? Wave = null,
    int? TotalWaves = null,
    Guid? StepId = null,
    string? StepName = null,
    string? Output = null,
    string? Error = null,
    QualityGateResult? GateResult = null)
{
    /// <summary>
    /// Creates a Started event.
    /// </summary>
    public static StoryProgressEvent Started(Guid storyId, int totalWaves) => new(
        StoryProgressEventType.Started,
        storyId,
        DateTimeOffset.UtcNow,
        TotalWaves: totalWaves);

    /// <summary>
    /// Creates a WaveStarted event.
    /// </summary>
    public static StoryProgressEvent WaveStarted(Guid storyId, int wave, int totalWaves) => new(
        StoryProgressEventType.WaveStarted,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        TotalWaves: totalWaves);

    /// <summary>
    /// Creates a StepStarted event.
    /// </summary>
    public static StoryProgressEvent StepStarted(Guid storyId, Guid stepId, string stepName, int wave) => new(
        StoryProgressEventType.StepStarted,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        StepId: stepId,
        StepName: stepName);

    /// <summary>
    /// Creates a StepOutput event with streaming content.
    /// </summary>
    public static StoryProgressEvent StepOutput(Guid storyId, Guid stepId, string output) => new(
        StoryProgressEventType.StepOutput,
        storyId,
        DateTimeOffset.UtcNow,
        StepId: stepId,
        Output: output);

    /// <summary>
    /// Creates a StepCompleted event.
    /// </summary>
    public static StoryProgressEvent StepCompleted(Guid storyId, Guid stepId, string stepName, string? output) => new(
        StoryProgressEventType.StepCompleted,
        storyId,
        DateTimeOffset.UtcNow,
        StepId: stepId,
        StepName: stepName,
        Output: output);

    /// <summary>
    /// Creates a StepFailed event.
    /// </summary>
    public static StoryProgressEvent StepFailed(Guid storyId, Guid stepId, string stepName, string? error) => new(
        StoryProgressEventType.StepFailed,
        storyId,
        DateTimeOffset.UtcNow,
        StepId: stepId,
        StepName: stepName,
        Error: error);

    /// <summary>
    /// Creates a WaveCompleted event.
    /// </summary>
    public static StoryProgressEvent WaveCompleted(Guid storyId, int wave, int completed, int failed) => new(
        StoryProgressEventType.WaveCompleted,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        Output: $"{completed} completed, {failed} failed");

    /// <summary>
    /// Creates a GateStarted event.
    /// </summary>
    public static StoryProgressEvent GateStarted(Guid storyId, int wave) => new(
        StoryProgressEventType.GateStarted,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave);

    /// <summary>
    /// Creates a GatePassed event.
    /// </summary>
    public static StoryProgressEvent GatePassed(Guid storyId, int wave, QualityGateResult result) => new(
        StoryProgressEventType.GatePassed,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        GateResult: result);

    /// <summary>
    /// Creates a GateFailed event.
    /// </summary>
    public static StoryProgressEvent GateFailed(Guid storyId, int wave, QualityGateResult result) => new(
        StoryProgressEventType.GateFailed,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        GateResult: result,
        Error: result.Error);

    /// <summary>
    /// Creates a ReadyToComplete event - all steps done, awaiting finalization.
    /// </summary>
    public static StoryProgressEvent ReadyToComplete(Guid storyId, int totalWaves) => new(
        StoryProgressEventType.ReadyToComplete,
        storyId,
        DateTimeOffset.UtcNow,
        TotalWaves: totalWaves);

    /// <summary>
    /// Creates a Completed event.
    /// </summary>
    public static StoryProgressEvent Completed(Guid storyId, int totalWaves) => new(
        StoryProgressEventType.Completed,
        storyId,
        DateTimeOffset.UtcNow,
        TotalWaves: totalWaves);

    /// <summary>
    /// Creates a Failed event.
    /// </summary>
    public static StoryProgressEvent Failed(Guid storyId, int wave, string error) => new(
        StoryProgressEventType.Failed,
        storyId,
        DateTimeOffset.UtcNow,
        Wave: wave,
        Error: error);

    /// <summary>
    /// Creates a Cancelled event.
    /// </summary>
    public static StoryProgressEvent Cancelled(Guid storyId) => new(
        StoryProgressEventType.Cancelled,
        storyId,
        DateTimeOffset.UtcNow);
}
