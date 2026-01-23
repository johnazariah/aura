// <copyright file="GuardianExecutor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Guardians;

using System.Diagnostics;
using System.Text.Json;
using Aura.Foundation.Guardians;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes guardian checks and creates workflows from violations.
/// </summary>
public sealed class GuardianExecutor : IGuardianExecutor
{
    private readonly IStoryService _workflowService;
    private readonly ILogger<GuardianExecutor> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardianExecutor"/> class.
    /// </summary>
    /// <param name="workflowService">Workflow service for creating workflows.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Time provider for testability.</param>
    public GuardianExecutor(
        IStoryService workflowService,
        ILogger<GuardianExecutor> logger,
        TimeProvider? timeProvider = null)
    {
        _workflowService = workflowService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task<GuardianExecutionResult> ExecuteAsync(
        GuardianDefinition guardian,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(guardian, new GuardianExecutionContext
        {
            TriggerType = GuardianTriggerType.Manual,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<GuardianExecutionResult> ExecuteAsync(
        GuardianDefinition guardian,
        GuardianExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Executing guardian: {GuardianId}", guardian.Id);

        try
        {
            // Run the guardian check
            var checkResult = await RunCheckAsync(guardian, context, cancellationToken);

            if (!checkResult.HasViolations)
            {
                _logger.LogInformation("Guardian {GuardianId} found no violations", guardian.Id);
                return new GuardianExecutionResult
                {
                    GuardianId = guardian.Id,
                    Status = GuardianExecutionStatus.Clean,
                    CheckResult = checkResult,
                    Duration = stopwatch.Elapsed,
                    CompletedAt = _timeProvider.GetUtcNow(),
                };
            }

            // Create workflows for violations
            var createdWorkflowIds = new List<Guid>();
            foreach (var violation in checkResult.Violations)
            {
                var workflowId = await CreateWorkflowForViolationAsync(
                    guardian,
                    violation,
                    context,
                    cancellationToken);

                if (workflowId.HasValue)
                {
                    createdWorkflowIds.Add(workflowId.Value);
                }
            }

            _logger.LogInformation(
                "Guardian {GuardianId} found {ViolationCount} violations, created {WorkflowCount} workflows",
                guardian.Id,
                checkResult.Violations.Count,
                createdWorkflowIds.Count);

            return new GuardianExecutionResult
            {
                GuardianId = guardian.Id,
                Status = GuardianExecutionStatus.ViolationsFound,
                CheckResult = checkResult,
                CreatedWorkflowIds = createdWorkflowIds,
                Duration = stopwatch.Elapsed,
                CompletedAt = _timeProvider.GetUtcNow(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guardian execution failed: {GuardianId}", guardian.Id);
            return new GuardianExecutionResult
            {
                GuardianId = guardian.Id,
                Status = GuardianExecutionStatus.Failed,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                CompletedAt = _timeProvider.GetUtcNow(),
            };
        }
    }

    private Task<GuardianCheckResult> RunCheckAsync(
        GuardianDefinition guardian,
        GuardianExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Detection is configured via Sources/Rules in GuardianDetection
        // For now, this is a placeholder - real implementation would:
        // 1. Check Sources (e.g., GitHub Actions API)
        // 2. Apply Rules against the codebase
        // 3. Run Commands if configured

        var detection = guardian.Detection;
        if (detection?.Sources is { Count: > 0 })
        {
            _logger.LogDebug(
                "Source-based detection for {GuardianId}: {SourceCount} sources",
                guardian.Id,
                detection.Sources.Count);
        }

        if (detection?.Rules is { Count: > 0 })
        {
            _logger.LogDebug(
                "Rule-based detection for {GuardianId}: {RuleCount} rules",
                guardian.Id,
                detection.Rules.Count);
        }

        if (detection?.Commands is { Count: > 0 })
        {
            _logger.LogDebug(
                "Command-based detection for {GuardianId}: {CommandCount} commands",
                guardian.Id,
                detection.Commands.Count);
        }

        // Return empty result for now - real detection to be implemented
        return Task.FromResult(GuardianCheckResult.Success(new GuardianMetrics
        {
            CheckedAt = _timeProvider.GetUtcNow(),
        }));
    }

    private async Task<Guid?> CreateWorkflowForViolationAsync(
        GuardianDefinition guardian,
        GuardianViolation violation,
        GuardianExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = guardian.Workflow;

            // Build title from template or violation
            var title = template?.Title is not null
                ? ReplacePlaceholders(template.Title, violation)
                : $"[{guardian.Name}] {violation.Summary}";

            // Build description
            var description = template?.Description is not null
                ? ReplacePlaceholders(template.Description, violation)
                : BuildDefaultDescription(guardian, violation);

            // Determine priority from template or severity
            var priority = ParsePriority(template?.Priority) ?? violation.Severity switch
            {
                ViolationSeverity.Critical => StoryPriority.Critical,
                ViolationSeverity.Error => StoryPriority.High,
                ViolationSeverity.Warning => StoryPriority.Medium,
                ViolationSeverity.Info => StoryPriority.Low,
                _ => StoryPriority.Medium,
            };

            // Create workflow context with violation details
            var workflowContext = JsonSerializer.Serialize(new
            {
                guardian = guardian.Id,
                violation = new
                {
                    violation.RuleId,
                    violation.Summary,
                    violation.FilePath,
                    violation.LineNumber,
                    violation.Severity,
                    violation.Context,
                },
                detectedAt = _timeProvider.GetUtcNow(),
            });

            var workflow = await _workflowService.CreateFromGuardianAsync(
                new GuardianWorkflowRequest
                {
                    Title = title,
                    Description = description,
                    RepositoryPath = context.WorkspacePath,
                    GuardianId = guardian.Id,
                    Priority = priority,
                    SuggestedCapability = template?.SuggestedCapability,
                    Context = workflowContext,
                },
                cancellationToken);

            return workflow.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create workflow for violation from guardian {GuardianId}",
                guardian.Id);
            return null;
        }
    }

    private static StoryPriority? ParsePriority(string? priority) => priority?.ToLowerInvariant() switch
    {
        "critical" => StoryPriority.Critical,
        "high" => StoryPriority.High,
        "medium" => StoryPriority.Medium,
        "low" => StoryPriority.Low,
        _ => null,
    };

    private static string ReplacePlaceholders(string template, GuardianViolation violation)
    {
        return template
            .Replace("{file}", violation.FilePath ?? string.Empty)
            .Replace("{line}", violation.LineNumber?.ToString() ?? string.Empty)
            .Replace("{summary}", violation.Summary)
            .Replace("{rule}", violation.RuleId)
            .Replace("{severity}", violation.Severity.ToString());
    }

    private static string BuildDefaultDescription(GuardianDefinition guardian, GuardianViolation violation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**Detected by:** {guardian.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Rule:** {violation.RuleId}");
        sb.AppendLine($"**Severity:** {violation.Severity}");

        if (!string.IsNullOrEmpty(violation.FilePath))
        {
            sb.AppendLine($"**File:** {violation.FilePath}");
            if (violation.LineNumber.HasValue)
            {
                sb.AppendLine($"**Line:** {violation.LineNumber}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("**Details:**");
        sb.AppendLine(violation.Summary);

        return sb.ToString();
    }
}
