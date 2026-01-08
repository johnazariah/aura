// <copyright file="IBackgroundIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Background indexing service that queues documents for async processing.
/// </summary>
public interface IBackgroundIndexer
{
    /// <summary>
    /// Queues content for background indexing.
    /// Returns immediately - indexing happens asynchronously.
    /// </summary>
    /// <param name="content">The content to index.</param>
    /// <returns>True if queued successfully, false if queue is full.</returns>
    bool QueueContent(RagContent content);

    /// <summary>
    /// Queues a directory for background indexing.
    /// Returns immediately - indexing happens asynchronously.
    /// If a job for the same path is already queued or processing, returns that job's ID.
    /// </summary>
    /// <param name="directoryPath">Path to the directory.</param>
    /// <param name="options">Indexing options.</param>
    /// <returns>A tuple of (jobId, isNew) - isNew is false if reusing existing job.</returns>
    (Guid JobId, bool IsNew) QueueDirectory(string directoryPath, RagIndexOptions? options = null);

    /// <summary>
    /// Gets the current queue status.
    /// </summary>
    BackgroundIndexerStatus GetStatus();

    /// <summary>
    /// Gets the status of a specific job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <returns>Job status, or null if not found.</returns>
    IndexJobStatus? GetJobStatus(Guid jobId);
}

/// <summary>
/// Status of the background indexer.
/// </summary>
public record BackgroundIndexerStatus
{
    /// <summary>Gets the number of items currently in the queue.</summary>
    public int QueuedItems { get; init; }

    /// <summary>Gets the number of items processed.</summary>
    public int ProcessedItems { get; init; }

    /// <summary>Gets whether the indexer is currently processing.</summary>
    public bool IsProcessing { get; init; }

    /// <summary>Gets the number of active jobs.</summary>
    public int ActiveJobs { get; init; }

    /// <summary>Gets the number of failed items.</summary>
    public int FailedItems { get; init; }
}

/// <summary>
/// Status of an indexing job.
/// </summary>
public record IndexJobStatus
{
    /// <summary>Gets the job ID.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Gets the source (directory path or content ID).</summary>
    public required string Source { get; init; }

    /// <summary>Gets the job state.</summary>
    public required IndexJobState State { get; init; }

    /// <summary>Gets the total items to process.</summary>
    public int TotalItems { get; init; }

    /// <summary>Gets the items processed so far.</summary>
    public int ProcessedItems { get; init; }

    /// <summary>Gets the number of failed items.</summary>
    public int FailedItems { get; init; }

    /// <summary>Gets when the job started.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Gets when the job completed.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Gets the error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the progress percentage (0-100).</summary>
    public int ProgressPercent => TotalItems > 0 ? (ProcessedItems * 100) / TotalItems : 0;
}

/// <summary>
/// State of an indexing job.
/// </summary>
public enum IndexJobState
{
    /// <summary>Job is queued and waiting to start.</summary>
    Queued,

    /// <summary>Job is currently being processed.</summary>
    Processing,

    /// <summary>Job completed successfully.</summary>
    Completed,

    /// <summary>Job failed with an error.</summary>
    Failed,

    /// <summary>Job was cancelled.</summary>
    Cancelled,
}
