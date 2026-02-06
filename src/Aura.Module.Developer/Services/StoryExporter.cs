// <copyright file="StoryExporter.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aura.Module.Developer.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for exporting story artifacts as markdown files.
/// </summary>
public sealed partial class StoryExporter(
    IStoryService storyService,
    ILogger<StoryExporter> logger) : IStoryExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<StoryExportResult> ExportAsync(
        Guid storyId,
        StoryExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var story = await storyService.GetByIdWithStepsAsync(storyId, cancellationToken);
        if (story is null)
        {
            throw new InvalidOperationException($"Story {storyId} not found");
        }

        // Determine output path
        var outputPath = DetermineOutputPath(story, request.OutputPath);
        var exported = new List<ExportedFile>();
        var warnings = new List<string>();

        // Determine which artifacts to export
        var include = request.Include ?? ["research", "plan", "changes"];

        foreach (var artifact in include)
        {
            try
            {
                var exportedFile = artifact.ToLowerInvariant() switch
                {
                    "research" => await ExportResearchAsync(story, outputPath, cancellationToken),
                    "plan" => await ExportPlanAsync(story, outputPath, cancellationToken),
                    "changes" => await ExportChangesAsync(story, outputPath, cancellationToken),
                    _ => null,
                };

                if (exportedFile is not null)
                {
                    exported.Add(exportedFile);
                }
                else
                {
                    warnings.Add($"Unknown artifact type: {artifact}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to export {Artifact} for story {StoryId}", artifact, storyId);
                warnings.Add($"Failed to export {artifact}: {ex.Message}");
            }
        }

        return new StoryExportResult
        {
            Exported = exported,
            Warnings = warnings.Count > 0 ? warnings : null,
        };
    }

    private static string DetermineOutputPath(Story story, string? requestedPath)
    {
        if (!string.IsNullOrEmpty(requestedPath))
        {
            return requestedPath;
        }

        // Use worktree path if available
        if (!string.IsNullOrEmpty(story.WorktreePath))
        {
            return Path.Combine(story.WorktreePath, ".project");
        }

        // Use repository path
        if (!string.IsNullOrEmpty(story.RepositoryPath))
        {
            return Path.Combine(story.RepositoryPath, ".project");
        }

        throw new InvalidOperationException(
            "Cannot determine output path. Story has no worktree or repository path, and no outputPath was provided.");
    }

    private async Task<ExportedFile> ExportResearchAsync(
        Story story,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var slug = Slugify(story.Title);
        var date = story.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Research: {story.Title}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Story ID:** {story.Id}");

        // Parse analyzed context
        AnalyzedContextData? context = null;
        if (!string.IsNullOrEmpty(story.AnalyzedContext))
        {
            try
            {
                context = JsonSerializer.Deserialize<AnalyzedContextData>(story.AnalyzedContext, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse AnalyzedContext JSON for story {StoryId}", story.Id);
            }
        }

        if (context is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Analyzed:** {context.AnalyzedAt ?? story.UpdatedAt.ToString("O")}");
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Analyzed:** {story.UpdatedAt:O}");
        }

        sb.AppendLine("**Status:** Complete");
        sb.AppendLine();

        // Summary / Analysis
        sb.AppendLine("## Summary");
        sb.AppendLine();
        if (context?.Analysis is not null)
        {
            sb.AppendLine(context.Analysis);
        }
        else if (!string.IsNullOrEmpty(story.Description))
        {
            sb.AppendLine(story.Description);
        }
        else
        {
            sb.AppendLine("*No analysis available*");
        }

        sb.AppendLine();

        // Core Requirements
        sb.AppendLine("## Core Requirements");
        sb.AppendLine();
        if (context?.CoreRequirements?.Count > 0)
        {
            foreach (var req in context.CoreRequirements)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {req}");
            }
        }
        else
        {
            sb.AppendLine("*Extracted from analysis*");
        }

        sb.AppendLine();

        // Technical Constraints
        sb.AppendLine("## Technical Constraints");
        sb.AppendLine();
        if (context?.Constraints?.Count > 0)
        {
            foreach (var constraint in context.Constraints)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {constraint}");
            }
        }
        else
        {
            sb.AppendLine("*No explicit constraints identified*");
        }

        sb.AppendLine();

        // Files Likely Affected
        sb.AppendLine("## Files Likely Affected");
        sb.AppendLine();
        if (context?.AffectedFiles?.Count > 0)
        {
            foreach (var file in context.AffectedFiles)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{file}`");
            }
        }
        else
        {
            sb.AppendLine("*No files identified*");
        }

        sb.AppendLine();

        // Open Questions
        sb.AppendLine("## Open Questions");
        sb.AppendLine();
        sb.AppendLine("*No explicit open questions tracked*");
        sb.AppendLine();

        // Risks
        sb.AppendLine("## Risks");
        sb.AppendLine();
        sb.AppendLine("*Risk analysis not performed*");
        sb.AppendLine();

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(CultureInfo.InvariantCulture, $"*Exported from Aura workflow {story.Id}*");

        // Write file
        var researchDir = Path.Combine(outputPath, "research");
        Directory.CreateDirectory(researchDir);
        var fileName = $"research-{slug}-{date}.md";
        var filePath = Path.Combine(researchDir, fileName);
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

        logger.LogInformation("Exported research to {Path}", filePath);

        return new ExportedFile
        {
            Type = "research",
            Path = Path.Combine(".project", "research", fileName),
        };
    }

    private async Task<ExportedFile> ExportPlanAsync(
        Story story,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var slug = Slugify(story.Title);
        var date = story.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Plan: {story.Title}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Story ID:** {story.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Planned:** {story.UpdatedAt:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Steps:** {story.Steps.Count}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(story.Description ?? "*No description*");
        sb.AppendLine();

        // Implementation Steps
        sb.AppendLine("## Implementation Steps");
        sb.AppendLine();

        var orderedSteps = story.Steps.OrderBy(s => s.Order).ToList();
        foreach (var step in orderedSteps)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### Step {step.Order}: {step.Name}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Capability:** {step.Capability}");
            if (!string.IsNullOrEmpty(step.Language))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Language:** {step.Language}");
            }

            sb.AppendLine();

            if (!string.IsNullOrEmpty(step.Description))
            {
                sb.AppendLine(step.Description);
                sb.AppendLine();
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {step.Status}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Verification
        sb.AppendLine("## Verification");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(story.VerificationResult))
        {
            try
            {
                var verification = JsonSerializer.Deserialize<VerificationData>(story.VerificationResult, JsonOptions);
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Build: {(verification?.Build?.Passed == true ? "✅ Passed" : "❌ Failed")}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Tests: {(verification?.Tests?.Passed == true ? "✅ Passed" : "❌ Failed")}");
            }
            catch (JsonException)
            {
                sb.AppendLine("- Build: *Unknown*");
                sb.AppendLine("- Tests: *Unknown*");
            }
        }
        else
        {
            sb.AppendLine("- Build: *Not yet verified*");
            sb.AppendLine("- Tests: *Not yet verified*");
        }

        sb.AppendLine();

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(CultureInfo.InvariantCulture, $"*Exported from Aura workflow {story.Id}*");

        // Write file
        var plansDir = Path.Combine(outputPath, "plans");
        Directory.CreateDirectory(plansDir);
        var fileName = $"plan-{slug}-{date}.md";
        var filePath = Path.Combine(plansDir, fileName);
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

        logger.LogInformation("Exported plan to {Path}", filePath);

        return new ExportedFile
        {
            Type = "plan",
            Path = Path.Combine(".project", "plans", fileName),
        };
    }

    private async Task<ExportedFile> ExportChangesAsync(
        Story story,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var slug = Slugify(story.Title);
        var date = story.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Changes: {story.Title}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Story ID:** {story.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Started:** {story.CreatedAt:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Completed:** {story.CompletedAt?.ToString("O") ?? "In Progress"}");
        sb.AppendLine();

        // Progress
        sb.AppendLine("## Progress");
        sb.AppendLine();
        var orderedSteps = story.Steps.OrderBy(s => s.Order).ToList();
        foreach (var step in orderedSteps)
        {
            var status = step.Status switch
            {
                StepStatus.Completed => "[x]",
                StepStatus.Skipped => "[-]",
                _ => "[ ]",
            };
            var icon = step.Status switch
            {
                StepStatus.Completed => "✅",
                StepStatus.Skipped => "⏭️",
                StepStatus.Running => "🔄",
                StepStatus.Failed => "❌",
                _ => "⏳",
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {status} Step {step.Order}: {step.Name} {icon}");
        }

        sb.AppendLine();

        // Step Details
        sb.AppendLine("## Step Details");
        sb.AppendLine();

        foreach (var step in orderedSteps)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### Step {step.Order}: {step.Name}");
            sb.AppendLine();

            if (step.StartedAt.HasValue && step.CompletedAt.HasValue)
            {
                var duration = step.CompletedAt.Value - step.StartedAt.Value;
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Duration:** {duration.TotalSeconds:F1}s");
            }

            if (!string.IsNullOrEmpty(step.AssignedAgentId))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Agent:** {step.AssignedAgentId}");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {step.Status}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(step.Output))
            {
                sb.AppendLine("**Output Summary:**");
                sb.AppendLine();

                // Try to extract a summary from the output
                var summary = ExtractOutputSummary(step.Output);
                sb.AppendLine(summary);
            }

            if (!string.IsNullOrEmpty(step.Error))
            {
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Error:** {step.Error}");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine(CultureInfo.InvariantCulture, $"*Exported from Aura workflow {story.Id}*");

        // Write file
        var changesDir = Path.Combine(outputPath, "changes");
        Directory.CreateDirectory(changesDir);
        var fileName = $"changes-{slug}-{date}.md";
        var filePath = Path.Combine(changesDir, fileName);
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

        logger.LogInformation("Exported changes to {Path}", filePath);

        return new ExportedFile
        {
            Type = "changes",
            Path = Path.Combine(".project", "changes", fileName),
        };
    }

    private static string ExtractOutputSummary(string output)
    {
        // Try to parse as JSON and extract meaningful content
        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            // Look for common summary fields
            if (root.TryGetProperty("summary", out var summary))
            {
                return summary.GetString() ?? "*No summary*";
            }

            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "*No message*";
            }

            if (root.TryGetProperty("result", out var result))
            {
                return result.GetString() ?? result.ToString();
            }

            // Return truncated JSON if no summary field
            var text = output.Length > 500 ? output[..500] + "..." : output;
            return $"```json\n{text}\n```";
        }
        catch (JsonException)
        {
            // Not JSON, return truncated text
            var text = output.Length > 500 ? output[..500] + "..." : output;
            return text;
        }
    }

    private static string Slugify(string title)
    {
        // Convert to lowercase
        var slug = title.ToLowerInvariant();

        // Replace spaces and special characters with hyphens
        slug = SlugifyRegex().Replace(slug, "-");

        // Remove consecutive hyphens
        slug = ConsecutiveHyphensRegex().Replace(slug, "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        // Limit length
        if (slug.Length > 50)
        {
            slug = slug[..50].TrimEnd('-');
        }

        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugifyRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex ConsecutiveHyphensRegex();

    // DTOs for JSON deserialization
    private sealed record AnalyzedContextData
    {
        public string? AnalyzedAt { get; init; }
        public string? Analysis { get; init; }
        public List<string>? CoreRequirements { get; init; }
        public List<string>? Constraints { get; init; }
        public List<string>? AffectedFiles { get; init; }
    }

    private sealed record VerificationData
    {
        public VerificationCheck? Build { get; init; }
        public VerificationCheck? Tests { get; init; }
    }

    private sealed record VerificationCheck
    {
        public bool? Passed { get; init; }
    }
}
