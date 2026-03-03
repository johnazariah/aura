// <copyright file="McpHandler.Index.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;
using Aura.Foundation.Rag;

public sealed partial class McpHandler
{
    /// <summary>
    /// aura_index - Trigger and manage content indexing.
    /// </summary>
    private async Task<object> IndexAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args.GetStringOrDefault("operation") ?? "index_directory";

        return operation switch
        {
            "index_directory" => await IndexDirectoryAsync(args, ct),
            "index_file" => await IndexFileAsync(args, ct),
            "status" => GetIndexJobStatus(args),
            "stats" => await GetIndexStatsAsync(args, ct),
            _ => new { error = $"Unknown index operation: {operation}" }
        };
    }

    private async Task<object> IndexDirectoryAsync(JsonElement? args, CancellationToken ct)
    {
        var path = args.GetStringOrDefault("path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return new { error = "path is required for index_directory operation" };
        }

        if (!Directory.Exists(path))
        {
            return new { error = $"Directory not found: {path}" };
        }

        var recursive = true;
        if (args.HasValue && args.Value.TryGetProperty("recursive", out var recursiveEl))
        {
            recursive = recursiveEl.GetBoolean();
        }

        IReadOnlyList<string>? includePatterns = null;
        if (args.HasValue && args.Value.TryGetProperty("filePattern", out var patternEl))
        {
            var pattern = patternEl.GetString();
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                includePatterns = [pattern];
            }
        }

        var options = new RagIndexOptions
        {
            Recursive = recursive,
            IncludePatterns = includePatterns,
        };

        var (jobId, isNew) = _backgroundIndexer.QueueDirectory(path, options);

        _logger.LogInformation(
            "aura_index(index_directory): path={Path}, recursive={Recursive}, jobId={JobId}, isNew={IsNew}",
            path, recursive, jobId, isNew);

        return new
        {
            success = true,
            jobId,
            isNewJob = isNew,
            path,
            message = isNew
                ? "Indexing started. Use status operation with jobId to track progress."
                : "Indexing already in progress for this directory."
        };
    }

    private async Task<object> IndexFileAsync(JsonElement? args, CancellationToken ct)
    {
        var path = args.GetStringOrDefault("path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return new { error = "path is required for index_file operation" };
        }

        if (!File.Exists(path))
        {
            return new { error = $"File not found: {path}" };
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var ragContent = RagContent.FromFile(path, content);

            var queued = _backgroundIndexer.QueueContent(ragContent);
            if (!queued)
            {
                return new { error = "Index queue is full. Try again later." };
            }

            _logger.LogInformation("aura_index(index_file): path={Path}", path);

            return new
            {
                success = true,
                path,
                contentType = ragContent.ContentType.ToString(),
                message = "File queued for indexing."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue file for indexing: {Path}", path);
            return new { error = $"Failed to index file: {ex.Message}" };
        }
    }

    private object GetIndexJobStatus(JsonElement? args)
    {
        var jobIdStr = args.GetStringOrDefault("jobId");

        // If no jobId, return all active jobs
        if (string.IsNullOrWhiteSpace(jobIdStr))
        {
            var activeJobs = _backgroundIndexer.GetActiveJobs();
            var indexerStatus = _backgroundIndexer.GetStatus();

            return new
            {
                queuedItems = indexerStatus.QueuedItems,
                processedItems = indexerStatus.ProcessedItems,
                failedItems = indexerStatus.FailedItems,
                isProcessing = indexerStatus.IsProcessing,
                activeJobs = activeJobs.Select(j => new
                {
                    jobId = j.JobId,
                    source = j.Source,
                    state = j.State.ToString().ToLowerInvariant(),
                    processedItems = j.ProcessedItems,
                    totalItems = j.TotalItems,
                    progressPercent = j.ProgressPercent,
                    error = j.Error,
                })
            };
        }

        if (!Guid.TryParse(jobIdStr, out var jobId))
        {
            return new { error = $"Invalid jobId format: {jobIdStr}" };
        }

        var status = _backgroundIndexer.GetJobStatus(jobId);
        if (status is null)
        {
            return new { error = $"Job not found: {jobId}" };
        }

        return new
        {
            jobId = status.JobId,
            source = status.Source,
            state = status.State.ToString().ToLowerInvariant(),
            totalItems = status.TotalItems,
            processedItems = status.ProcessedItems,
            failedItems = status.FailedItems,
            progressPercent = status.ProgressPercent,
            startedAt = status.StartedAt,
            completedAt = status.CompletedAt,
            error = status.Error,
        };
    }

    private async Task<object> GetIndexStatsAsync(JsonElement? args, CancellationToken ct)
    {
        var path = args.GetStringOrDefault("path");

        if (!string.IsNullOrWhiteSpace(path))
        {
            var dirStats = await _ragService.GetDirectoryStatsAsync(path, ct);
            if (dirStats is null)
            {
                return new
                {
                    path,
                    isIndexed = false,
                    message = "No index data found for this path."
                };
            }

            return new
            {
                path = dirStats.DirectoryPath,
                isIndexed = dirStats.IsIndexed,
                chunkCount = dirStats.ChunkCount,
                fileCount = dirStats.FileCount,
                lastIndexedAt = dirStats.LastIndexedAt,
            };
        }

        // Global stats
        var stats = await _ragService.GetStatsAsync(ct);
        return new
        {
            totalChunks = stats.TotalChunks,
            totalDocuments = stats.TotalDocuments,
            indexSizeBytes = stats.IndexSizeBytes,
            byContentType = stats.ByContentType?.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value),
            isHealthy = await _ragService.IsHealthyAsync(ct),
        };
    }
}
